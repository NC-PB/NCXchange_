using Ncx.Core.Model;

namespace Ncx.Core.Expander;

/// <summary>
/// The program rewriters of the plugins in the expander (architecture 5.5, 9; code-guidelines 11): each is asked about
/// every block of the program once, and its answer is applied.
/// </summary>
internal static class ProgramRewriters
{
    /// <summary>
    /// Asks one rewriter about the block as the rules and the rewriters before it left the block, and applies its
    /// answer: Replace puts the rewritten block in place of the block, Surround puts generated blocks around it inside
    /// those already there (architecture 5.5; code-guidelines 5, chain of rewriters).
    /// </summary>
    public static void Apply(IProgramRewriter rewriter, BlockExpansion expansion, RewriteContext context,
        GeneratedText generated)
    {
        RewriteResult result = rewriter.Rewrite(expansion.Block, context);
        string name = rewriter.GetType().Name;
        switch (result.Kind)
        {
            case RewriteKind.Replace:
                Replace(result, name, expansion, generated);
                break;
            case RewriteKind.Surround:
                Surround(result, name, expansion, generated);
                break;
        }
    }

    // Replace: a rewriter changes the words of the block, and the virtual machine executes the changed block (architecture
    // 9). The new block stands in place of the block of the file and names it as its origin, so that ncx format still
    // writes the block as read (language 4.15).
    private static void Replace(RewriteResult result, string name, BlockExpansion expansion, GeneratedText generated)
    {
        GeneratedBlock origin = GeneratedText.Rewritten(expansion.Block, expansion.Origin, name, result.Reason);
        if (generated.FromRewriter(result.Replacement ?? "", name, origin) is not Block rewritten)
        {
            return;
        }

        if (!expansion.Replace(rewritten))
        {
            generated.Error(expansion.Origin, DiagnosticCodes.GeneratedBlockOutsideSection,
                $"{name} rewrites a block that opens or closes the file, a program or a subprogram; those stand as "
                + "written (language 4.1, 4.13).");
        }
    }

    // Surround: generated blocks before and after the block, which stays (architecture 9).
    private static void Surround(RewriteResult result, string name, BlockExpansion expansion, GeneratedText generated)
    {
        List<Block> before = Blocks(result.Before, name, result.Reason, GeneratedPlacement.Before, expansion, generated);
        List<Block> after = Blocks(result.After, name, result.Reason, GeneratedPlacement.After, expansion, generated);
        if (!expansion.Surround(before, after))
        {
            generated.Error(expansion.Origin, DiagnosticCodes.GeneratedBlockOutsideSection,
                $"The blocks of {name} would stand before a BEGIN or after an END block, outside every program and "
                + "subprogram (language 4.13).");
        }
    }

    // The generated blocks of the texts of one side, each text one block.
    private static List<Block> Blocks(IReadOnlyList<string> texts, string name, string reason,
        GeneratedPlacement placement, BlockExpansion expansion, GeneratedText generated)
    {
        var blocks = new List<Block>();
        foreach (string text in texts)
        {
            var origin = new GeneratedBlock
            {
                Origin = expansion.Origin,
                Source = name,
                Reason = reason,
                Placement = placement,
            };
            if (generated.FromRewriter(text, name, origin) is Block block)
            {
                blocks.Add(block);
            }
        }

        return blocks;
    }
}
