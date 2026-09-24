using CvManagement.Web.Data;
using CvManagement.Web.Services.Abstractions;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.EntityFrameworkCore;
using Directory = Lucene.Net.Store.Directory;

namespace CvManagement.Web.Services.Implementations;

/// <summary>
/// Singleton: owns one long-lived IndexWriter (thread-safe for concurrent add/update/delete per
/// Lucene's own contract) for the lifetime of the app, disposed by DI on shutdown. Reads use a
/// near-real-time reader off the writer so a search sees writes from the same process without a
/// full commit+reopen round trip.
/// </summary>
public class SearchIndexService : ISearchIndexService, IDisposable
{
    private const LuceneVersion Version = LuceneVersion.LUCENE_48;
    private readonly StandardAnalyzer _analyzer = new(Version);
    private readonly Directory _directory;
    private readonly IndexWriter _writer;
    private readonly IServiceScopeFactory _scopeFactory;

    public SearchIndexService(IConfiguration configuration, IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        var path = configuration["Lucene:IndexPath"] ?? "App_Data/search-index";
        System.IO.Directory.CreateDirectory(path);
        _directory = FSDirectory.Open(path);
        var config = new IndexWriterConfig(Version, _analyzer) { OpenMode = OpenMode.CREATE_OR_APPEND };
        _writer = new IndexWriter(_directory, config);
    }

    public async Task RebuildAllAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _writer.DeleteAll();

        var positions = await db.Positions.AsNoTracking()
            .Select(p => new { p.Id, p.Title, p.ShortDescription, p.Company })
            .ToListAsync(ct);
        foreach (var p in positions)
        {
            _writer.AddDocument(BuildPositionDocument(p.Id, p.Title, p.ShortDescription, p.Company));
        }

        foreach (var doc in await BuildCandidateDocumentsAsync(db, userIds: null, ct))
        {
            _writer.AddDocument(doc);
        }

