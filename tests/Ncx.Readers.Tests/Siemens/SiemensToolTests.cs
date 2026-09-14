using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Siemens.SiemensRead;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// The tools and the spindles (controllers siemens.md 5, 11 rule 3; controller-mapping 3, 4; D154, D155): T, M6 and D
/// per the [tool_change] style of the machine, S and M3 bound to the master spindle of SETMS, S2= and M2=3 to spindle
/// 2, the role from the machine file.
/// </summary>
public sealed class SiemensToolTests
{
    // Rule 3: on a mill with a magazine T before M6 preloads, M6 changes, D selects the offset (siemens 5;
    // controller-mapping 3).
    [Fact]
    public void T_BeforeM6_IsPreloadThenToolWithOffset()
    {
        Assert.Equal(Lines("PRELOAD=1", "TOOL=1", "OFFSET=1"), CheckedBody("T1\nM6\nD1"));
    }

    // T0 unloads (siemens 5): on a mill that changes with M6, T0 alone selects the empty place, PRELOAD=0, and the M6
    // on the next line changes to it, TOOL=0, the two-line form the corpus writes for every change (controller-mapping
    // 3, TOOL=4 and TOOL=0); check runs the result without an ERROR.
    [Fact]
    public void T0_ThenM6_IsTool0()
    {
        Assert.Equal(Lines("PRELOAD=1", "TOOL=1", "PRELOAD=0", "TOOL=0"), CheckedBody("T1\nM6\nT0\nM6"));
    }

    // The T=0 the Hermle writes before M30 unloads the spindle without an M6: TOOL=0 (controller-mapping 3, TOOL=0).
    [Fact]
    public void TEquals0_BeforeTheEnd_IsTool0()
    {
        Assert.Equal(Lines("PRELOAD=1", "TOOL=1", "SPINDLE=OFF", "TOOL=0"), CheckedBody("T1\nM6\nM5\nT=0"));
    }

    // T="NAME" selects the tool by its name, D0 cancels the offsets (siemens 5; controller-mapping 3, TOOL and OFFSET).
    [Fact]
    public void TByName_AndD0_AreToolByNameAndOffset0()
    {
        Assert.Equal(Lines("PRELOAD=\"DRILL\"", "TOOL=\"DRILL\"", "OFFSET=0"), CheckedBody("T=\"DRILL\"\nM6\nD0"));
    }

    // Rule 3: on a turret the selection is the change, T is TOOL and T0 unloads (siemens 5; controller-mapping 3).
    [Fact]
    public void T_OnATurret_IsTheChange()
    {
        Assert.Equal(Lines("TOOL=\"ROUGH\"", "TOOL=1", "TOOL=0"), CheckedBody("T=\"ROUGH\"\nT1\nT0", MillTurn()));
    }

    // Rule 3: S and M3 bind to the master spindle; a machine with one spindle writes no role (D154).
    [Fact]
    public void SAndM3_OnTheMill_AreTheSpindleWithoutARole()
    {
        Assert.Equal(Lines("SPINDLE=CW RPM=1000", "SPINDLE=OFF"), CheckedBody("S1000 M3\nM5"));
    }

    // Rule 3: SETMS(2) makes spindle 2 the master spindle that S and M3 refer to, SETMS alone returns to the configured
    // one; the roles come from the machine file (siemens 5).
    [Fact]
    public void SETMS2_BindsBareSToSpindle2()
    {
        Assert.Equal(Lines("SPINDLE:SUB=CW RPM:SUB=1000", "SPINDLE:MAIN=CCW RPM:MAIN=500"),
            CheckedBody("SETMS(2)\nS1000 M3\nSETMS\nS500 M4", MillTurn()));
    }

    // Rule 3: S2= and M2=3 address spindle 2, S1= and M1=4 spindle 1 (siemens 5).
    [Fact]
    public void S2AndM2_BindToSpindle2()
    {
        Assert.Equal(Lines("SPINDLE:SUB=CW RPM:SUB=1000", "SPINDLE:SUB=OFF", "SPINDLE:MAIN=CCW RPM:MAIN=200"),
            CheckedBody("S2=1000 M2=3\nM2=5\nS1=200 M1=4", MillTurn()));
    }

    // G96 S with LIMS is the constant surface speed with its limit, G97 ends it, G26 S is the upper limit (siemens 5;
    // controller-mapping 4, CSS and RPM_MAX).
    [Fact]
    public void G96WithLims_IsCssWithTheLimit()
    {
        Assert.Equal(Lines("FEED_MODE=PER_REV SPINDLE=CW CSS=ON VC=200 RPM_MAX=3000", "RPM=1000 CSS=OFF",
                "RPM_MAX=4000"),
            CheckedBody("G96 S200 LIMS=3000 M3\nG97 S1000\nG26 S4000"));
    }

    // SPOS= and M19 orient a spindle, M2=70 switches spindle 2 to axis mode (controller-mapping 4, ORIENT and
    // SPINDLE_MODE; D154, D155).
    [Fact]
    public void SposM19AndM70_OrientAndSwitchToAxisMode()
    {
        Assert.Equal(Lines("ORIENT:MAIN=90", "ORIENT:MAIN=0", "ORIENT:SUB=45", "SPINDLE_MODE:SUB=AXIS"),
            CheckedBody("SPOS=90\nM19\nSPOS[2]=45\nM2=70", MillTurn()));
    }

    // COUPON(S2, S1, 90) couples the following spindle 2 to the leading spindle 1 with an angular offset, COUPOF ends
    // the coupling (siemens 5; controller-mapping 4, SPINDLE_SYNC).
    [Fact]
    public void Coupon_IsSpindleSyncWithPhase()
    {
        Assert.Equal(Lines("SPINDLE_SYNC=MAIN,SUB PHASE=90", "SPINDLE_SYNC=OFF"),
            CheckedBody("COUPON(S2,S1,90)\nCOUPOF(S2,S1)", MillTurn()));
    }

    // A spindle the machine file names no role for keeps its block RAW (controller-mapping 4).
    [Fact]
    public void Setms_OfASpindleWithoutARole_IsRaw()
    {
        NcxProgram program = FramedProgram("SETMS(2)");

        Assert.Equal(Lines(Raw("SETMS(2)")), BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(program));
    }
}
