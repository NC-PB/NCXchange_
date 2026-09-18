namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// Cycles as CYCL DEF with the Q parameters in the control's order, and CYCL CALL or M99 (controllers heidenhain.md 5;
/// 8 rule 7; controller-mapping 5; machine-config 6; D163, D164, D178, D226).
/// </summary>
public sealed class HeidenhainCycleTests
{
    // The tool and the position before every cycle of these tests.
    private const string Start = "TOOL CALL 1 Z S1000\nL X+10 Y+10 Z+5 R0 FMAX\n";

    // Heidenhain 8 rule 7: cycle 200 with its Q parameters in the order of the signature, BOHREN.h's; the planes
    // relative to Q203 (heidenhain 5); CYCL CALL at the current position and M99 in the positioning block; CYCLE=OFF
    // writes nothing, since the definition stays until the next CYCL DEF.
    [Fact]
    public void Rule7_Drill_IsCycle200InTheControlsOrderWithCyclCallAndM99()
    {
        CompileResult result = Run(
            "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE CYCLE_F=565",
            "CYCLE_CALL",
            "CYCLE_CALL X=30",
            "CYCLE=OFF");

        Assert.Equal(
            Start + "CYCL DEF 200 BOHREN ~\n    Q200=+5 ~\n    Q201=-21,732 ~\n    Q206=+565 ~\n    Q202=+21,732 ~\n"
            + "    Q210=+0 ~\n    Q203=+0 ~\n    Q204=+5 ~\n    Q211=+0\nCYCL CALL\nL X+30 FMAX M99",
            HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 7: CYCLE_RETRACT=SAFE becomes Q204, SAFE less SURFACE; CYCLE_DWELL is Q211.
    [Fact]
    public void Rule7_CycleRetractSafe_BecomesQ204()
    {
        CompileResult result = Run(
            "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-15 SAFE=50 CYCLE_RETRACT=SAFE CYCLE_F=300 CYCLE_DWELL=0.5");

        string body = HeidenhainCompile.Body(result);
        Assert.Contains("    Q204=+50 ~\n", body, StringComparison.Ordinal);
        Assert.EndsWith("    Q211=+0,5", body, StringComparison.Ordinal);
    }

    // The TODO(question) D226 of HeidenhainCycleValues: CYCLE_RETRACT=CLEARANCE writes Q204 equal to Q200; every plane
    // relative to the surface Q203.
    [Fact]
    public void Rule7_CycleRetractClearance_WritesQ204EqualToQ200()
    {
        CompileResult result = Run("CYCLE=DRILL SURFACE=-2 CLEARANCE=3 DEPTH=-10 SAFE=20 CYCLE_F=300");

        Assert.EndsWith(
            "Q200=+5 ~\n    Q201=-8 ~\n    Q206=+300 ~\n    Q202=+8 ~\n    Q210=+0 ~\n    Q203=-2 ~\n    Q204=+5 ~\n"
            + "    Q211=+0",
            HeidenhainCompile.Body(result), StringComparison.Ordinal);
    }

    // Controller-mapping 5, PECK: cycle 203; the TODO(question) D163 and D164 of HeidenhainCycleValues: Q213=0 for
    // PECK, Q205 equal to Q202, Q208=MAX, Q256 0.6, Q210 and Q212 zero.
    [Fact]
    public void Rule7_Peck_IsCycle203WithQ213Zero()
    {
        CompileResult result = Run(
            "CYCLE=PECK SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE PECK=1.2 CYCLE_F=565");

        Assert.EndsWith(
            "CYCL DEF 203 UNIVERSALBOHREN ~\n    Q200=+5 ~\n    Q201=-21,732 ~\n    Q206=+565 ~\n    Q202=+1,2 ~\n"
            + "    Q210=+0 ~\n    Q203=+0 ~\n    Q204=+5 ~\n    Q212=+0 ~\n    Q213=+0 ~\n    Q205=+1,2 ~\n"
            + "    Q211=+0 ~\n    Q208=MAX ~\n    Q256=+0,6",
            HeidenhainCompile.Body(result), StringComparison.Ordinal);
    }

    // The TODO(question) D163 of HeidenhainCycleValues: Q213 of CHIP_BREAK is the number of infeeds, the depth over
    // PECK rounded up, 19 for 21.732 over 1.2.
    [Fact]
    public void Rule7_ChipBreak_IsCycle203WithTheNumberOfInfeedsAsQ213()
    {
        CompileResult result = Run(
            "CYCLE=CHIP_BREAK SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE PECK=1.2 CYCLE_F=565");

        Assert.Contains("    Q213=+19 ~\n", HeidenhainCompile.Body(result), StringComparison.Ordinal);
    }

    // Controller-mapping 5, TAP: cycle 207 with the pitch Q239; its signature has no Q206 and no Q211, so CYCLE_F is
    // not written, with the WARNING CMP105 (machine-config 6).
    [Fact]
    public void Rule7_Tap_IsCycle207AndItsFeedIsNotWrittenWithAWarning()
    {
        CompileResult result = Run(
            "CYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE CYCLE_F=750 PITCH=1.5");

        Assert.EndsWith(
            "CYCL DEF 207 GEW.-BOHREN GS NEU ~\n    Q200=+5 ~\n    Q201=-20 ~\n    Q239=+1,5 ~\n    Q203=+0 ~\n"
            + "    Q204=+5",
            HeidenhainCompile.Body(result), StringComparison.Ordinal);
        Assert.Equal(6, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainCycleWordNotWritten).Line);
    }

    // Controller-mapping 5, REAM; the TODO(question) D164 of HeidenhainCycleValues: Q208 of cycle 201 equals Q206.
    [Fact]
    public void Rule7_Ream_IsCycle201WithTheRetractionFeedOfThePlungeFeed()
    {
        CompileResult result = Run(
            "CYCLE=REAM SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE CYCLE_F=420");

        Assert.EndsWith(
            "CYCL DEF 201 REIBEN ~\n    Q200=+5 ~\n    Q201=-20 ~\n    Q206=+420 ~\n    Q211=+0 ~\n    Q208=+420 ~\n"
            + "    Q203=+0 ~\n    Q204=+5",
            HeidenhainCompile.Body(result), StringComparison.Ordinal);
    }

    // The TODO(question) D178 of HeidenhainCycles: the signature of cycle 202 is not in the documents, and compiling
    // an entry without one is CMP103.
    [Fact]
    public void Rule7_BoreWithoutSignature_IsTheErrorCmp103()
    {
        CompileResult result = Run("CYCLE=BORE SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_F=100");

        Assert.Empty(result.Files);
        Assert.Equal(6, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainCycleWithoutSignature).Line);
    }

