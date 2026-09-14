using Ncx.Core.Expander;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;
using Ncx.Core.Writing;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// The expander for the tests: a file parsed as a user file and expanded against a machine, written with its generated
/// blocks, and executed by the virtual machine block by block or as a STATIC run.
/// </summary>
internal static class ExpanderHarness
{
    /// <summary>
    /// A file of one program named TEST with these lines between PROGRAM=BEGIN and PROGRAM=END: line 3 is the first of
    /// them.
    /// </summary>
    public static string File(params string[] lines)
    {
        return "FILE=BEGIN NCX=1\nPROGRAM=BEGIN NAME=\"TEST\"\n" + string.Join("\n", lines)
            + "\nPROGRAM=END\nFILE=END\n";
    }

    public static NcxProgram Parse(string text)
    {
        return Parser.Parse(text, "test.ncx", new ParserOptions());
    }

    /// <summary>
    /// Parses a file and expands it against a machine with these rewriters.
    /// </summary>
    public static NcxProgram Expand(string text, MachineConfig machine, params IProgramRewriter[] rewriters)
    {
        return Ncx.Core.Expander.Expander.Expand(Parse(text), machine, rewriters);
    }

    /// <summary>
    /// The program in canonical form with its generated blocks (language 4.15).
    /// </summary>
    public static string WriteWithGenerated(NcxProgram program)
    {
        return NcxWriter.Write(program, new WriterOptions { IncludeGenerated = true });
    }

    /// <summary>
    /// The first block whose canonical text is this.
    /// </summary>
    public static Block Find(NcxProgram program, string blockText)
    {
        foreach (Block block in program.Blocks)
        {
            if (NcxWriter.WriteBlock(block) == blockText)
            {
                return block;
            }
        }

        throw new InvalidOperationException($"The program has no block {blockText}.");
    }

    /// <summary>
    /// Runs a program STATIC and returns the diagnostics of the run, the parser's and the expander's among them.
    /// </summary>
    public static Diagnostics Run(NcxProgram program, MachineConfig machine, out RunResult result)
    {
        var diagnostics = new Diagnostics(program.FileName);
        foreach (Diagnostic diagnostic in program.Diagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        var vm = new Ncx.Core.VirtualMachine.VirtualMachine(machine, VmOptions.ForMachine(machine), diagnostics);
        result = vm.Run(program);
        return diagnostics;
    }

    /// <summary>
    /// Executes the blocks of a program in file order up to and with the first block whose canonical text is this, and
    /// returns the state of the channel after it.
    /// </summary>
    public static ChannelSnapshot StateAfter(NcxProgram program, MachineConfig machine, string blockText)
    {
        var vm = new Ncx.Core.VirtualMachine.VirtualMachine(machine, VmOptions.ForMachine(machine),
            new Diagnostics(program.FileName));
        foreach (Block block in program.Blocks)
        {
            vm.Execute(block);
            if (NcxWriter.WriteBlock(block) == blockText)
            {
                return vm.Snapshot();
            }
        }

        throw new InvalidOperationException($"The program has no block {blockText}.");
    }

    /// <summary>
    /// Executes the blocks of a program in file order up to PROGRAM=END, which resets the spindle and the coolant
    /// (virtual machine 4), and returns the state of the channel before it.
    /// </summary>
    public static ChannelSnapshot StateBeforeProgramEnd(NcxProgram program, MachineConfig machine)
    {
        var vm = new Ncx.Core.VirtualMachine.VirtualMachine(machine, VmOptions.ForMachine(machine),
            new Diagnostics(program.FileName));
        foreach (Block block in program.Blocks)
        {
            if (block.Has("PROGRAM", null, "END"))
            {
                return vm.Snapshot();
            }

            vm.Execute(block);
        }

        throw new InvalidOperationException("The program has no PROGRAM=END.");
    }

    /// <summary>
    /// The diagnostics with this code.
    /// </summary>
    public static List<Diagnostic> WithCode(Diagnostics diagnostics, string code)
    {
        var found = new List<Diagnostic>();
        foreach (Diagnostic diagnostic in diagnostics.Items)
        {
            if (diagnostic.Code == code)
            {
                found.Add(diagnostic);
            }
        }

        return found;
    }
}
