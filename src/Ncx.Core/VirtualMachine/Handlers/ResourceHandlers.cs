using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Handlers;

/// <summary>
/// The resource word WORKPIECE of language 4.10: the workpiece holder of virtual machine 2.1.
/// </summary>
internal static class ResourceHandlers
{
    /// <summary>
    /// Registers the resource word.
    /// </summary>
    public static void Register(Dictionary<string, Action<Word, BlockContext>> handlers)
    {
        handlers["WORKPIECE"] = ApplyWorkpiece;
    }

    // WORKPIECE=role: from here on the program machines the part held by this holder; ORIGIN, SHIFT and A B C refer to
    // it, and its coordinates are in its own frame (language 4.10, virtual machine 3.4, D57).
    private static void ApplyWorkpiece(Word word, BlockContext context)
    {
        if (context.ResourceOf.TryGetValue(word, out string? holder))
        {
            FrameRules.SelectWorkpiece(context.State, holder);
        }
    }
}
