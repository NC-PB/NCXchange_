using static Ncx.Readers.Tests.Siemens.SiemensRead;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// The channels and the program groups (controllers siemens.md 9, 11 rule 7; controller-mapping 5, 7; language 4.14).
/// </summary>
public sealed class SiemensChannelTests
{
    // Rule 7: WAITM(mark, channels) is SYNC with WITH.
    [Fact]
    public void Waitm_IsSyncWithTheChannels()
    {
        Assert.Equal(Lines("SYNC=1 WITH=1,2"), CheckedBody("WAITM(1,1,2)"));
    }

    // Rule 7: INIT and START are START_CHANNEL with the NAME of the program INIT selects, WAITE is WAIT_CHANNEL.
    [Fact]
    public void InitStartAndWaite_AreTheChannelWords()
    {
        Assert.Equal(Lines("NAME=\"CHAN2\" START_CHANNEL=2", "WAIT_CHANNEL=2"),
            CheckedBody("INIT(2,\"CHAN2\")\nSTART(2)\nWAITE(2)"));
    }

    // GROUP_BEGIN of the program groups is a SECTION with its name, GROUP_END stays RAW (controller-mapping 5).
    [Fact]
    public void GroupBegin_IsASection()
    {
        Assert.Equal(Lines("SECTION=\"DRILL\"", "RAPID X=0", Raw("GROUP_END(0,0)")),
            CheckedBody("GROUP_BEGIN(0,\"DRILL\",0,0)\nG0 X0\nGROUP_END(0,0)"));
    }
}
