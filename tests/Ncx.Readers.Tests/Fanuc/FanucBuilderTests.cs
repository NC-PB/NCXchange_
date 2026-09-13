using Ncx.Core.Model;
using static Ncx.Readers.Tests.Fanuc.FanucRead;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// The M codes through the machine's tables (controller-mapping 4, machine-config 5, D105), the wait marks of
/// controller-mapping 7, and what the Fanuc reader keeps as RAW (controller-mapping 9; controllers fanuc.md 9 rule 7).
/// </summary>
public sealed class FanucBuilderTests
{
    // M8 and M9 are COOLANT=ON and OFF of the default channel, compared by number (controller-mapping 4, D105).
    [Fact]
    public void M08AndM9_AreCoolantOnAndOff()
    {
        Assert.Equal(Lines("COOLANT=ON", "COOLANT=OFF"), Body("M08\nM9"));
    }

    // An M code of [func] is FUNC:name=state (controller-mapping 4, FUNC).
    [Fact]
    public void M29_IsTheFunctionOfItsTable()
    {
        Assert.Equal(Lines("FUNC:RIGID_TAP=ON"), Body("M29"));
    }

    // Any other M is MFUNC with a WARNING (controller-mapping 4, MFUNC; machine-config 5).
    [Fact]
    public void M136_NamedByNoTable_IsMfuncWithAWarning()
    {
        NcxProgram program = FramedProgram("M136");

        Assert.Equal(Lines("MFUNC=136"), BodyOf(Core.Writing.NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.FanucMCodeNotNamed], Codes(program));
    }

    // The builder's spindle codes are SPINDLE:role, the S of the block goes with them (controller-mapping 4, Nakamura
    // M88 S1000).
    [Fact]
    public void M88S1000_IsTheToolSpindleWithItsSpeed()
    {
        Assert.Equal(Lines("SPINDLE:TOOL=CW RPM:TOOL=1000"), Body("M88 S1000", Lathe()));
    }

    // M91 and M41 switch the C axis of the main spindle, SPINDLE_MODE:MAIN (controller-mapping 4, SPINDLE_MODE).
    [Fact]
    public void M91AndM41_AreTheSpindleModeOfTheMainSpindle()
    {
        Assert.Equal(Lines("SPINDLE_MODE:MAIN=AXIS", "SPINDLE_MODE:MAIN=SPINDLE"), Body("M91\nM41", Lathe()));
    }

    // M96 synchronizes the two work spindles, M97 ends it (controller-mapping 4, SPINDLE_SYNC).
    [Fact]
    public void M96AndM97_AreSpindleSyncOfTheWorkSpindles()
    {
        Assert.Equal(Lines("SPINDLE_SYNC=MAIN,SUB", "SPINDLE_SYNC=OFF"), Body("M96\nM97", Lathe()));
    }

    // M428 and M427 of [workpiece] select the spindle the turret works on, the datum stays ORIGIN (controller-mapping
    // 4, WORKPIECE).
    [Fact]
    public void M428AndG59M427_AreWorkpieceMainAndSub()
    {
        Assert.Equal(Lines("WORKPIECE=MAIN", "ORIGIN=6 WORKPIECE=SUB"), Body("M428\nG59 M427", Lathe()));
    }

    // The wait codes of [sync] are SYNC marks, P the participating paths, WITH (controller-mapping 7).
    [Fact]
    public void WaitCodes_M110AndM120P12_AreSyncMarks()
    {
        Assert.Equal(Lines("SYNC=110", "SYNC=120 WITH=1,2"), Body("M110\nM120 P12", Lathe()));
    }

    // G10 writes data and stays RAW with a WARNING, never dropped (controllers fanuc.md 9 rule 7; controller-mapping
    // 9).
    [Fact]
    public void G10_IsKeptAsRawFanucWithAWarning()
    {
        NcxProgram program = FramedProgram("G10 L2 P1 Z-175.");

        Assert.Equal(Lines("RAW:FANUC=\"G10 L2 P1 Z-175.\""), BodyOf(Core.Writing.NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // A builder macro of the [raw] table is RAW:BUILDER (machine-config 5, controller-mapping 9).
    [Fact]
    public void G411_OfTheRawTable_IsRawOfTheBuilder()
    {
        Assert.Equal(Lines("RAW:NAKAMURA=\"G411 L1. I102.\""), Body("G411 L1. I102.", Lathe()));
    }

    // The block a G411 of the builder jumps to, N{n} of I{n}, keeps its LABEL, so that the RAW jump still finds its
    // block after a compile (controller-mapping 6 and 8; D5, language 2 rule 8).
    [Fact]
    public void G411_TheBlockOfItsJumpTarget_KeepsItsLabel()
    {
        Assert.Equal(
            Lines("RAW:NAKAMURA=\"G411 L1. I102.\"", "LABEL=102", "RAPID X=1"),
            Body("G411 L1. I102.\nN102 G0 X1.", Lathe()));
    }

    // M92, the PHASE code of [spindle_sync], runs the spindles phase-synchronous with the phase position that M93 set
    // in the control; PHASE needs that angle, which the program does not carry, so the block stays RAW (controller-
    // mapping 4, SPINDLE_SYNC and PHASE; language 4.5; D5).
    [Fact]
    public void M92_PhaseCodeWithoutAnAngle_IsKeptAsRaw()
    {
        NcxProgram program = FramedProgram("M92", Lathe());

        Assert.Equal(Lines("RAW:FANUC=\"M92\""), BodyOf(Core.Writing.NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // Probing (G31), push checks (G38) and every G code the reader does not map stay RAW (controller-mapping 9).
    [Fact]
    public void G31G38AndUnmappedCodes_AreKeptAsRaw()
    {
        Assert.Equal(
            Lines("RAW:FANUC=\"G31 Z-10. F100\"", "RAW:FANUC=\"G38 A10.\"", "RAW:FANUC=\"G64\""),
            Body("G31 Z-10. F100\nG38 A10.\nG64"));
    }
}
