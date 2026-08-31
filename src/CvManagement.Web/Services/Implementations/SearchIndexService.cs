using CvManagement.Web.Data;
using CvManagement.Web.Services.Abstractions;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
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

        var userIds = await db.Users.AsNoTracking().Select(u => u.Id).ToListAsync(ct);
        foreach (var userId in userIds)
        {
            var doc = await BuildCandidateDocumentAsync(db, userId, ct);
            if (doc is not null) _writer.AddDocument(doc);
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

        var doc = await BuildCandidateDocumentAsync(db, userId, ct);
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

        var parser = new QueryParser(Version, "text", _analyzer) { AllowLeadingWildcard = false };
        // A bare term search should behave like "contains this word anywhere" -- wrap as a prefix-ish
        // OR of the raw term and a wildcard so short/partial queries still match sensibly.
        Query textQuery;
        try
        {
            textQuery = parser.Parse(QueryParserBase.Escape(queryText));
        }
        catch (ParseException)
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

    private async Task<Document?> BuildCandidateDocumentAsync(ApplicationDbContext db, string userId, CancellationToken ct)
    {
        var displayName = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
        if (displayName is null) return null; // user no longer exists

        var attributeText = await db.UserAttributeValues.AsNoTracking()
            .Where(v => v.UserId == userId && (v.ValueString != null || v.ValueText != null))
            .Select(v => (v.ValueString ?? string.Empty) + " " + (v.ValueText ?? string.Empty))
            .ToListAsync(ct);

        var projectText = await db.Projects.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.Name + " " + p.DescriptionMarkdown)
            .ToListAsync(ct);

        var text = string.Join(' ', new[] { displayName }.Concat(attributeText).Concat(projectText));

        return new Document
        {
            new StringField("key", CandidateKey(userId), Field.Store.NO),
            new StringField("doctype", "candidate", Field.Store.NO),
            new StoredField("id", userId),
            new TextField("text", text, Field.Store.NO)
        };
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
