using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.Catalog;

/// <summary>
/// docs/spec/generated/word-catalog.md is written from the catalog by this test and committed, so that the catalog and
/// the specification can be compared by eye and every change of the catalog shows in the diff (D90, implementation
/// 00-method 4).
/// </summary>
public sealed class WordCatalogTableTests
{
    // The test rewrites the file when it differs and fails, so the next run passes once the new table is committed.
    [Fact]
    public void WordCatalogTable_CommittedFile_EqualsTheCatalog()
    {
        string path = Path.Combine(Fixture.RepositoryRoot(), "docs", "spec", "generated", "word-catalog.md");
        string table = WordCatalogTable.Write();
        string committed = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : "";
        if (committed != table)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, table);
        }

        Assert.Equal(table, committed);
    }
}
