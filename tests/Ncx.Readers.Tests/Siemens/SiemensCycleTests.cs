using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Siemens.SiemensRead;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// The cycles (controllers siemens.md 7, 11 rule 5; controller-mapping 5; machine-config 6; D94, D157): the sl
/// signatures through the catalog into CYCLE= blocks, _AXN as AXIS, the modal F before MCALL as CYCLE_F, the MCALL
/// positions as CYCLE_CALL, MCALL alone as CYCLE=OFF, the patterns expanded into calls.
/// </summary>
public sealed class SiemensCycleTests
{
    private const string Drill = "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-5 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_F=100";

    // Rule 5: MCALL CYCLE81 makes the cycle modal with the F before it as CYCLE_F, every block with a position calls
    // it, MCALL alone is CYCLE=OFF; the retract plane RTP is SAFE with CYCLE_RETRACT=SAFE (D157).
    [Fact]
    public void MCALLCYCLE81_PositionsBecomeCycleCalls()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10",
                "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-5 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_F=200",
                "CYCLE_CALL X=10 Y=10", "CYCLE_CALL X=20", "CYCLE=OFF", "RAPID Z=50"),
            CheckedBody("G0 X0 Y0 Z10\nF200\nMCALL CYCLE81(10,0,2,-5,)\nX10 Y10\nX20\nMCALL\nG0 Z50"));
    }

    // A direct call runs the cycle once at the current position: CYCLE= and one CYCLE_CALL (siemens 7).
    [Fact]
    public void DirectCall_IsCycleAndOneCall()
    {
        Assert.Equal(Lines("RAPID X=10 Y=10 Z=10", Drill, "CYCLE_CALL", "RAPID Z=50"),
            CheckedBody("G0 X10 Y10 Z10\nF100\nCYCLE81(10,0,2,-5,)\nG0 Z50"));
    }

    // Rule 5: _AXN is AXIS, the drilling axis 3 the third geometry axis Z; the parameters the catalog entry has no word
    // for are not written, with a WARNING (siemens 7; machine-config 6).
    [Fact]
    public void CYCLE83WithAxn_IsPeckWithAxis()
    {
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\nF100\nMCALL CYCLE83(10,0,2,-30,,5,,1,0,0,1,1,3)\nX5\nMCALL");

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10",
                "CYCLE=PECK AXIS=Z SURFACE=0 CLEARANCE=2 DEPTH=-30 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_F=100 "
                    + "CYCLE_DWELL=0",
                "CYCLE_CALL X=5", "CYCLE=OFF"),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensValueNotCarried], Codes(program));
    }

    // Rule 5: HOLES1 and HOLES2 call the modal cycle at every position of the row and of the circle (siemens 7).
    [Fact]
    public void Holes1AndHoles2_ExpandIntoCycleCalls()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Drill, "CYCLE_CALL X=10 Y=0", "CYCLE_CALL X=20 Y=0",
                "CYCLE_CALL X=30 Y=0", "CYCLE=OFF"),
            CheckedBody("G0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nHOLES1(0,0,0,10,10,3)\nMCALL"));
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Drill, "CYCLE_CALL X=20 Y=0", "CYCLE_CALL X=0 Y=20",
                "CYCLE_CALL X=-20 Y=0", "CYCLE_CALL X=0 Y=-20", "CYCLE=OFF"),
            CheckedBody("G0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nHOLES2(0,0,20,0,0,4)\nMCALL"));
    }

    // The parameters of CYCLE86 that the catalog entry BORE names no word for are not written, with a WARNING
    // (machine-config 6).
    [Fact]
    public void CYCLE86_ParametersWithoutAWord_AreNotWrittenWithAWarning()
    {
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\nCYCLE86(10,0,2,-20,,1,3,1,1,0,0)");

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10",
                "CYCLE=BORE SURFACE=0 CLEARANCE=2 DEPTH=-20 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_DWELL=1", "CYCLE_CALL"),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensValueNotCarried], Codes(program));
    }

    // CYCLE832 is TOLERANCE with the mode of the machine's table, CYCLE832(0, 0, 1) switches it off (controller-mapping
    // 1, TOLERANCE; D85).
    [Fact]
    public void CYCLE832_IsTolerance()
    {
        Assert.Equal(Lines("TOLERANCE=0.02 TOLERANCE_MODE=FINISH", "TOLERANCE=OFF"),
            CheckedBody("CYCLE832(0.02,1,0.1)\nCYCLE832(0,0,1)"));
    }
}
