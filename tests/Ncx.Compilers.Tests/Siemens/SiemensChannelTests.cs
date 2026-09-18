using static Ncx.Compilers.Tests.Siemens.SiemensCompile;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// Rule 6 of controllers siemens.md 12 and the Siemens column of controller-mapping 7: WAITM from [sync], START and
/// WAITE, INIT for the program START selects.
/// </summary>
public sealed class SiemensChannelTests
{
    // The mill-turn with two channels, whose [sync] templates are the generic 840D sl ones (millturn1.toml).
    private static readonly Lazy<Ncx.Core.Machine.MachineConfig> s_twoChannels =
        new(() => Replaced(MillTurnFile, "channels = [1]", "channels = [1, 2]"));

    // Siemens 12 rule 6: WAITM(mark, channels) from the wait template of [sync], the channels of WITH as 1,2; without
    // WITH every channel of the machine (language 4.8, WITH).
    [Fact]
    public void Sync_WithAndWithoutWith_IsWaitmWithTheChannels()
    {
        CompileResult result = Run(Program(MillTurnHeader, "SYNC=10 WITH=1,2", "SYNC=11"), s_twoChannels.Value);

        Assert.Equal(Lines("WAITM(10,1,2)", "WAITM(11,1,2)"), Body(result));
    }

    // Machine-config 5, mark_range: a mark outside it has no wait on this machine, an ERROR.
    [Fact]
    public void Sync_OutsideTheMarkRange_IsAnError()
    {
        Assert.Equal(["CMP610"], CompilerCodes(Run(Program(MillTurnHeader, "SYNC=100 WITH=1,2"),
            s_twoChannels.Value)));
    }

    // Controller-mapping 7, START_CHANNEL and WAIT_CHANNEL: INIT selects the program NAME names, START starts the
    // channel, WAITE waits for its end (controllers siemens.md 9).
    [Fact]
    public void StartAndWaitChannel_AreInitStartAndWaite()
    {
        Assert.Equal(Lines("INIT(2,\"_N_SHAFT2_MPF\",\"S\")", "START(2)", "WAITE(2)"),
            Body(Run(Program(MillTurnHeader, "START_CHANNEL=2 NAME=\"SHAFT2\"", "WAIT_CHANNEL=2"),
                s_twoChannels.Value)));
    }
}
