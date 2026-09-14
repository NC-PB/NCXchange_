using Ncx.Compilers.Tests.Fakes;

namespace Ncx.Compilers.Tests;

/// <summary>
/// The tool change of the framework with the look-ahead of the STATIC pass (machine-config 3; virtual machine 3.5;
/// D52): {next} from a following PRELOAD and from auto_preload, the preload auto_preload inserts, change_preloaded and
/// unload.
/// </summary>
public sealed class ToolChangeTests
{
    // A change command that carries the next tool, as the Nakamura G340 T.. A.. does (machine-config 3).
    private const string ChangeWithNext = """
        [tool_change]
        change = "T{tool} M6 A{next}"
        preload = "T{tool}"
        """;

    // The header of every program of the tests (language 4.1).
    private const string ProgramHeader = "UNITS=MM WORKPLANE=XY FEED_MODE=PER_MIN";

    // auto_preload with the inserted preload before the first motion after the change, as on the Mori Seiki NTX
    // (machine-config 3).
    private const string BeforeFirstMotion = FakeMachines.PlainToolChange
        + "\nauto_preload = true\npreload_position = \"before_first_motion\"";

    // Machine-config 3, language 4.4: a PRELOAD that follows the TOOL block before the next motion is folded into a
    // change command with {next}, and not written again.
    [Fact]
    public void Next_FollowingPreload_IsFoldedIntoTheChange()
    {
        string program = FakeCompile.Program(
            "TOOL=1", "PRELOAD=2", "RAPID X=0 Y=0 Z=5", "TOOL=2", "PRELOAD=0", "RAPID Z=10");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: ChangeWithNext)));

        Assert.Equal(
            "%\nO1 (T)\nN10 T1 M6 A2\nN20 G0 X0. Y0. Z5.\nN30 T2 M6 A0\nN40 Z10.\nN50 M30\n%\n",
            text);
    }

    // Language 4.4: the Nakamura block that changes and preloads, TOOL=1 PRELOAD=2, folds its own PRELOAD.
    [Fact]
    public void Next_PreloadInTheToolBlock_IsFoldedIntoTheChange()
    {
        string program = FakeCompile.Program("TOOL=1 PRELOAD=2", "RAPID X=0 Y=0 Z=5", "TOOL=2 PRELOAD=0");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: ChangeWithNext)));

        Assert.Equal("%\nO1 (T)\nN10 T1 M6 A2\nN20 G0 X0. Y0. Z5.\nN30 T2 M6 A0\nN40 M30\n%\n", text);
    }

    // Machine-config 3, virtual machine 3.5: with auto_preload, in a program without PRELOAD words, {next} is the next
    // tool of the STATIC look-ahead; the last change has none and writes 0 (the TODO(question) of NextOf).
    [Fact]
    public void Next_AutoPreload_IsTheNextToolOfTheLookAhead()
    {
        string toolChange = ChangeWithNext + "\nauto_preload = true";
        string program = FakeCompile.Program("TOOL=1", "RAPID X=0 Y=0 Z=5", "TOOL=3", "RAPID Z=10");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: toolChange)));

        Assert.Equal("%\nO1 (T)\nN10 T1 M6 A3\nN20 G0 X0. Y0. Z5.\nN30 T3 M6 A0\nN40 Z10.\nN50 M30\n%\n", text);
    }

    // Machine-config 3, virtual machine 3.5: with auto_preload and a change command without {next}, the preload of
    // the next tool is inserted after each change; the last change has no next tool.
    [Fact]
    public void AutoPreload_AfterChange_InsertsThePreloadOfTheNextTool()
    {
        string toolChange = FakeMachines.PlainToolChange + "\nauto_preload = true";
        string program = FakeCompile.Program(
            "TOOL=1", "COMMENT=\"ROUGH\"", "RAPID X=0 Y=0 Z=5", "TOOL=3", "RAPID Z=10");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: toolChange)));

        Assert.Equal(
            "%\nO1 (T)\nN10 T1 M6\nN20 T3\n(ROUGH)\nN30 G0 X0. Y0. Z5.\nN40 T3 M6\nN50 Z10.\nN60 M30\n%\n",
            text);
    }

    // Machine-config 3: under preload_position = "before_first_motion" the inserted preload waits for the first motion
    // after the change.
    [Fact]
    public void AutoPreload_BeforeFirstMotion_StandsBeforeTheFirstMotionAfterTheChange()
    {
        string toolChange = FakeMachines.PlainToolChange
            + "\nauto_preload = true\npreload_position = \"before_first_motion\"";
        string program = FakeCompile.Program(
            "TOOL=1", "COMMENT=\"ROUGH\"", "RAPID X=0 Y=0 Z=5", "TOOL=3", "RAPID Z=10");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: toolChange)));

        Assert.Equal(
            "%\nO1 (T)\nN10 T1 M6\n(ROUGH)\nN20 T3\nN30 G0 X0. Y0. Z5.\nN40 T3 M6\nN50 Z10.\nN60 M30\n%\n",
            text);
    }

    // Machine-config 3, virtual machine 3.5 and 5: a preload that waits for the first motion is dropped when the
    // change of the tool it names comes first; after that change it would preload the tool already in the spindle.
    [Fact]
    public void AutoPreload_BeforeFirstMotionWithTheNextChangeFirst_IsDropped()
    {
        string program = FakeCompile.Program("TOOL=1", "TOOL=2", "RAPID X=0 Y=0 Z=5");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: BeforeFirstMotion)));

        Assert.Equal("%\nO1 (T)\nN10 T1 M6\nN20 T2 M6\nN30 G0 X0. Y0. Z5.\nN40 M30\n%\n", text);
    }

    // Machine-config 3, virtual machine 3.5: a change of the holder to another tool than the waiting preload names
    // comes before the first motion; the preload stands before that change.
    [Fact]
    public void AutoPreload_BeforeFirstMotionWithAnUnloadFirst_StandsBeforeTheUnload()
    {
        string toolChange = BeforeFirstMotion + "\nunload = \"T0 M6\"";
        string program = FakeCompile.Program("TOOL=1", "TOOL=0", "TOOL=3", "RAPID X=0 Y=0 Z=5");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: toolChange)));

        Assert.Equal("%\nO1 (T)\nN10 T1 M6\nN20 T3\nN30 T0 M6\nN40 T3 M6\nN50 G0 X0. Y0. Z5.\nN60 M30\n%\n", text);
    }

    // Machine-config 3; virtual machine 3.9, D99: the first motion after the change lies in the walk of a subprogram,
    // whose section is written once for every caller, so the preload stands before the CALL that enters the walk (the
    // TODO(question) of LookAhead.BeforeFirstMotion); the next change does not preload its own tool.
    [Fact]
    public void AutoPreload_BeforeFirstMotionInACalledSub_StandsBeforeTheCall()
    {
        string program = ProgramAndSub(["TOOL=1", "CALL=100", "TOOL=3", "RAPID Z=50"], ["RAPID X=0 Y=0 Z=5"]);

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: BeforeFirstMotion)));

        Assert.Equal(
            "%\nO1 (T)\nN10 T1 M6\nN20 T3\nN30 M98 P100\nN40 T3 M6\nN50 Z50.\nN60 M30\nO100\nN70 G0 X0. Y0. Z5.\n"
            + "N80 M99\n%\n",
            text);
    }

    // Machine-config 3; virtual machine 3.9, D99: a called subprogram that does not move leaves the first motion after
    // the change to the caller, where the preload stands before it.
    [Fact]
    public void AutoPreload_BeforeFirstMotionWithACallOfASubWithoutMotion_StandsBeforeTheMotionOfTheCaller()
    {
        string program = ProgramAndSub(
            ["TOOL=1", "CALL=100", "RAPID X=0 Y=0 Z=5", "TOOL=3", "RAPID Z=10"], ["COMMENT=\"PART\""]);

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: BeforeFirstMotion)));

        Assert.Equal(
            "%\nO1 (T)\nN10 T1 M6\nN20 M98 P100\nN30 T3\nN40 G0 X0. Y0. Z5.\nN50 T3 M6\nN60 Z10.\nN70 M30\nO100\n"
            + "(PART)\nN80 M99\n%\n",
            text);
    }

    // Machine-config 3; virtual machine 3.9, D99: the first motion after a change in a subprogram lies in its caller
    // after the return, where the preload stands, and not in the section that is written once for every caller.
    [Fact]
    public void AutoPreload_BeforeFirstMotionAfterAChangeInASub_StandsBeforeTheMotionOfTheCaller()
    {
        string program = ProgramAndSub(
            ["TOOL=1", "RAPID X=0 Y=0 Z=5", "CALL=100", "RAPID Z=20", "TOOL=7", "RAPID Z=10"], ["TOOL=5"]);

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: BeforeFirstMotion)));

        Assert.Equal(
            "%\nO1 (T)\nN10 T1 M6\nN20 T5\nN30 G0 X0. Y0. Z5.\nN40 M98 P100\nN50 T7\nN60 Z20.\nN70 T7 M6\nN80 Z10.\n"
            + "N90 M30\nO100\nN100 T5 M6\nN110 M99\n%\n",
            text);
    }

    // Virtual machine 3.5: a program that has its own PRELOAD words is left alone by auto_preload.
    [Fact]
    public void AutoPreload_ProgramWithPreloadWords_IsLeftAlone()
    {
        string toolChange = FakeMachines.PlainToolChange + "\nauto_preload = true";
        string program = FakeCompile.Program("TOOL=1", "PRELOAD=5", "RAPID X=0 Y=0 Z=5", "TOOL=3", "TOOL=5");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: toolChange)));

        Assert.Equal(
            "%\nO1 (T)\nN10 T1 M6\nN20 T5\nN30 G0 X0. Y0. Z5.\nN40 T3 M6\nN50 T5 M6\nN60 M30\n%\n",
            text);
    }

    // Machine-config 3; virtual machine 1, 3.5; language 4.1, 4.13: the look-ahead of auto_preload ends with the
    // program of the change, at its PROGRAM=END, where the control rewinds; the last change of the first program
    // preloads no tool of the second.
    [Fact]
    public void AutoPreload_TwoPrograms_LastChangeOfTheFirstPreloadsNoToolOfTheSecond()
    {
        string toolChange = FakeMachines.PlainToolChange + "\nauto_preload = true";
        string program = TwoPrograms(["TOOL=1", "RAPID X=0 Y=0 Z=5"], ["TOOL=7", "RAPID X=0 Y=0 Z=5"]);

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: toolChange)));

        Assert.Equal(
            "%\nO1 (A)\nN10 T1 M6\nN20 G0 X0. Y0. Z5.\nN30 M30\nO2 (B)\nN40 T7 M6\nN50 G0 X0. Y0. Z5.\nN60 M30\n%\n",
            text);
    }

    // Machine-config 3, language 4.13, virtual machine 3.5: a PRELOAD at the head of the next program follows no TOOL
    // block of the program before it, so it is not folded into that program's last change and is written in its own
    // program; that program has PRELOAD words of its own, and the first, which has none, is still left to
    // auto_preload.
    [Fact]
    public void Next_PreloadAtTheHeadOfTheNextProgram_IsNotFoldedAndIsWrittenThere()
    {
        string toolChange = ChangeWithNext + "\nauto_preload = true";
        string program = TwoPrograms(["TOOL=1", "RAPID X=0 Y=0 Z=5", "TOOL=2"], ["PRELOAD=4", "RAPID X=0 Y=0 Z=5"]);

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: toolChange)));

        Assert.Equal(
            "%\nO1 (A)\nN10 T1 M6 A2\nN20 G0 X0. Y0. Z5.\nN30 T2 M6 A0\nN40 M30\nO2 (B)\nN50 T4\nN60 G0 X0. Y0. Z5.\n"
            + "N70 M30\n%\n",
            text);
    }

    // Machine-config 3; virtual machine 3.9, D99: the walk of a subprogram stands inside the program that calls it, so
    // the change of the caller preloads the tool of the subprogram, and the change in the subprogram looks ahead into
    // the rest of its caller, not into the next program.
    [Fact]
    public void AutoPreload_ChangeInACalledSub_LooksAheadInTheCallingProgramOnly()
    {
        string toolChange = FakeMachines.PlainToolChange + "\nauto_preload = true";
        string program = FakeCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"A\" NUMBER=1",
            ProgramHeader,
            "TOOL=1",
            "RAPID X=0 Y=0 Z=5",
            "CALL=100",
            "PROGRAM=END",
            "PROGRAM=BEGIN NAME=\"B\" NUMBER=2",
            ProgramHeader,
            "TOOL=7",
            "RAPID X=0 Y=0 Z=5",
            "PROGRAM=END",
            "SUB=BEGIN NAME=100",
            "TOOL=5",
            "RAPID Z=10",
            "SUB=END",
            "FILE=END");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: toolChange)));

        Assert.Equal(
            "%\nO1 (A)\nN10 T1 M6\nN20 T5\nN30 G0 X0. Y0. Z5.\nN40 M98 P100\nN50 M30\nO100\nN60 T5 M6\nN70 G0 Z10.\n"
            + "N80 M99\nO2 (B)\nN90 T7 M6\nN100 G0 X0. Y0. Z5.\nN110 M30\n%\n",
            text);
    }

    // Machine-config 3, virtual machine 3.5, D91: change_preloaded is written when the tool is preloaded already, a
    // bare TOOL among them; unload for TOOL=0.
    [Fact]
    public void Change_PreloadedToolAndToolZero_WriteChangePreloadedAndUnload()
    {
        string toolChange = FakeMachines.PlainToolChange + "\nchange_preloaded = \"M6\"\nunload = \"T0 M6\"";
        string program = FakeCompile.Program("PRELOAD=4", "TOOL", "RAPID X=0 Y=0 Z=5", "TOOL=0");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(toolChange: toolChange)));

        Assert.Equal("%\nO1 (T)\nN10 T4\nN20 M6\nN30 G0 X0. Y0. Z5.\nN40 T0 M6\nN50 M30\n%\n", text);
    }

    // Machine-config 3: PRELOAD on a machine without a preload template is dropped with a WARNING.
    [Fact]
    public void Preload_MachineWithoutPreloadTemplate_IsDroppedWithCmp011()
    {
        string toolChange = "[tool_change]\nchange = \"T{tool} M6\"";
        string program = FakeCompile.Program("PRELOAD=4", "TOOL=4");

        CompileResult result = FakeCompile.Run(program, FakeMachines.Mill(toolChange: toolChange));

        Assert.Equal("%\nO1 (T)\nN10 T4 M6\nN20 M30\n%\n", FakeCompile.TextOf(result));
        Assert.Equal([DiagnosticCodes.PreloadDropped], FakeCompile.Codes(result).FindAll(IsCompiler));
    }

    // Machine-config introduction: a change command whose {next} has no value is unusable, which the template
    // reports.
    [Fact]
    public void Next_WithoutPreloadAndWithoutAutoPreload_LeavesTheTemplateUnusable()
    {
        string program = FakeCompile.Program("TOOL=1");

        CompileResult result = FakeCompile.Run(program, FakeMachines.Mill(toolChange: ChangeWithNext));

        Assert.Empty(result.Files);
        Assert.Contains("CFG100", FakeCompile.Codes(result));
    }

    private static bool IsCompiler(string code)
    {
        return code.StartsWith("CMP", StringComparison.Ordinal);
    }

    // A file of the programs A (number 1) and B (number 2), each with its header and the given blocks (language 4.13).
    private static string TwoPrograms(string[] first, string[] second)
    {
        var lines = new List<string> { "FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"A\" NUMBER=1", ProgramHeader };
        lines.AddRange(first);
        lines.Add("PROGRAM=END");
        lines.Add("PROGRAM=BEGIN NAME=\"B\" NUMBER=2");
        lines.Add(ProgramHeader);
        lines.AddRange(second);
        lines.Add("PROGRAM=END");
        lines.Add("FILE=END");
        return FakeCompile.Lines(lines.ToArray());
    }

    // A file of the program T (number 1) with its header and the given blocks, and the subprogram 100 with the given
    // blocks (language 4.13).
    private static string ProgramAndSub(string[] program, string[] sub)
    {
        var lines = new List<string> { "FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"T\" NUMBER=1", ProgramHeader };
        lines.AddRange(program);
        lines.Add("PROGRAM=END");
        lines.Add("SUB=BEGIN NAME=100");
        lines.AddRange(sub);
        lines.Add("SUB=END");
        lines.Add("FILE=END");
        return FakeCompile.Lines(lines.ToArray());
    }
}
