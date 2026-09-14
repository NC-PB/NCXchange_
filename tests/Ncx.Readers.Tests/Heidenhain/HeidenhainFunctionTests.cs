using static Ncx.Readers.Tests.Heidenhain.HeidenhainRead;

namespace Ncx.Readers.Tests.Heidenhain;

/// <summary>
/// The M functions (controllers heidenhain.md 2, 4; controller-mapping 1, 2, 4; machine-config 5; D83, D86): the
/// machine's tables, M126 and M127, M116 and M117, M128 and M129, M136 and M137, M140.
/// </summary>
public sealed class HeidenhainFunctionTests
{
    // M3, M4, M5, M8 and M9 come through the tables of the machine, the default spindle and coolant without an address
    // (machine-config 5; language 4.6, 4.10).
    [Fact]
    public void SpindleAndCoolant_ComeThroughTheTablesOfTheMachine()
    {
        Assert.Equal(Lines("SPINDLE=CW", "SPINDLE=CCW", "SPINDLE=OFF", "COOLANT=ON", "COOLANT=OFF"),
            Body("1 M3\n2 M4\n3 M5\n4 M8\n5 M9"));
    }

    // M functions at the end of a positioning block stand in its NCX block (heidenhain 2; language 5 rule 3).
    [Fact]
    public void MFunctions_InAPositioningBlock_StandInItsBlock()
    {
        Assert.Equal(Lines("RAPID X=10 COMP=OFF SPINDLE=CW COOLANT=ON"), Body("1 L X+10 R0 FMAX M3 M8"));
    }

    // M126 and M127 are ROTARY_PATH, M116 and M117 ROTARY_FEED (controller-mapping 1; D86).
    [Fact]
    public void M126M127M116M117_AreRotaryPathAndRotaryFeed()
    {
        Assert.Equal(Lines("ROTARY_PATH=SHORTEST", "ROTARY_PATH=FULL", "ROTARY_FEED=MM_MIN", "ROTARY_FEED=DEG_MIN"),
            Body("1 M126\n2 M127\n3 M116\n4 M117"));
    }

    // M128 and M129 are TCPM, M136 and M137 FEED_MODE (controller-mapping 1, 2).
    [Fact]
    public void M128M129M136M137_AreTcpmAndFeedMode()
    {
        Assert.Equal(Lines("TCPM=ON", "TCPM=OFF", "FEED_MODE=PER_REV", "FEED_MODE=PER_MIN"),
            Body("1 M128\n2 M129\n3 M136\n4 M137"));
    }

    // M140 MB MAX is RETRACT, M140 MB50 RETRACT=50 (controller-mapping 1, RETRACT; D83).
    [Fact]
    public void M140_IsRetract()
    {
        Assert.Equal(Lines("RETRACT", "RETRACT=50"), Body("1 M140 MB MAX\n2 M140 MB50 FMAX"));
    }

    // M128 with the feed of the compensating motion, M140 with a feed or in a moving block, and M92 stay RAW (D86;
    // virtual machine 3.1a).
    [Theory]
    [InlineData("1 M128 F1000")]
    [InlineData("1 M140 MB MAX F500")]
    [InlineData("1 L Z+10 FMAX M140 MB MAX")]
    [InlineData("1 L X+10 FMAX M92")]
    public void MFunctions_WithoutAnNcxForm_AreKeptAsRaw(string snippet)
    {
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(FramedProgram(snippet)));
    }
}
