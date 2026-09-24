using CvManagement.Web.Services.Implementations;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace CvManagement.Tests;

public class SearchQueryTests
{
    private static int CountHits(string indexedText, string query)
    {
        using var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
        using var directory = new RAMDirectory();
        using (var writer = new IndexWriter(directory, new IndexWriterConfig(LuceneVersion.LUCENE_48, analyzer)))
        {
            writer.AddDocument(new Document { new TextField("text", indexedText, Field.Store.NO) });
        }

        using var reader = DirectoryReader.Open(directory);
        var built = SearchIndexService.BuildTextQuery(analyzer, query);
        return built is null ? 0 : new IndexSearcher(reader).Search(built, 10).TotalHits;
    }

    [Fact]
    public void Partial_word_matches_by_prefix()
        => Assert.Equal(1, CountHits("Senior Python developer", "pyth"));

    [Fact]
    public void Every_word_must_match()
        => Assert.Equal(0, CountHits("Senior Python developer", "python kotlin"));

    [Fact]
    public void Query_syntax_characters_are_not_interpreted()
        => Assert.Equal(1, CountHits("Backend engineer: C#", "engineer: C#"));

    [Fact]
    public void Query_of_only_punctuation_yields_no_query()
    {
        using var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
        Assert.Null(SearchIndexService.BuildTextQuery(analyzer, "  ?! "));
    }
}
