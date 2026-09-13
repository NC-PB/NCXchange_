using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Tests.VirtualMachine;
using Ncx.Core.VirtualMachine;
using Ncx.Core.Writing;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// The five examples of the specification through the expander: on the built-in default machine of D103 there is no
/// rule to apply and the expanded program is the program; with a rule on the tool change the generated blocks run with
/// the example, and ncx format still writes the example as read (architecture 5.5, language 4.15).
/// </summary>
public sealed class ExampleExpansionTests
{
    // Without a machine file there is no expansion rule and no rewriter: every block stays the block it was
    // (architecture 5.5, D103).
    [Theory]
    [InlineData("2.5D_FRAESEN.ncx")]
    [InlineData("PATTERN_LOOP.ncx")]
    [InlineData("INCREMENTAL_SUB.ncx")]
    [InlineData("MILLTURN_TRANSFER.ncx")]
    [InlineData("POLAR_FACE.ncx")]
    public void Architecture55_ExampleOnTheDefaultMachine_IsExpandedToItself(string example)
    {
        NcxProgram original = Parser.Parse(Fixture.ReadText(example), example, new ParserOptions());

        NcxProgram expanded = Ncx.Core.Expander.Expander.Expand(original, VmMachines.Default(), []);

        Assert.Equal(original.Blocks, expanded.Blocks);
        Assert.Equal(original.Diagnostics.Items, expanded.Diagnostics.Items);
    }

    // With [tool_change] pre = ["HOME Z"] every tool change of the example gets its HOME Z, the run gives no ERROR, and
    // ncx format writes the example as read (machine-config 5a, language 4.15).
    [Theory]
    [InlineData("2.5D_FRAESEN.ncx")]
    [InlineData("INCREMENTAL_SUB.ncx")]
    [InlineData("MILLTURN_TRANSFER.ncx")]
    public void MachineConfig5a_ExampleWithAToolChangeRule_RunsAndFormatsAsRead(string example)
    {
        MachineConfig machine = VmMachines.Default() with
        {
            ToolChange = new ToolChangeConfig { Rule = new ExpansionRule { Pre = ["HOME Z"] } },
        };
        string text = Fixture.ReadText(example);
        NcxProgram original = Parser.Parse(text, example, new ParserOptions());

        NcxProgram expanded = Ncx.Core.Expander.Expander.Expand(original, machine, []);

        Assert.Equal(NcxWriter.Write(original), NcxWriter.Write(expanded));
        Assert.Equal(ToolBlocks(original), GeneratedHomes(expanded));
        Diagnostics diagnostics = ExpanderHarness.Run(expanded, machine, out RunResult result);
        Assert.False(result.Stopped, diagnostics.ToText());
        Assert.False(diagnostics.HasErrors, diagnostics.ToText());
    }

    // The number of blocks with a TOOL word.
    private static int ToolBlocks(NcxProgram program)
    {
        int count = 0;
        foreach (Block block in program.Blocks)
        {
            if (block.Has("TOOL"))
            {
                count++;
            }
        }

        return count;
    }

    // The number of generated HOME Z blocks.
    private static int GeneratedHomes(NcxProgram program)
    {
        int count = 0;
        foreach (Block block in program.Blocks)
        {
            if (block.IsGenerated && NcxWriter.WriteBlock(block).EndsWith("HOME Z", StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
