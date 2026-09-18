namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// Straight lines and arcs in Klartext (controllers heidenhain.md 2; 8 rules 2 and 4; controller-mapping 2): the comma,
/// the sign on every coordinate, FMAX and F on change, R0, RL and RR, M91, IX, LN; CC plus C, CR and CP IPA.
/// </summary>
public sealed class HeidenhainMotionTests
{
    // Heidenhain 8 rule 2: the comma as the decimal separator and a sign on every coordinate, Z+0 included.
    [Fact]
    public void Rule2_Coordinates_AreWrittenWithTheCommaAndASign()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("RAPID X=50.4 Y=-7.025 Z=0"));

        Assert.Equal("L X+50,4 Y-7,025 Z+0 R0 FMAX", HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 2: FMAX for RAPID, F on a LINE where the feed changed; the F of the program stays modal
    // (heidenhain 2).
    [Fact]
    public void Rule2_Rapid_IsFmaxAndTheFeedOfALineIsWrittenWhereItChanged()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0",
            "LINE Z=-10 F=2387",
            "LINE X=7",
            "LINE X=10 F=2387",
            "LINE X=12 F=100"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nL Z-10 F2387\nL X+7\nL X+10\nL X+12 F100", HeidenhainCompile.Body(result));
    }

    // Controller-mapping 2, COMP; language 4.4: R0, RL and RR on the straight line where the compensation changes,
    // and R0 on the first line of the program, whose compensation the control does not know yet.
    [Fact]
    public void Rule2_Compensation_IsWrittenOnTheLineWhereItChanges()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0",
            "LINE Y=2 COMP=LEFT F=100",
            "LINE X=7",
            "LINE Y=0 COMP=OFF",
            "RAPID Z=5"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nL Y+2 RL F100\nL X+7\nL Y+0 R0\nL Z+5 FMAX", HeidenhainCompile.Body(result));
    }

    // Language 4.4, COMP applies from the motion of the same block on; Klartext writes the compensation in a line, so
    // the COMP of a block without a motion stands on the next line.
    [Fact]
    public void Rule2_CompensationOfABlockWithoutMotion_StandsOnTheNextLine()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "COMP=RIGHT", "LINE X=5 F=100"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nL X+5 RR F100", HeidenhainCompile.Body(result));
    }

    // Language 4.4, virtual machine 4: a COMP that no motion follows before PROGRAM=END moves nothing, and
    // PROGRAM=END, the M30, resets the compensation, so it needs no L block and nothing is reported.
    [Fact]
    public void Rule2_CompensationChangeThatNoMotionFollows_IsResetByTheM30()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LINE Z=-1 F=100", "LINE X=10 COMP=LEFT", "COMP=OFF"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nL Z-1 F100\nL X+10 RL", HeidenhainCompile.Body(result));
        Assert.DoesNotContain(result.Diagnostics.Items,
            diagnostic => diagnostic.Code.StartsWith("CMP", StringComparison.Ordinal));
    }

    // Controllers heidenhain.md 2: IX+30 is the incremental coordinate; controller-mapping 2, IX.
    [Fact]
    public void Rule2_IncrementalWords_AreIWithTheAxis()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LINE IX=30 IY=-5 F=800"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nL IX+30 IY-5 F800", HeidenhainCompile.Body(result));
    }

    // Controllers heidenhain.md 2; controller-mapping 1, FRAME=MACHINE: M91 in the block, at its end.
    [Fact]
    public void Rule2_MachineFrame_IsM91AtTheEndOfTheBlock()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "RAPID Z=0 FRAME=MACHINE"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nL Z+0 FMAX M91", HeidenhainCompile.Body(result));
    }

    // Controllers heidenhain.md 6: a Q parameter may stand where a number stands, L X+Q1 Y-Q2.
    [Fact]
    public void Rule2_QParameterInACoordinate_IsWrittenWithItsSign()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "VAR:Q1=5", "VAR:Q2=3", "RAPID X={$Q1} Y={-$Q2}"));

        Assert.Equal("Q1 = 5\nQ2 = 3\nL X+Q1 Y-Q2 R0 FMAX", HeidenhainCompile.Body(result));
    }

    // The TODO(question) of HeidenhainNumbers: a formula in a coordinate has no Klartext form, CMP102.
    [Fact]
    public void Rule2_FormulaInACoordinate_IsTheErrorCmp102()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("RAPID X={$Q1 + 5} Y=0"));

        Assert.Empty(result.Files);
        Assert.Equal(4, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainValueWithoutKlartext).Line);
    }

    // Controllers heidenhain.md 2; controller-mapping 2, tool vectors; D81: a LINE with the tool vector under TCPM is
    // LN with the surface normal and the tool vector, as language 6 writes it.
    [Fact]
    public void ToolVector_OfALine_IsLnWithTheNormalAndTheToolVector()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TCPM=ON",
            "RAPID X=0 Y=0 Z=100",
            "LINE X=41.786 Y=-57.382 Z=95.488 TX=0 TY=0.5 TZ=0.866 NX=0 NY=0 NZ=1 F=2841"));

        Assert.Equal(
            "M128\nL X+0 Y+0 Z+100 R0 FMAX\nLN X+41,786 Y-57,382 Z+95,488 NX0 NY0 NZ1 TX0 TY0,5 TZ0,866 F2841",
            HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 4: CR when the NCX block had R; DR- is CW (language 4.3).
    [Fact]
    public void Rule4_ArcWithRadius_IsCr()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=7 Y=2", "LINE Z=-1 F=100", "ARC=CW X=2 Y=7 R=5"));

        Assert.Equal("L X+7 Y+2 R0 FMAX\nL Z-1 F100\nCR X+2 Y+7 R+5 DR-", HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 4: the canonical form CC plus C; DR+ is CCW (language 4.3; controller-mapping 2, CENTER:X).
    [Fact]
    public void Rule4_ArcWithCenter_IsCcPlusC()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=50.534 Y=69.993", "LINE Z=-1 F=100", "ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50"));

        Assert.Equal("L X+50,534 Y+69,993 R0 FMAX\nL Z-1 F100\nCC X+50 Y+50\nC X+70 Y+50 DR+",
            HeidenhainCompile.Body(result));
    }

    // Controller-mapping 2, CENTER:IX: the incremental centre is CC IX IY, incremental from the start of the arc.
    [Fact]
    public void Rule4_ArcWithIncrementalCenter_IsCcWithIncrementalWords()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=50.534 Y=69.993", "LINE Z=-1 F=100", "ARC=CCW X=70 Y=50 CENTER:IX=-0.534 CENTER:IY=-19.993"));

        Assert.Equal("L X+50,534 Y+69,993 R0 FMAX\nL Z-1 F100\nCC IX-0,534 IY-19,993\nC X+70 Y+50 DR+",
            HeidenhainCompile.Body(result));
    }

    // Virtual machine 3.2: start = end is a full circle, whose C ends where it starts.
    [Fact]
    public void Rule4_FullCircle_IsCToItsStart()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=60 Y=50", "LINE Z=-1 F=100", "ARC=CCW CENTER:X=50 CENTER:Y=50"));

        Assert.Equal("L X+60 Y+50 R0 FMAX\nL Z-1 F100\nCC X+50 Y+50\nC X+60 Y+50 DR+", HeidenhainCompile.Body(result));
    }

    // Virtual machine 3.2 and 3.4: the full circle ends at its start, a coordinate of the workpiece frame, which a
    // SETPOS shifted (cycle 7, machine-config 3): the physical position less the setpos shift.
    [Fact]
    public void Rule4_FullCircleAfterSetpos_EndsAtItsStartInTheShiftedFrame()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=10 Y=0", "RAPID Z=5", "SETPOS X=0 Y=0", "LINE Z=-1 F=100", "ARC=CCW CENTER:X=5 CENTER:Y=0"));

        Assert.Equal(
            "L X+10 Y+0 R0 FMAX\nL Z+5 FMAX\nCYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\nCYCL DEF 7.2 Y+0\nL Z-1 F100\n"
            + "CC X+5 Y+0\nC X+0 Y+0 DR+",
            HeidenhainCompile.Body(result));
    }

    // Language 4.4; controllers heidenhain.md 2, differences.md (radius compensation): COMP applies from the motion of
    // its block on, and Klartext writes R0, RL and RR at the end of an L block only, so an arc after a COMP=LEFT that
    // no L block has written would run uncompensated on the control, CMP115.
    [Fact]
    public void Rule4_ArcAfterACompensationChangeNoLineWrote_IsTheErrorCmp115()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LINE Z=-1 F=100", "COMP=LEFT", "ARC=CW X=10 Y=0 R=5", "LINE X=20"));

        Assert.Empty(result.Files);
        Assert.Equal(7, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainCompensationWithoutLine).Line);
    }

    // Language 4.4; virtual machine 3.9, D99: a subprogram entered with RL active runs with it, so an arc after its
    // COMP=OFF without a motion would run compensated on the control, CMP115, although its walk starts from an unknown
    // target state.
    [Fact]
    public void Rule4_ArcAfterCompOffInASubprogramEnteredWithRl_IsTheErrorCmp115()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"",
            "UNITS=MM WORKPLANE=XY",
            "RAPID X=0 Y=0",
            "LINE Z=-1 F=100",
            "LINE X=10 COMP=LEFT",
            "CALL=1",
            "PROGRAM=END",
            "SUB=BEGIN NAME=1",
            "COMP=OFF",
            "ARC=CW X=20 Y=0 R=5",
            "SUB=END",
            "FILE=END"));

        Assert.Empty(result.Files);
        Assert.Equal(11, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainCompensationWithoutLine).Line);
    }

    // Heidenhain 8 rule 4, controllers heidenhain.md 2, D84: a sweep beyond a turn is CP IPA about the pole, the sign
    // of IPA that of the direction, with the travel of the helix.
    [Fact]
    public void Rule4_SweepBeyondATurn_IsCpIpaAboutThePole()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=60 Y=50 Z=0",
            "LINE Z=-1 F=100",
            "ARC=CCW IZ=-5.4 CENTER:X=50 CENTER:Y=50 ANGLE=737.956",
            "ARC=CW CENTER:X=50 CENTER:Y=50 ANGLE=400"));

        Assert.Equal(
            "L X+60 Y+50 Z+0 R0 FMAX\nL Z-1 F100\nCC X+50 Y+50\nCP IPA+737,956 IZ-5,4 DR+\nCC X+50 Y+50\n"
            + "CP IPA-400 DR-",
            HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 2: the feed where it changed, also on an arc, which moves at the feed like a line (language
    // 4.3).
    [Fact]
    public void Rule4_FeedOfAnArc_IsWrittenWhereItChanges()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=7 Y=2", "LINE Z=-1 F=100", "ARC=CW X=2 Y=7 R=5 F=50"));

        Assert.Equal("L X+7 Y+2 R0 FMAX\nL Z-1 F100\nCR X+2 Y+7 R+5 DR- F50", HeidenhainCompile.Body(result));
    }
}
