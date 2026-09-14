using Ncx.Core.Model;
using static Ncx.Readers.Tests.Heidenhain.HeidenhainRead;

namespace Ncx.Readers.Tests.Heidenhain;

/// <summary>
/// TOOL CALL and TOOL DEF (controllers heidenhain.md 4, 7 rule 2; controller-mapping 1 WORKPLANE, 3; language 4.4).
/// </summary>
public sealed class HeidenhainToolTests
{
    // TOOL CALL n Z S gives TOOL=n RPM= and both offset words with n (heidenhain 7 rule 2).
    [Fact]
    public void ToolCall_WithAxisAndSpeed_BecomesToolWithBothOffsetsAndRpm()
    {
        Assert.Equal(Lines("TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1 RPM=1592"), Body("5 TOOL CALL 1 Z S1592"));
    }

    // A changed axis letter gives WORKPLANE, the tool axis X the plane YZ (heidenhain 7 rule 2; controller-mapping 1).
    [Fact]
    public void ToolCall_WithAnotherAxisLetter_WritesWorkplane()
    {
        Assert.Equal(Lines(
            "TOOL=3 OFFSET:LEN=3 OFFSET:RAD=3 RPM=500 WORKPLANE=YZ",
            "TOOL=4 OFFSET:LEN=4 OFFSET:RAD=4 WORKPLANE=ZX",
            "TOOL=5 OFFSET:LEN=5 OFFSET:RAD=5 WORKPLANE=XY"),
            Body("5 TOOL CALL 3 X S500\n6 TOOL CALL 4 Y\n7 TOOL CALL 5 Z"));
    }

    // The same axis letter again changes no plane and writes no WORKPLANE.
    [Fact]
    public void ToolCall_WithTheSameAxisLetter_WritesNoWorkplane()
    {
        Assert.Equal(Lines("TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1", "TOOL=2 OFFSET:LEN=2 OFFSET:RAD=2"),
            Body("5 TOOL CALL 1 Z\n6 TOOL CALL 2 Z"));
    }

    // TOOL CALL S2000 without a number sets only RPM (controller-mapping 3).
    [Fact]
    public void ToolCallS_WithoutANumber_IsRpm()
    {
        Assert.Equal(Lines("RPM=2000"), Body("5 TOOL CALL S2000"));
    }

    // TOOL CALL 0 empties the spindle, TOOL=0 (controller-mapping 3).
    [Fact]
    public void ToolCall0_EmptiesTheSpindle()
    {
        Assert.Equal(Lines("TOOL=0 OFFSET:LEN=0 OFFSET:RAD=0"), Body("5 TOOL CALL 0"));
    }

    // TOOL DEF n gives PRELOAD=n, TOOL DEF 0 clears the preload (heidenhain 7 rule 2; language 4.4).
    [Fact]
    public void ToolDef_IsPreload()
    {
        Assert.Equal(Lines("PRELOAD=5", "PRELOAD=0"), Body("5 TOOL DEF 5\n6 TOOL DEF 0"));
    }

    // The delta offsets DL and DR have no NCX word, and the meaning of the F of a TOOL CALL is not documented: the
    // block stays RAW (language 4.4; D5).
    [Theory]
    [InlineData("5 TOOL CALL 4 Z S1592 DL+0.1 DR-0.05")]
    [InlineData("5 TOOL CALL 4 Z S1592 F500")]
    public void ToolCall_WithDeltaOffsetsOrF_IsKeptAsRaw(string line)
    {
        NcxProgram program = FramedProgram(line);

        Assert.Single(program.Blocks, block => block.Find("RAW") is not null);
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // The words check without an ERROR: the offsets are the registers of the tool (virtual machine 3.5).
    [Fact]
    public void ToolCall_AndToolDef_CheckWithoutError()
    {
        string text = Text("0 BEGIN PGM T MM\n1 TOOL CALL 1 Z S1592\n2 TOOL DEF 2\n3 M3\n4 TOOL CALL 2 Z S800\n"
            + "5 M30\n6 END PGM T MM\n");

        Assert.False(Check(text).HasErrors, Check(text).ToText());
    }
}
