using System.Globalization;
using Ncx.Compilers.Fanuc;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Compilers.Tests.Jobs;

/// <summary>
/// The channel binding in a single-channel compile (virtual machine 3.8 rule 2a, D56): a word of a table the machine
/// accepts only from another channel, or needs in every channel program, is an ERROR of the compile of one file; the
/// program on the channel the table is bound to writes it.
/// </summary>
public sealed class ChannelBindingTests
{
    // Virtual machine 3.8 rule 2a, D56: SPINDLE_SYNC with [spindle_sync] channel = 2 in a program of channel 1, which
    // a program without CHANNEL runs on (language 4.1), is the ERROR of D56.
    [Fact]
    public void SingleChannelCompile_WordBoundToAnotherChannel_IsCmp700()
    {
        CompileResult result = Compile(JobCompile.Machine(), "SPINDLE_SYNC=MAIN,SUB");

        Diagnostic error = ErrorOf(result);
        Assert.Equal(DiagnosticCodes.WordBoundToAnotherChannel, error.Code);
        Assert.Equal(4, error.Line);
        Assert.Contains("only from channel 2", error.Message, StringComparison.Ordinal);
    }

    // Virtual machine 3.8 rule 2a: the program of the channel the table is bound to writes the word.
    [Fact]
    public void SingleChannelCompile_ProgramOnTheBoundChannel_WritesTheWord()
    {
        CompileResult result = Compile(JobCompile.Machine(), "SPINDLE_SYNC=MAIN,SUB", channel: 2);

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.Contains("\nM96\n", Assert.Single(result.Files).Text, StringComparison.Ordinal);
    }

    // Machine-config 5, D56: a named function bound to channel 1, the door of the Biglia, in a program of channel 2.
    [Fact]
    public void SingleChannelCompile_FunctionBoundToAnotherChannel_IsCmp700()
    {
        CompileResult result = Compile(JobCompile.Machine(), "FUNC:DOOR=OPEN", channel: 2);

        Assert.Equal(DiagnosticCodes.WordBoundToAnotherChannel, ErrorOf(result).Code);
    }

    // D56 and the TODO(question) of ChannelBinding.Check: a word bound to every channel, M34/M35 of the Mori Seiki,
    // cannot stand in every channel program behind a wait in a single-channel compile of a machine of two channels.
    [Fact]
    public void SingleChannelCompile_WordBoundToEveryChannel_IsCmp701()
    {
        CompileResult result = Compile(JobCompile.Machine(spindleSync: "channels = \"all\""), "SPINDLE_SYNC=MAIN,SUB");

        Assert.Equal(DiagnosticCodes.WordBoundToEveryChannel, ErrorOf(result).Code);
    }

    // Virtual machine 3.8 rule 2a: every function the compiler writes is bound, also a word an expansion rule
    // generates: FUNC:DOOR=OPEN of [tool_change] pre in a program of channel 2.
    [Fact]
    public void SingleChannelCompile_WordAnExpansionRuleGeneratesForAnotherChannel_IsCmp700()
    {
        CompileResult result = Compile(JobCompile.Machine(toolChange: "pre = [\"FUNC:DOOR=OPEN\"]"), "TOOL=2 OFFSET=2",
            channel: 2);

        Diagnostic error = ErrorOf(result);
        Assert.Equal(DiagnosticCodes.WordBoundToAnotherChannel, error.Code);
        Assert.Equal(4, error.OriginLine);
    }

    // D56: a table without a channel binding is written from every channel.
    [Fact]
    public void SingleChannelCompile_WordOfAnUnboundTable_IsWritten()
    {
        CompileResult result = Compile(JobCompile.Machine(spindleSync: ""), "SPINDLE_SYNC=MAIN,SUB");

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.Contains("\nM96\n", Assert.Single(result.Files).Text, StringComparison.Ordinal);
    }

    // Compiles one file with one program, on its CHANNEL when one is given, for the twin-turret lathe.
    private static CompileResult Compile(string machine, string block, int? channel = null)
    {
        string text = JobCompile.Program(1000, block);
        if (channel is int number)
        {
            string header = "NUMBER=1000 CHANNEL=" + number.ToString(CultureInfo.InvariantCulture);
            text = text.Replace("NUMBER=1000", header, StringComparison.Ordinal);
        }

        NcxProgram program = Parser.Parse(text, "T.ncx", new ParserOptions());
        return new FanucCompiler().Compile(program, JobCompile.MachineOf(machine), new CompileOptions());
    }

    private static Diagnostic ErrorOf(CompileResult result)
    {
        Assert.Empty(result.Files);
        return Assert.Single(result.Diagnostics.Items, diagnostic => diagnostic.Severity == Severity.Error);
    }
}
