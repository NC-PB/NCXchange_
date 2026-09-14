using Ncx.Config;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.Writing;
using Ncx.Readers.Heidenhain;
using Ncx.Tests.Fixtures;

namespace Ncx.Readers.Tests.Heidenhain;

/// <summary>
/// Reads Klartext snippets with the Heidenhain reader against the iTNC 530 of the repository,
/// machines/heidenhain-itnc530.toml with the Heidenhain cycle catalog of cycles/, and writes the result the way ncx
/// convert writes it.
/// </summary>
internal static class HeidenhainRead
{
    private static readonly Lazy<MachineConfig> s_mill = new(() => Load("machines/heidenhain-itnc530.toml"));

    /// <summary>
    /// The iTNC 530 vertical mill of machines/, with the Heidenhain cycle catalog.
    /// </summary>
    public static MachineConfig Mill()
    {
        return s_mill.Value;
    }

    /// <summary>
    /// A machine file of the repository loaded by its path, with the Heidenhain cycle catalog (machine-config 6).
    /// </summary>
    /// <param name="relativePath">The path from the repository root.</param>
    public static MachineConfig Load(string relativePath)
    {
        var diagnostics = new Diagnostics(relativePath);
        MachineConfig? machine = MachineConfigLoader.Load(Path.Combine(Fixture.RepositoryRoot(), relativePath),
            diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        CycleCatalog? catalog = CycleCatalogLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "cycles", "heidenhain.toml"), Controller.Heidenhain, diagnostics);
        Assert.True(catalog is not null, diagnostics.ToText());
        return CycleCatalogLoader.WithCatalog(machine, catalog);
    }

    /// <summary>
    /// Reads a Klartext source.
    /// </summary>
    public static NcxProgram Program(string source, MachineConfig? machine = null)
    {
        return new HeidenhainReader().Read(new SourceFile("TEST.h", source), machine ?? Mill(), new ReadOptions());
    }

    /// <summary>
    /// The canonical text of a Klartext source.
    /// </summary>
    public static string Text(string source, MachineConfig? machine = null)
    {
        return NcxWriter.Write(Program(source, machine));
    }

    /// <summary>
    /// The program of a snippet framed with BEGIN PGM, M30 and END PGM.
    /// </summary>
    public static NcxProgram FramedProgram(string snippet, MachineConfig? machine = null)
    {
        return Program(Frame(snippet), machine);
    }

    /// <summary>
    /// The blocks a snippet reads into, between the header after PROGRAM=BEGIN and PROGRAM=END, each line ended with
    /// LF; the snippet is framed with BEGIN PGM, M30 and END PGM.
    /// </summary>
    public static string Body(string snippet, MachineConfig? machine = null)
    {
        return BodyOf(Text(Frame(snippet), machine));
    }

    /// <summary>
    /// The lines between the header after PROGRAM=BEGIN and PROGRAM=END of a canonical text.
    /// </summary>
    public static string BodyOf(string text)
    {
        List<string> lines = [.. text.Split('\n')];
        int begin = lines.FindIndex(line => line.StartsWith("PROGRAM=BEGIN", StringComparison.Ordinal));
        int end = lines.FindIndex(line => line.StartsWith("PROGRAM=END", StringComparison.Ordinal));
        return Lines([.. lines.GetRange(begin + 2, end - begin - 2)]);
    }

    /// <summary>
    /// Lines joined with LF, the last one ended too.
    /// </summary>
    public static string Lines(params string[] lines)
    {
        return lines.Length == 0 ? "" : string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// A block with its comment in column 57 (language 5 rule 7).
    /// </summary>
    public static string Commented(string words, string comment)
    {
        return words.PadRight(56) + comment;
    }

    /// <summary>
    /// The codes of the diagnostics in the order they were reported.
    /// </summary>
    public static List<string> Codes(NcxProgram program)
    {
        var codes = new List<string>();
        foreach (Diagnostic diagnostic in program.Diagnostics.Items)
        {
            codes.Add(diagnostic.Code);
        }

        return codes;
    }

    /// <summary>
    /// What ncx check finds in the text the reader wrote: the diagnostics of a STATIC run of the parsed text on the
    /// machine; a reader produces a program that check can run (code-guidelines 4, virtual machine 1).
    /// </summary>
    public static Diagnostics Check(string text, MachineConfig? machine = null)
    {
        NcxProgram parsed = Parser.Parse(text, "read.ncx", new ParserOptions());
        Assert.True(parsed.Diagnostics.Items.Count == 0, parsed.Diagnostics.ToText());
        MachineConfig mill = machine ?? Mill();
        var diagnostics = new Diagnostics("read.ncx");
        new VirtualMachine(mill, VmOptions.ForMachine(mill), diagnostics).Run(parsed);
        return diagnostics;
    }

    /// <summary>
    /// Asserts that the text parses back without a diagnostic and formats to itself, what ncx format does to the output
    /// of ncx convert (D91).
    /// </summary>
    public static void AssertFormatsToItself(string text)
    {
        NcxProgram parsed = Parser.Parse(text, "read.ncx", new ParserOptions());
        Assert.True(parsed.Diagnostics.Items.Count == 0, parsed.Diagnostics.ToText());
        Assert.Equal(text, NcxWriter.Write(parsed));
    }

    private static string Frame(string snippet)
    {
        return "0 BEGIN PGM TEST MM\n" + snippet + "\n9998 M30\n9999 END PGM TEST MM\n";
    }
}
