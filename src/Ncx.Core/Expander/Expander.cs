using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Core.Expander;

/// <summary>
/// The stage between the parser and the virtual machine (virtual machine 1, architecture 5.5, D63). It applies the
/// expansion rules of the machine configuration and the program rewriters of the plugins to every block and returns a
/// new program: the generated NCX blocks stand before and after the block they were generated for, and a block whose
/// words a rewriter or the limits = "clamp" policy rewrote stands in its place (D64). Everything it produces is NCX,
/// so the virtual machine executes it like the rest; it never sees VM state, and where a rule needs the previous value
/// of something it writes @SAVE and @RESTORE and the virtual machine does the remembering (3.10). ncx format does not
/// run it (D91).
/// </summary>
public static class Expander
{
    /// <summary>
    /// Expands a parsed program for a machine.
    /// </summary>
    /// <param name="program">The parsed program; it stays as it is (code-guidelines 7).</param>
    /// <param name="machine">The machine file, or the built-in default machine of D103, which has no rules.</param>
    /// <param name="rewriters">The program rewriters of the plugins in their order; empty without plugins.</param>
    /// <returns>A new program with the generated blocks in place, every section around the same blocks, and the
    /// diagnostics of the parser followed by those of the expander.</returns>
    public static NcxProgram Expand(NcxProgram program, MachineConfig machine,
        IReadOnlyList<IProgramRewriter> rewriters)
    {
        // The new program reports what the parser found and what the expander finds (D98).
        var diagnostics = new Diagnostics(program.FileName);
        foreach (Diagnostic diagnostic in program.Diagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        // An ERROR stops the run before the first block (virtual machine 2.9): a program with an ERROR of the parser, a
        // pseudo-word in a user file among them (D95), is not expanded.
        if (program.Diagnostics.HasErrors)
        {
            return program with { Diagnostics = diagnostics };
        }

        // The expander runs once over the parsed program, block by block, before the virtual machine (architecture
        // 5.5).
        // TODO: in INTERPRETED mode (P4-01) a JUMP or a REPEAT to a LABEL enters its block after the blocks generated
        // before it; the pre-pass of the labels must map the label to the first of them.
        var generated = new GeneratedText(machine, diagnostics);
        var blocks = new List<Block>();
        var newIndex = new int[program.Blocks.Count];
        for (int index = 0; index < program.Blocks.Count; index++)
        {
            BlockExpansion expansion = ExpandBlock(program, index, machine, rewriters, generated, diagnostics);
            blocks.AddRange(expansion.Before);
            newIndex[index] = blocks.Count;
            blocks.Add(expansion.Block);
            blocks.AddRange(expansion.After);
        }

        // Every program and subprogram keeps its BEGIN and its END block, and a generated block stands inside the
        // section of the block it was generated for (language 4.13).
        var sections = new List<Section>();
        foreach (Section section in program.Sections)
        {
            sections.Add(section with
            {
                FirstBlock = newIndex[section.FirstBlock],
                LastBlock = newIndex[section.LastBlock],
            });
        }

        return program with { Blocks = blocks, Sections = sections, Diagnostics = diagnostics };
    }

    // For every block (architecture 5.5): the expansion rules it triggers, the outermost first; the rewriters in their
    // order, each asked about the block as the one before left it, their blocks inside those of the rules; then
    // limits = "clamp" over every block the virtual machine executes for it (D64). A block that triggers nothing stays
    // the same block.
    private static BlockExpansion ExpandBlock(NcxProgram program, int index, MachineConfig machine,
        IReadOnlyList<IProgramRewriter> rewriters, GeneratedText generated, Diagnostics diagnostics)
    {
        Block block = program.Blocks[index];
        Section? section = SectionOf(program, index);
        var expansion = new BlockExpansion(block, section, index);
        foreach (TriggeredRule rule in ExpansionRules.Of(block, machine))
        {
            RuleBlocks.Apply(rule, expansion, generated, machine);
        }

        // A rewriter learns the machine name, the channel and the line, and nothing of the virtual machine (D61, D106);
        // the channel of FILE=BEGIN and FILE=END, which stand in no program, is the default channel 1 (virtual machine
        // 2.8).
        var context = new BlockRewriteContext(machine.Machine.Name, section?.Channel ?? 1, block.Line);
        foreach (IProgramRewriter rewriter in rewriters)
        {
            ProgramRewriters.Apply(rewriter, expansion, context, generated);
        }

        if (machine.Limits == LimitPolicy.Clamp)
        {
            LimitClamp.Apply(expansion, machine, diagnostics);
        }

        return expansion;
    }

    // The program or subprogram a block stands in; null for FILE=BEGIN and FILE=END.
    private static Section? SectionOf(NcxProgram program, int index)
    {
        foreach (Section section in program.Sections)
        {
            if (section.FirstBlock <= index && index <= section.LastBlock)
            {
                return section;
            }
        }

        return null;
    }
}
