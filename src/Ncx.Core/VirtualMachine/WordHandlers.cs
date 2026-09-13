using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Handlers;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The state words of the language and the handler that applies each to the channel state, by key: step 3 of virtual
/// machine 3 looks every word of a block up here (code-guidelines 5, table-driven dispatch; architecture 5.1). A new
/// state word is a catalog entry plus a handler registered in the class of its group, so the virtual machine does not
/// change for it (code-guidelines 4, open/closed). A word without a handler sets no state: a verb, an axis word, a flow
/// or a channel word, a word of its block only.
/// </summary>
internal static class WordHandlers
{
    private static readonly Dictionary<string, Action<Word, BlockContext>> s_handlers = Register();

    /// <summary>
    /// The handler of a key; null for a word that sets no state.
    /// </summary>
    /// <param name="key">The key of the word, SPINDLE.</param>
    public static Action<Word, BlockContext>? Find(string key)
    {
        return s_handlers.TryGetValue(key, out Action<Word, BlockContext>? handler) ? handler : null;
    }

    // One registration line per state word, in the class of its group (code-guidelines 5, registry).
    private static Dictionary<string, Action<Word, BlockContext>> Register()
    {
        var handlers = new Dictionary<string, Action<Word, BlockContext>>(StringComparer.Ordinal);
        FrameHandlers.Register(handlers);
        MotionHandlers.Register(handlers);
        ToolHandlers.Register(handlers);
        SpindleHandlers.Register(handlers);
        FunctionHandlers.Register(handlers);
        CycleHandlers.Register(handlers);
        VariableHandlers.Register(handlers);
        ResourceHandlers.Register(handlers);
        return handlers;
    }
}
