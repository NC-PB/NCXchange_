using Ncx.Core.Machine;

namespace Ncx.Core.Tests.Machine;

/// <summary>
/// The built-in drilling family of language 4.7, defined in code per controller family as controller-mapping 5 maps
/// it (machine-config 6, phase 2 P2-03).
/// </summary>
public sealed class DrillingFamilyTests
{
    // The seven words that G81, CYCLE81 and CYCL DEF 200 share (phase 2, P2-03; language 4.7).
    private static readonly string[] s_drillWords =
        ["SURFACE", "CLEARANCE", "DEPTH", "SAFE", "CYCLE_F", "CYCLE_DWELL", "CYCLE_RETRACT"];

    private static readonly Controller[] s_families = [Controller.Fanuc, Controller.Heidenhain, Controller.Siemens];

    /// <summary>
    /// The three controller families of machine-config 1 for the theories.
    /// </summary>
    public static TheoryData<Controller> Families => new(s_families);

    // Language 4.7: the built-in names.
    [Fact]
    public void Names_BuiltInFamily_AreTheSevenNamesOfLanguage47()
    {
        Assert.Equal(["DRILL", "DRILL_DWELL", "PECK", "CHIP_BREAK", "TAP", "REAM", "BORE"], DrillingFamily.Names);
    }

    // Machine-config 6: the family is defined for every controller family, one entry per built-in name.
    [Theory]
    [MemberData(nameof(Families))]
    public void Catalog_EveryControllerFamily_HasOneEntryPerBuiltInName(Controller controller)
    {
        CycleCatalog catalog = DrillingFamily.Catalog(controller);

        Assert.Equal(controller, catalog.Controller);
        var names = new List<string>();
        foreach (CycleEntry entry in catalog.Entries)
        {
            names.Add(entry.Name);
        }

        Assert.Equal(DrillingFamily.Names, names);
    }

    // Controller-mapping 5: the native cycle of every built-in name in every family.
    [Theory]
    [InlineData(Controller.Fanuc, "DRILL", "G81")]
    [InlineData(Controller.Fanuc, "DRILL_DWELL", "G82")]
    [InlineData(Controller.Fanuc, "PECK", "G83")]
    [InlineData(Controller.Fanuc, "CHIP_BREAK", "G73")]
    [InlineData(Controller.Fanuc, "TAP", "G84")]
    [InlineData(Controller.Fanuc, "REAM", "G85")]
    [InlineData(Controller.Fanuc, "BORE", "G86")]
    [InlineData(Controller.Heidenhain, "DRILL", "200")]
    [InlineData(Controller.Heidenhain, "DRILL_DWELL", "200")]
    [InlineData(Controller.Heidenhain, "PECK", "203")]
    [InlineData(Controller.Heidenhain, "CHIP_BREAK", "203")]
    [InlineData(Controller.Heidenhain, "TAP", "207")]
    [InlineData(Controller.Heidenhain, "REAM", "201")]
    [InlineData(Controller.Heidenhain, "BORE", "202")]
    [InlineData(Controller.Siemens, "DRILL", "CYCLE81")]
    [InlineData(Controller.Siemens, "DRILL_DWELL", "CYCLE82")]
    [InlineData(Controller.Siemens, "PECK", "CYCLE83")]
    [InlineData(Controller.Siemens, "CHIP_BREAK", "CYCLE83")]
    [InlineData(Controller.Siemens, "TAP", "CYCLE84")]
    [InlineData(Controller.Siemens, "REAM", "CYCLE85")]
    [InlineData(Controller.Siemens, "BORE", "CYCLE86")]
    public void Catalog_BuiltInName_MapsToTheNativeCycleOfControllerMapping5(
        Controller controller, string name, string native)
    {
        Assert.Equal(native, DrillingFamily.Catalog(controller).Find(name)?.Native);
    }

    // Phase 2, P2-03: G81, CYCL DEF 200 and CYCLE81 carry the same NCX words, each with its parameters and the rules
    // of its family (language 4.7, controller-mapping 5).
    [Fact]
    public void Catalog_DrillOfEveryFamily_CarriesTheSameNcxWords()
    {
        List<string> fanuc = SortedWords(Controller.Fanuc, "DRILL");

        foreach (string word in s_drillWords)
        {
            Assert.Contains(word, fanuc);
        }

        Assert.Equal(fanuc, SortedWords(Controller.Heidenhain, "DRILL"));
        Assert.Equal(fanuc, SortedWords(Controller.Siemens, "DRILL"));
    }

