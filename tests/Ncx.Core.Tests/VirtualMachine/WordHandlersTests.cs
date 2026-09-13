using Ncx.Core.Catalog;
using Ncx.Core.VirtualMachine;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The table of word handlers (code-guidelines 5, table-driven dispatch; virtual machine 3 step 3): every state word of
/// the catalog has a handler, and no other word has one.
/// </summary>
public sealed class WordHandlersTests
{
    // VM 3 step 3: every modal word of the catalog sets state and has a handler.
    [Fact]
    public void EveryModalWordOfTheCatalog_HasAHandler()
    {
        var missing = new List<string>();
        foreach (WordDefinition definition in WordCatalog.All())
        {
            if (definition.Scope == Scope.Modal && WordHandlers.Find(definition.Key) is null)
            {
                missing.Add(definition.Key);
            }
        }

        Assert.Empty(missing);
    }

    // VM 2.1, 2.4, 2.5: FRAME, ORIENT and FUNC are words of their block with a state row.
    [Fact]
    public void BlockWordsWithAStateRow_HaveAHandler()
    {
        Assert.NotNull(WordHandlers.Find("FRAME"));
        Assert.NotNull(WordHandlers.Find("ORIENT"));
        Assert.NotNull(WordHandlers.Find("FUNC"));
    }

    // VM 3: verbs are steps 4 and 5, axis words belong to their verb, flow and channel words are step 6.
    [Fact]
    public void VerbsAxisFlowAndChannelWords_HaveNoHandler()
    {
        string[] keys = ["RAPID", "LINE", "HOME", "SETPOS", "CYCLE_CALL", "X", "CENTER", "JUMP", "CALL", "SYNC"];
        foreach (string key in keys)
        {
            Assert.Null(WordHandlers.Find(key));
        }
    }
}
