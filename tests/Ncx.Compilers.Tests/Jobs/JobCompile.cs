using System.Globalization;
using Ncx.Compilers.Fanuc;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Compilers.Tests.Jobs;

/// <summary>
/// Compiles a job of NCX texts with the job compiler and the Fanuc compiler for a twin-turret lathe given as text:
/// G-code system A, the main spindle MAIN and the sub spindle SUB, turret 1 on channel 1 and turret 2 on channel 2, the
/// wait marks M100 to M199, [spindle_sync] with a channel binding of the test, and the door that only channel 1 may
/// command.
/// </summary>
internal static class JobCompile
{
    /// <summary>
    /// The wait marks of the lathe, M{mark} without paths (machine-config 5).
    /// </summary>
    public const string DefaultSync = "[sync]\nwait = \"M{mark}\"\nmark_range = [100, 199]";

    /// <summary>
    /// The twin-turret lathe.
    /// </summary>
    /// <param name="sync">The [sync] table.</param>
    /// <param name="spindleSync">The channel binding of [spindle_sync]: "channel = 2", "channels = \"all\"" or "".
    /// </param>
    /// <param name="channels">[machine] channels.</param>
    /// <param name="format">Further keys of [format].</param>
    /// <param name="toolChange">Further keys of [tool_change], an expansion rule (machine-config 5a).</param>
    /// <param name="functions">Further entries of [func].</param>
    public static string Machine(string sync = DefaultSync, string spindleSync = "channel = 2",
        string channels = "[1, 2]", string format = "", string toolChange = "", string functions = "")
    {
        return $$"""
            [machine]
            name = "Test twin turret"
            controller = "fanuc"
            gcode_system = "A"
            s_binds_to_spindle_word = true
            channels = {{channels}}
            units_default = "MM"
            default_spindle = "S1"
            default_holder = "T1"
            default_workpiece = "S1"

            [format]
            decimals = { X = 3, Z = 3, C = 3, F = 3, S = 0 }
            block_numbers = { enabled = false }
            line_ending = "LF"
            program_end = "M30"
            sub_end = "M99"
            {{format}}

            [tool_change]
            change = "T{tool:02}{offset:02}"
            {{toolChange}}

            [home]
            template = "G28 {axes}"

            [roles]
            MAIN = "S1"
            SUB = "S2"
            TURRET1 = "T1"
            TURRET2 = "T2"

            [[resource]]
            id = "S1"
            type = "work_spindle"
            axis = "C1"

            [[resource]]
            id = "S2"
            type = "work_spindle"
            axis = "C2"

            [[resource]]
            id = "T1"
            type = "tool_holder"
            channel = 1

            [[resource]]
            id = "T2"
            type = "tool_holder"
            channel = 2

            [[axis]]
            id = "X1"
            ncx = "X"
            letter = "X"
            incremental_letter = "U"
            programming = "diameter"
            kind = "linear"
            home = 0

            [[axis]]
            id = "Z1"
            ncx = "Z"
            letter = "Z"
            incremental_letter = "W"
            kind = "linear"
            home = 0

            [[axis]]
            id = "C1"
            ncx = "C"
            letter = "C"
            incremental_letter = "H"
            kind = "rotary"
            owner = "S1"

            [[axis]]
            id = "C2"
            ncx = "C2"
            letter = "C"
            incremental_letter = "H"
            kind = "rotary"
            owner = "S2"

            [spindle.MAIN]
            CW = "M3"
            CCW = "M4"
            OFF = "M5"
            RPM = "S{rpm}"

            [spindle.SUB]
            CW = "M53"
            CCW = "M54"
            OFF = "M55"
            RPM = "S{rpm}"

            [spindle_mode.SUB]
            AXIS = "M491"
            SPINDLE = "M441"

            [spindle_sync]
            ON = "M96"
            OFF = "M97"
            {{spindleSync}}

            [coolant]
            STANDARD = { ON = "M8", OFF = "M9" }

            [func]
            DOOR = { OPEN = "M62", CLOSE = "M63", channel = 1 }
            {{functions}}

            {{sync}}
            """;
    }

    /// <summary>
    /// A file of one program named "T" with the program number and the complete header of D34, and the given blocks.
    /// </summary>
    public static string Program(int number, params string[] blocks)
    {
        var lines = new List<string>
        {
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=" + number.ToString(CultureInfo.InvariantCulture),
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
        };
        lines.AddRange(blocks);
        lines.Add("PROGRAM=END");
        lines.Add("FILE=END");
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// Compiles a job whose channel n runs the one program of the n-th text, the file Cn.ncx, for a machine given as
    /// text.
    /// </summary>
    public static CompileResult Run(string machineToml, params string[] programs)
    {
        (JobManifest job, Dictionary<string, string> files) = JobOf(programs);
        return Run(machineToml, job, files);
    }

    /// <summary>
    /// Compiles a job manifest over files given as text by their file value, with the plugins of the options.
    /// </summary>
    public static CompileResult Run(string machineToml, JobManifest job, Dictionary<string, string> files,
        CompileOptions? options = null)
    {
        var parsed = new Dictionary<string, NcxProgram>();
        foreach (KeyValuePair<string, string> file in files)
        {
            parsed[file.Key] = Parser.Parse(file.Value, file.Key, new ParserOptions());
        }

        return new JobCompiler(new FanucCompiler()).Compile("job.ncxjob.toml", job, parsed, MachineOf(machineToml),
            options ?? new CompileOptions());
    }

    /// <summary>
    /// The manifest of a job whose channel n runs the one program of the file Cn.ncx, and those files.
    /// </summary>
    public static (JobManifest Job, Dictionary<string, string> Files) JobOf(params string[] programs)
    {
        var channels = new List<ChannelProgram>();
        var files = new Dictionary<string, string>();
        for (int index = 0; index < programs.Length; index++)
        {
            string file = "C" + (index + 1).ToString(CultureInfo.InvariantCulture) + ".ncx";
            channels.Add(new ChannelProgram { Id = index + 1, File = file });
            files[file] = programs[index];
        }

        return (new JobManifest { Name = "JOB", Machine = "test.toml", Channels = channels }, files);
    }

    /// <summary>
    /// A machine file given as text, which loads without an ERROR.
    /// </summary>
    public static MachineConfig MachineOf(string machineToml)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfig machine = MachineConfigLoader.LoadText(machineToml, diagnostics)
            ?? throw new InvalidOperationException("The test machine does not load:\n" + diagnostics.ToText());
        Assert.False(diagnostics.HasErrors, diagnostics.ToText());
        return machine;
    }

    /// <summary>
    /// The texts of the files of a job without an ERROR, one per channel.
    /// </summary>
    public static List<string> TextsOf(CompileResult result)
    {
        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        var texts = new List<string>();
        foreach (CompiledFile file in result.Files)
        {
            texts.Add(file.Text);
        }

        return texts;
    }

    /// <summary>
    /// The one ERROR of a job that stops it, with no file written.
    /// </summary>
    public static Diagnostic ErrorOf(CompileResult result)
    {
        Assert.Empty(result.Files);
        return Assert.Single(result.Diagnostics.Items, diagnostic => diagnostic.Severity == Severity.Error);
    }

    /// <summary>
    /// The codes of the diagnostics of a job, in their order.
    /// </summary>
    public static List<string> CodesOf(CompileResult result)
    {
        var codes = new List<string>();
        foreach (Diagnostic diagnostic in result.Diagnostics.Items)
        {
            codes.Add(diagnostic.Code);
        }

        return codes;
    }
}
