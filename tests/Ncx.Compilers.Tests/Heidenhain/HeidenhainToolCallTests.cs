namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// TOOL CALL and TOOL DEF (controllers heidenhain.md 4; 8 rule 3; controller-mapping 3; machine-config 3; virtual
/// machine 3.5).
/// </summary>
public sealed class HeidenhainToolCallTests
{
    // Heidenhain 8 rule 3: TOOL CALL n axis S assembled from TOOL, WORKPLANE and the RPM of the same block; the
    // offsets come with the call (offsets_with_change); TOOL DEF n for PRELOAD.
    [Fact]
    public void Rule3_ToolCall_IsAssembledFromToolWorkplaneAndTheRpmOfTheSameBlock()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1 RPM=1592", "PRELOAD=0", "SPINDLE=CW"));

        Assert.Equal("TOOL CALL 1 Z S1592\nTOOL DEF 0\nM3", HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 3: the RPM of the following block is folded into the TOOL CALL, and not written again.
    [Fact]
    public void Rule3_ToolCall_TakesTheRpmOfTheFollowingBlock()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TOOL=1", "SPINDLE=CW RPM=10000"));

        Assert.Equal("TOOL CALL 1 Z S10000\nM3", HeidenhainCompile.Body(result));
    }

    // The TODO(question) of HeidenhainToolCall.FollowingBlock: the block after a PRELOAD that TOOL DEF writes right
    // after the call is the following block (Expected/BOHREN.ncx: TOOL=1, PRELOAD=2, SPINDLE=CW RPM=10000).
    [Fact]
    public void Rule3_ToolCall_TakesTheRpmOfTheBlockAfterItsPreload()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOOL=1", "PRELOAD=2", "SPINDLE=CW RPM=10000"));

        Assert.Equal("TOOL CALL 1 Z S10000\nTOOL DEF 2\nM3", HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 3, machine-config 3: without an RPM in the same or the following block the call keeps the
    // speed the spindle has.
    [Fact]
    public void Rule3_ToolCallWithoutRpm_KeepsTheSpeedOfTheSpindle()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TOOL=1 RPM=500", "TOOL=2"));

        Assert.Equal("TOOL CALL 1 Z S500\nTOOL CALL 2 Z S500", HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 3; controller-mapping 1, WORKPLANE: the tool axis of the call is the one of the working plane,
    // Y of ZX.
    [Fact]
    public void Rule3_WorkplaneOfTheToolBlock_IsTheToolAxisOfTheCall()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TOOL=1 RPM=500 WORKPLANE=ZX"));

        Assert.Equal("TOOL CALL 1 Y S500", HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 3; language 4.4: PRELOAD=5 is TOOL DEF 5, and a bare TOOL calls the preloaded tool by its
    // number (D91).
    [Fact]
    public void Rule3_PreloadAndBareTool_AreToolDefAndTheCallOfThePreloadedTool()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("PRELOAD=3", "TOOL RPM=800"));

        Assert.Equal("TOOL DEF 3\nTOOL CALL 3 Z S800", HeidenhainCompile.Body(result));
    }

    // Controller-mapping 3, TOOL=0; machine-config 3, unload: TOOL CALL 0 empties the spindle.
    [Fact]
    public void Rule3_ToolZero_IsTheUnloadTemplate()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TOOL=1 RPM=500", "TOOL=0"));

        Assert.Equal("TOOL CALL 1 Z S500\nTOOL CALL 0", HeidenhainCompile.Body(result));
    }

    // Machine-config 3, offsets_with_change; language 2 rule 4: the length and the radius come with the TOOL CALL, so
    // the OFFSET words write nothing.
    [Fact]
    public void Rule3_Offsets_AreImplicitInTheToolCall()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOOL=1 RPM=500", "RAPID X=0 Y=0 Z=5 OFFSET:LEN=1"));

        Assert.Equal("TOOL CALL 1 Z S500\nL X+0 Y+0 Z+5 R0 FMAX", HeidenhainCompile.Body(result));
    }

    // Virtual machine 3.8 rule 2a; D180: a speed without a tool is the RPM template of the spindle, TOOL CALL S2000 on
    // the iTNC 530 of the repository, written where the speed changes.
    [Fact]
    public void SpeedWithoutTool_IsTheRpmTemplateOfTheSpindle()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOOL=1 RPM=500", "RAPID X=0 Y=0", "RPM=2000", "RPM=2000"));

        Assert.Equal("TOOL CALL 1 Z S500\nL X+0 Y+0 R0 FMAX\nTOOL CALL S2000", HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 3: the WORKPLANE of the block that follows the TOOL block is folded into the TOOL CALL.
    [Fact]
    public void Rule3_WorkplaneOfTheFollowingBlock_IsTheToolAxisOfTheCall()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TOOL=1 RPM=500", "WORKPLANE=ZX"));

        Assert.Equal("TOOL CALL 1 Y S500", HeidenhainCompile.Body(result));
    }

    // The TODO(question) of HeidenhainToolCall.CheckWorkplane: a WORKPLANE that changes the tool axis of the last TOOL
    // CALL without a TOOL, after a motion, is CMP110.
    [Fact]
    public void Workplane_ThatChangesTheToolAxisOfTheLastCall_IsTheErrorCmp110()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOOL=1 RPM=500", "RAPID X=0 Y=0", "WORKPLANE=ZX"));

        Assert.Empty(result.Files);
        Assert.Equal(6, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainWorkplaneWithoutToolCall).Line);
    }

    // A SKIP block that changes a tool cannot carry its / in front of the TOOL CALL of the framework, CMP111.
    [Fact]
    public void Skip_OnAToolChange_IsTheErrorCmp111()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("SKIP TOOL=1 RPM=500"));

        Assert.Empty(result.Files);
        Assert.Equal(4, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainSkipOnToolChange).Line);
    }
}
