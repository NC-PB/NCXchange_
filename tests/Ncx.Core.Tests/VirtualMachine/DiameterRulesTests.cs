using Ncx.Core.Model;
using Ncx.Core.Tests.Parsing;
using Ncx.Core.VirtualMachine;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The word set of diameter programming (language 4.2, D60): X, IX, the absolute CENTER:X and the X words of an AXIS=X
/// cycle are halved under DIAMETER=ON; every other radial distance is a radius value.
/// </summary>
public sealed class DiameterRulesTests
{
    // D60: X and IX are halved.
    [Fact]
    public void XAndIX_UnderDiameterOn_AreHalved()
    {
        Block block = ParseText.CleanBlock("LINE X=40 IX=10");

        Assert.Equal(20m, Radius(block, "X", 40m, cycleAxis: null));
        Assert.Equal(5m, Radius(block, "IX", 10m, cycleAxis: null));
    }

    // D60: the absolute CENTER:X is halved, CENTER:IX is relative to the start point and a radius value.
    [Fact]
    public void CenterX_UnderDiameterOn_AbsoluteIsHalvedIncrementalIsARadius()
    {
        Block block = ParseText.CleanBlock("ARC=CW X=10 Z=0 CENTER:X=40 CENTER:IX=4");

        Assert.True(DiameterRules.IsDiameterWord(block.Find("CENTER", "X")!, cycleAxis: null));
        Assert.False(DiameterRules.IsDiameterWord(block.Find("CENTER", "IX")!, cycleAxis: null));
    }

    // D60: R and every other radial distance stay radius values, and Y and Z are never diameters.
    [Fact]
    public void ROtherRadialDistancesAndOtherAxes_UnderDiameterOn_StayAsWritten()
    {
        Block block = ParseText.CleanBlock("ARC=CW X=10 Y=2 Z=0 R=5");

        Assert.Equal(5m, Radius(block, "R", 5m, cycleAxis: null));
        Assert.Equal(2m, Radius(block, "Y", 2m, cycleAxis: null));
        Assert.Equal(0m, Radius(block, "Z", 0m, cycleAxis: null));
    }

    // D59, D60, VM 3.3: the coordinates along the drilling axis of an AXIS=X cycle are X values, halved; PECK is a
    // radial distance.
    [Fact]
    public void CycleWords_AxisXCycleUnderDiameterOn_TheDrillingAxisCoordinatesAreHalved()
    {
        Block block = ParseText.CleanBlock("CYCLE=DRILL AXIS=X SURFACE=50 CLEARANCE=54 DEPTH=30 SAFE=60 PECK=2");

        Assert.Equal(25m, Radius(block, "SURFACE", 50m, cycleAxis: "X"));
        Assert.Equal(27m, Radius(block, "CLEARANCE", 54m, cycleAxis: "X"));
        Assert.Equal(15m, Radius(block, "DEPTH", 30m, cycleAxis: "X"));
        Assert.Equal(30m, Radius(block, "SAFE", 60m, cycleAxis: "X"));
        Assert.Equal(2m, Radius(block, "PECK", 2m, cycleAxis: "X"));
    }

    // D60: along Z the cycle words are no X values.
    [Fact]
    public void CycleWords_AxisZCycleUnderDiameterOn_StayAsWritten()
    {
        Block block = ParseText.CleanBlock("CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-15");

        Assert.Equal(-15m, Radius(block, "DEPTH", -15m, cycleAxis: "Z"));
    }

    // Language 4.2: DIAMETER=OFF, the default, halves nothing.
    [Fact]
    public void X_UnderDiameterOff_StaysAsWritten()
    {
        Block block = ParseText.CleanBlock("LINE X=40");

        Assert.Equal(40m, DiameterRules.ToRadius(block.Find("X")!, 40m, diameterOn: false, cycleAxis: null));
    }

    private static decimal Radius(Block block, string key, decimal value, string? cycleAxis)
    {
        return DiameterRules.ToRadius(block.Find(key)!, value, diameterOn: true, cycleAxis);
    }
}
