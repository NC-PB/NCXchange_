namespace Ncx.Config.Tests;

/// <summary>
/// The known key an unknown key of a machine file is most likely a typo of (P2-01).
/// </summary>
public sealed class NearestKeyTests
{
    // P2-01: [formt] suggests [format].
    [Fact]
    public void Find_Formt_IsFormat()
    {
        Assert.Equal("format", NearestKey.Find("formt", ["machine", "format", "tool_change", "home", "setpos"]));
    }

    // TOML keys are case-sensitive; a key written in other capitals is nearest to itself in the known spelling.
    [Fact]
    public void Find_OtherCapitals_IsTheKnownSpelling()
    {
        Assert.Equal("rpm_max", NearestKey.Find("RPM_MAX", ["CW", "rpm_min", "rpm_max"]));
    }

    // Two known keys at the same distance: the first in the order of the specification.
    [Fact]
    public void Find_TwoKeysAtTheSameDistance_IsTheFirst()
    {
        Assert.Equal("ON", NearestKey.Find("OX", ["ON", "OFF", "OR"]));
    }
}