    // Controllers heidenhain.md 8 rule 7, heidenhain 5: Q203 is the surface every plane is relative to, so a cycle
    // without SURFACE has no Q203, CMP104.
    [Fact]
    public void Rule7_CycleWithoutSurface_IsTheErrorCmp104()
    {
        CompileResult result = Run("CYCLE=DRILL CLEARANCE=5 DEPTH=-20 CYCLE_F=100");

        Assert.Empty(result.Files);
        Assert.Equal(6, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainCycleParameterWithoutValue).Line);
    }

    // Language 4.4; controllers heidenhain.md 2, differences.md (radius compensation): CYCL CALL carries no R0, RL
    // or RR, so a call at the current position after a COMP=OFF that no L block has written would run with the RL of
    // the control, CMP115.
    [Fact]
    public void Rule7_CyclCallAfterACompensationChangeNoLineWrote_IsTheErrorCmp115()
    {
        CompileResult result = Run(
            "LINE X=20 COMP=LEFT F=100",
            "COMP=OFF",
            "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE CYCLE_F=565",
            "CYCLE_CALL");

        Assert.Empty(result.Files);
        Assert.Equal(9, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainCompensationWithoutLine).Line);
    }

    // Language 4.7.1, D94: a native cycle is CYCL DEF n with its Q parameters in source order.
    [Fact]
    public void Rule7_NativeCycle_IsCyclDefWithItsQParametersInSourceOrder()
    {
        CompileResult result = Run("CYCLE:HEIDENHAIN=251 Q218=60 Q215=0 Q219=40");

        Assert.EndsWith("CYCL DEF 251 ~\n    Q218=+60 ~\n    Q215=+0 ~\n    Q219=+40", HeidenhainCompile.Body(result),
            StringComparison.Ordinal);
    }

    // Controller-mapping 1, DWELL: cycle 9 with the seconds.
    [Fact]
    public void Dwell_IsCycle9WithTheSeconds()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("DWELL=1.5"));

        Assert.Equal("CYCL DEF 9.0 VERWEILZEIT\nCYCL DEF 9.1 V.ZEIT 1,5", HeidenhainCompile.Body(result));
    }

    private static CompileResult Run(params string[] blocks)
    {
        var lines = new List<string> { "TOOL=1 RPM=1000", "RAPID X=10 Y=10 Z=5" };
        lines.AddRange(blocks);
        return HeidenhainCompile.Run(HeidenhainCompile.Program(lines.ToArray()));
    }
}