    // Fanuc 6: the cycle block defines and calls, and every following block with a position calls again; Heidenhain
    // and Siemens call with words of their own and leave modal out (machine-config 6).
    [Theory]
    [MemberData(nameof(Families))]
    public void Catalog_Entries_AreModalOnFanucOnly(Controller controller)
    {
        foreach (CycleEntry entry in DrillingFamily.Catalog(controller).Entries)
        {
            Assert.Equal(controller == Controller.Fanuc, entry.Modal);
        }
    }

    // Heidenhain 5, controller-mapping 5: CLEARANCE = Q203 + Q200, DEPTH = Q203 + Q201, SAFE = Q203 + Q204.
    [Fact]
    public void Catalog_HeidenhainDrill_TakesClearanceDepthAndSafeFromTheSurface()
    {
        CycleEntry? drill = DrillingFamily.Catalog(Controller.Heidenhain).Find("DRILL");

        Assert.NotNull(drill);
        Assert.Equal(["CLEARANCE", "DEPTH", "SAFE"], drill.AbsoluteFromSurface);
        Assert.Equal("Q203", drill.NativeOf("SURFACE"));
        Assert.Equal(-21.732m, drill.ToNcx("DEPTH", -21.732m, 0m));
    }

    // Siemens 7, controller-mapping 5: SDIS is unsigned and added to RFP, so CLEARANCE = RFP + SDIS; DP is absolute.
    [Fact]
    public void Catalog_SiemensDrill_TakesClearanceFromTheReferencePlane()
    {
        CycleEntry? drill = DrillingFamily.Catalog(Controller.Siemens).Find("DRILL");

        Assert.NotNull(drill);
        Assert.Equal(["CLEARANCE"], drill.AbsoluteFromSurface);
        Assert.Equal(11m, drill.ToNcx("CLEARANCE", 5m, 6m));
        Assert.Equal(-50m, drill.ToNcx("DEPTH", -50m, 6m));
    }

    // Controller-mapping 5: CYCLE83 with VARI=1 is PECK, with VARI=0 CHIP_BREAK.
    [Fact]
    public void Catalog_SiemensPeckAndChipBreak_AreToldApartByVari()
    {
        CycleCatalog siemens = DrillingFamily.Catalog(Controller.Siemens);

        Assert.Equal(1m, siemens.Find("PECK")?.Fixed["VARI"]);
        Assert.Equal(0m, siemens.Find("CHIP_BREAK")?.Fixed["VARI"]);
    }

    // Siemens 7: "The cycle feed is the modal F", CYCLE85 has its own FFR; so BORE on CYCLE86 takes CYCLE_F from the
    // modal F as DRILL, DRILL_DWELL, PECK and CHIP_BREAK do, as an address word before the call and no position of
    // the signature (machine-config 6; wave-1 question #24). TAP on CYCLE84 waits for D181.
    [Theory]
    [InlineData("DRILL", "F")]
    [InlineData("DRILL_DWELL", "F")]
    [InlineData("PECK", "F")]
    [InlineData("CHIP_BREAK", "F")]
    [InlineData("REAM", "FFR")]
    [InlineData("BORE", "F")]
    public void Catalog_SiemensCycleFeed_IsTheModalFOrTheOwnFeedOfCycle85(string name, string feed)
    {
        CycleEntry? entry = DrillingFamily.Catalog(Controller.Siemens).Find(name);

        Assert.NotNull(entry);
        Assert.Equal(feed, entry.CycleF);
    }

    // Controller-mapping 5: the Fanuc pitch is F / S, or F in the per-revolution mode; no address carries PITCH, a
    // rule of the reader and the compiler does.
    [Fact]
    public void Catalog_FanucTap_CarriesPitchByARule()
    {
        CycleEntry? tap = DrillingFamily.Catalog(Controller.Fanuc).Find("TAP");

        Assert.NotNull(tap);
        Assert.Null(tap.NativeOf("PITCH"));
        Assert.Contains("PITCH", tap.RuleWords);
        Assert.Contains("PITCH", tap.Words);
    }

    // The NCX words of an entry of the family, sorted.
    private static List<string> SortedWords(Controller controller, string name)
    {
        CycleEntry? entry = DrillingFamily.Catalog(controller).Find(name);
        Assert.NotNull(entry);
        var words = new List<string>(entry.Words);
        words.Sort(StringComparer.Ordinal);
        return words;
    }
}
