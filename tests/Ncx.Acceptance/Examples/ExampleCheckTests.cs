using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The five examples of the specification checked in STATIC mode without a machine file, against the built-in default
/// machine of D103: no ERROR, which is the acceptance of phase 1, and the diagnostics of each compared as a whole file
/// with tests/Ncx.Acceptance/Expected/&lt;name&gt;.check.txt (P1-04). The check runs through the library, the parser
/// and the virtual machine; the command ncx check comes with P1-07.
/// </summary>
public sealed class ExampleCheckTests
{
    /// <summary>
    /// The five complete programs of language 6.
    /// </summary>
    public static TheoryData<string> Examples()
    {
        return ["2.5D_FRAESEN.ncx", "INCREMENTAL_SUB.ncx", "MILLTURN_TRANSFER.ncx", "PATTERN_LOOP.ncx",
            "POLAR_FACE.ncx"];
    }

    // D103, the phases table: the five examples check with no ERROR without a machine file.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Check_ExampleWithoutAMachineFile_RaisesNoError(string example)
    {
        Diagnostics diagnostics = Check(example);

        Assert.False(diagnostics.HasErrors, diagnostics.ToText());
    }

    // VM 5, D100, D103: the WARNINGs of each example are those the notes, D100, D103 and the validation list name,
    // compared as a whole file. On a difference the check is written next to a temporary copy for a diff.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Check_Example_GivesTheExpectedDiagnostics(string example)
    {
        string name = Path.GetFileNameWithoutExtension(example) + ".check.txt";
        string expectedPath = Path.Combine(Fixture.RepositoryRoot(), "tests", "Ncx.Acceptance", "Expected", name);
        string expected = File.ReadAllText(expectedPath).ReplaceLineEndings("\n");
        string actual = Check(example).ToText();
        if (actual != expected)
        {
            string folder = Path.Combine(Path.GetTempPath(), "ncx-acceptance");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, name), actual);
        }

        Assert.True(actual == expected,
            $"The check of {example} differs from {expectedPath} (actual in the temporary folder ncx-acceptance).\n"
            + $"Expected:\n{expected}\nActual:\n{actual}");
    }

    // The check of an example: the parser, then the STATIC run of the virtual machine against the default machine of
    // D103, and what both report, in the order they reported it (virtual machine 1, 2.9, D98).
    private static Diagnostics Check(string example)
    {
        NcxProgram program = Parser.Parse(Fixture.ReadText(example), example, new ParserOptions());
        MachineConfig machine = DefaultMachine.Create();
        var diagnostics = new Diagnostics(example);
        foreach (Diagnostic diagnostic in program.Diagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        var vm = new Ncx.Core.VirtualMachine.VirtualMachine(machine, VmOptions.ForMachine(machine), diagnostics);
        vm.Run(program);
        return diagnostics;
    }
}
