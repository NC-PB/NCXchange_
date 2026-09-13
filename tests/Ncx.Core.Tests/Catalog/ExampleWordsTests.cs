using Ncx.Core.Catalog;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.Catalog;

/// <summary>
/// Every key and KEY:ADDR form of the five examples and of the examples of language 6 resolves in the word catalog
/// (phase 0, P0-03, done when).
/// </summary>
public sealed class ExampleWordsTests
{
    // The verb whose axis words stand bare (language 4.3, 5 rule 2; D93).
    private const string HomeKey = "HOME";

    [Fact]
    public void Catalog_EveryWordOfTheFiveExamples_Resolves()
    {
        var unresolved = new List<string>();
        foreach (string example in ExampleBlocks.NcxExamples())
        {
            AddUnresolved(unresolved, example, ExampleBlocks.Read(Fixture.ReadText(example)));
        }

        Assert.Equal(5, ExampleBlocks.NcxExamples().Count);
        Assert.Empty(unresolved);
    }

    [Fact]
    public void Catalog_EveryWordOfTheExamplesOfLanguage6_Resolves()
    {
        var unresolved = new List<string>();
        List<Block> blocks = ExampleBlocks.Read(LanguageDocument.ExamplesOfChapter6());
        AddUnresolved(unresolved, "language 6", blocks);

        Assert.NotEmpty(blocks);
        Assert.Empty(unresolved);
    }

    // The machine axis Z2 of MILLTURN_TRANSFER is no entry; it resolves by its form in a block whose verb takes axis
    // words (language 3, KEY; D93).
    [Fact]
    public void D93_Z2OfMillturnTransfer_ResolvesAsAMachineAxisWord()
    {
        Block block = Assert.Single(ExampleBlocks.Read("RAPID Z2=-58"));
        var diagnostics = new Diagnostics("test.ncx");

        Assert.Null(WordCatalog.Lookup("Z2"));
        Assert.True(Resolves(block.Words[1], block, diagnostics));
    }

    // Outside a block whose verb takes axis words, an axis-looking key the catalog does not know does not resolve
    // (language 5 rule 2, D93).
    [Fact]
    public void D93_MachineAxisWordWithoutAVerb_DoesNotResolve()
    {
        Block block = Assert.Single(ExampleBlocks.Read("Z2=-58 COOLANT=ON"));
        var diagnostics = new Diagnostics("test.ncx");

        Assert.False(Resolves(block.Words[0], block, diagnostics));
    }

    private static void AddUnresolved(List<string> unresolved, string source, List<Block> blocks)
    {
        var diagnostics = new Diagnostics(source);
        foreach (Block block in blocks)
        {
            foreach (Word word in block.Words)
            {
                if (!Resolves(word, block, diagnostics))
                {
                    unresolved.Add($"{source}({block.Line}): {word.ToCanonical()}");
                }
            }
        }

        foreach (Diagnostic diagnostic in diagnostics.Items)
        {
            unresolved.Add(diagnostic.ToText());
        }
    }

    // A word resolves when the catalog lists its key and its entry accepts the address and the value (a user word,
    // never an internal one), or when the catalog takes it by one of its two rules for the keys it does not list: a
    // native parameter with a number or expression value in a block that carries CYCLE:<controller>=n (D94), a machine
    // axis word with a number or expression value, bare under HOME, in a block whose verb takes axis words (D93).
    private static bool Resolves(Word word, Block block, Diagnostics diagnostics)
    {
        if (word.Definition is WordDefinition definition)
        {
            return !definition.IsInternal && WordCheck.Accepts(word, definition, block, diagnostics);
        }

        bool isNumberOrExpression = word.Value is IntegerValue or DecimalValue or ExprValue;
        if (WordCatalog.IsNativeParameterAllowed(block))
        {
            return isNumberOrExpression;
        }

        if (!WordCatalog.TryMachineAxis(word.Key, out _) || block.Verb?.Definition is not { TakesAxisWords: true })
        {
            return false;
        }

        return isNumberOrExpression || (word.Value is NoValue && block.Verb.Key == HomeKey);
    }
}
