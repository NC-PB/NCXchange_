using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.Writing;

namespace Ncx.Readers.Tests.Fakes;

/// <summary>
/// Reads a snippet of the fake syntax with the fake reader against a small Fanuc machine, and writes the result the
/// way ncx convert and ncx format write it.
/// </summary>
internal static class FakeRead
{
    /// <summary>
    /// A Fanuc mill of the builder "nakamura" with the standard coolant M8/M9 and the builder code G411.
    /// </summary>
    public const string MachineToml = """
        [machine]
        name = "Fake mill"
        controller = "fanuc"
        builder = "nakamura"

        [coolant]
        STANDARD = { ON = "M8", OFF = "M9" }

        [raw]
        known = ["G411"]
        """;

    public static MachineConfig Machine()
    {
        var diagnostics = new Diagnostics("fake.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(MachineToml, diagnostics);
        Assert.True(diagnostics.Items.Count == 0, diagnostics.ToText());
        return machine!;
    }

    public static NcxProgram Program(string source, params ISourceRule[] rules)
    {
        return new FakeReader().Read(new SourceFile("fake.nc", source), Machine(), new ReadOptions { Rules = rules });
    }

    /// <summary>
    /// The canonical text of what the reader read.
    /// </summary>
    public static string Text(string source, params ISourceRule[] rules)
    {
        return NcxWriter.Write(Program(source, rules));
    }

    /// <summary>
    /// The codes of the diagnostics in the order they were reported.
    /// </summary>
    public static List<string> Codes(NcxProgram program)
    {
        return Codes(program.Diagnostics);
    }

    /// <summary>
    /// The codes of the diagnostics in the order they were reported.
    /// </summary>
    public static List<string> Codes(Diagnostics diagnostics)
    {
        var codes = new List<string>();
        foreach (Diagnostic diagnostic in diagnostics.Items)
        {
            codes.Add(diagnostic.Code);
        }

        return codes;
    }

    /// <summary>
    /// What ncx check finds in the text a reader wrote without a machine file: the diagnostics of a STATIC run of the
    /// parsed text on the default machine of D103, whose axes the fake machine does not list; a reader produces a
    /// program that check can run (code-guidelines 4, virtual machine 1).
    /// </summary>
    public static Diagnostics Check(string text)
    {
        MachineConfig machine = DefaultMachine.Create();
        NcxProgram parsed = Parser.Parse(text, "read.ncx", new ParserOptions());
        Assert.True(parsed.Diagnostics.Items.Count == 0, parsed.Diagnostics.ToText());
        var diagnostics = new Diagnostics("read.ncx");
        new VirtualMachine(machine, VmOptions.ForMachine(machine), diagnostics).Run(parsed);
        return diagnostics;
    }

    /// <summary>
    /// Lines joined with LF, the last one ended too, as the canonical writer ends a program a reader built.
    /// </summary>
    public static string Lines(params string[] lines)
    {
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// A block with its comment in column 57 (language 5 rule 7).
    /// </summary>
    public static string Commented(string words, string comment)
    {
        return words.PadRight(56) + comment;
    }

    /// <summary>
    /// Asserts that the text parses back without a diagnostic and formats to itself: what ncx format does to the
    /// output of ncx convert (architecture 4.1, D91).
    /// </summary>
    public static void AssertFormatsToItself(string text)
    {
        NcxProgram parsed = Parser.Parse(text, "read.ncx", new ParserOptions());
        Assert.True(parsed.Diagnostics.Items.Count == 0, parsed.Diagnostics.ToText());
        Assert.Equal(text, NcxWriter.Write(parsed));
    }
}