        _writer.Commit();
    }

    public async Task IndexPositionAsync(int positionId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var p = await db.Positions.AsNoTracking()
            .Where(x => x.Id == positionId)
            .Select(x => new { x.Id, x.Title, x.ShortDescription, x.Company })
            .FirstOrDefaultAsync(ct);

        if (p is null)
        {
            await RemovePositionAsync(positionId, ct);
            return;
        }

        _writer.UpdateDocument(new Term("key", PositionKey(positionId)), BuildPositionDocument(p.Id, p.Title, p.ShortDescription, p.Company));
        _writer.Commit();
    }

    public Task RemovePositionAsync(int positionId, CancellationToken ct = default)
    {
        _writer.DeleteDocuments(new Term("key", PositionKey(positionId)));
        _writer.Commit();
        return Task.CompletedTask;
    }

    public async Task IndexCandidateAsync(string userId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var doc = (await BuildCandidateDocumentsAsync(db, [userId], ct)).SingleOrDefault();
        if (doc is null)
        {
            await RemoveCandidateAsync(userId, ct);
            return;
        }

        _writer.UpdateDocument(new Term("key", CandidateKey(userId)), doc);
        _writer.Commit();
    }

    public Task RemoveCandidateAsync(string userId, CancellationToken ct = default)
    {
        _writer.DeleteDocuments(new Term("key", CandidateKey(userId)));
        _writer.Commit();
        return Task.CompletedTask;
    }

    public SearchIndexResults Search(string queryText, bool includeCandidates, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return new SearchIndexResults([], []);
        }

        using var reader = _writer.GetReader(applyAllDeletes: true);
        var searcher = new IndexSearcher(reader);

        var textQuery = BuildTextQuery(_analyzer, queryText);
        if (textQuery is null)
        {
            return new SearchIndexResults([], []);
        }

        var positionIds = new List<int>();
        var candidateIds = new List<string>();

        var positionQuery = new BooleanQuery
        {
            { textQuery, Occur.MUST },
            { new TermQuery(new Term("doctype", "position")), Occur.MUST }
        };
        foreach (var hit in searcher.Search(positionQuery, maxResults).ScoreDocs)
        {
            var doc = searcher.Doc(hit.Doc);
            if (int.TryParse(doc.Get("id"), out var id)) positionIds.Add(id);
        }

        if (includeCandidates)
        {
            var candidateQuery = new BooleanQuery
            {
                { textQuery, Occur.MUST },
                { new TermQuery(new Term("doctype", "candidate")), Occur.MUST }
            };
            foreach (var hit in searcher.Search(candidateQuery, maxResults).ScoreDocs)
            {
                var id = searcher.Doc(hit.Doc).Get("id");
                if (id is not null) candidateIds.Add(id);
            }
        }

        return new SearchIndexResults(positionIds, candidateIds);
    }

    /// <summary>
    /// Every word of the query must match (AND), each either exactly (boosted) or as a prefix, so
    /// typing "pyth" already finds "Python". Words go through the same analyzer as the index, so
    /// casing/stop-words behave identically on both sides; query syntax characters are never
    /// interpreted, which means no parse errors on input like "C#" or "a:b". Null if nothing searchable remains.
    /// </summary>
    public static Query? BuildTextQuery(Lucene.Net.Analysis.Analyzer analyzer, string queryText)
    {
        var terms = new List<string>();
        using (var stream = analyzer.GetTokenStream("text", queryText))
        {
            var termAttribute = stream.AddAttribute<Lucene.Net.Analysis.TokenAttributes.ICharTermAttribute>();
            stream.Reset();
            while (stream.IncrementToken())
            {
                terms.Add(termAttribute.ToString());
            }
            stream.End();
        }
        if (terms.Count == 0) return null;

        var query = new BooleanQuery();
        foreach (var term in terms.Distinct())
        {
            query.Add(new BooleanQuery
            {
                { new TermQuery(new Term("text", term)) { Boost = 2f }, Occur.SHOULD },
                { new PrefixQuery(new Term("text", term)), Occur.SHOULD }
            }, Occur.MUST);
        }
        return query;
    }

    private static Document BuildPositionDocument(int id, string title, string? shortDescription, string? company)
    {
        var text = string.Join(' ', title, shortDescription, company);
        var doc = new Document
        {
            new StringField("key", PositionKey(id), Field.Store.NO),
            new StringField("doctype", "position", Field.Store.NO),
            new StoredField("id", id.ToString()),
            new TextField("text", text, Field.Store.NO)
        };
        return doc;
    }

    /// <summary>
    /// Builds candidate documents for the given users (null = every user) with three queries in total,
    /// however many users are involved -- the startup rebuild must not issue queries per user.
    /// </summary>
    private static async Task<List<Document>> BuildCandidateDocumentsAsync(
        ApplicationDbContext db, IReadOnlyCollection<string>? userIds, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking().AsQueryable();
        var values = db.UserAttributeValues.AsNoTracking().Where(v => v.ValueString != null || v.ValueText != null);
        var projects = db.Projects.AsNoTracking();
        if (userIds is not null)
        {
            users = users.Where(u => userIds.Contains(u.Id));
            values = values.Where(v => userIds.Contains(v.UserId));
            projects = projects.Where(p => userIds.Contains(p.UserId));
        }

        var displayNames = await users.Select(u => new { u.Id, u.DisplayName }).ToListAsync(ct);
        var attributeText = (await values
                .Select(v => new { v.UserId, Text = (v.ValueString ?? string.Empty) + " " + (v.ValueText ?? string.Empty) })
                .ToListAsync(ct))
            .ToLookup(v => v.UserId, v => v.Text);
        var projectText = (await projects
                .Select(p => new { p.UserId, Text = p.Name + " " + p.DescriptionMarkdown })
                .ToListAsync(ct))
            .ToLookup(p => p.UserId, p => p.Text);

        return displayNames.Select(u => new Document
        {
            new StringField("key", CandidateKey(u.Id), Field.Store.NO),
            new StringField("doctype", "candidate", Field.Store.NO),
            new StoredField("id", u.Id),
            new TextField("text", string.Join(' ', new[] { u.DisplayName }.Concat(attributeText[u.Id]).Concat(projectText[u.Id])), Field.Store.NO)
        }).ToList();
    }

    private static string PositionKey(int id) => $"position:{id}";
    private static string CandidateKey(string userId) => $"candidate:{userId}";

    public void Dispose()
    {
        _writer.Dispose();
        _directory.Dispose();
        _analyzer.Dispose();
    }
}
