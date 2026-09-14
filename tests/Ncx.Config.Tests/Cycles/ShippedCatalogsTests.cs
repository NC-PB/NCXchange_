using System.Globalization;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Config.Tests.Cycles;

/// <summary>
/// The three cycle catalogs of cycles/ (machine-config 6): they load, their drilling entries map the same NCX words
/// and restate the built-in family, the Heidenhain depths turn absolute, the Fanuc turning cycles are modal or carry
/// their contour (phase 2, P2-03).
/// </summary>
public sealed class ShippedCatalogsTests
{
    // The seven words that G81, CYCLE81 and CYCL DEF 200 share (phase 2, P2-03).
    private static readonly string[] s_drillWords =
        ["SURFACE", "CLEARANCE", "DEPTH", "SAFE", "CYCLE_F", "CYCLE_DWELL", "CYCLE_RETRACT"];

    /// <summary>
    /// The three catalog files with the controller family each belongs to.
    /// </summary>
    public static TheoryData<string, Controller> Catalogs => new()
    {
        { "fanuc.toml", Controller.Fanuc },
        { "heidenhain.toml", Controller.Heidenhain },
        { "siemens.toml", Controller.Siemens },
    };

    // P2-03: the three catalogs load, without ERROR and without WARNING.
    [Theory]
    [MemberData(nameof(Catalogs))]
    public void ShippedCatalog_EveryFile_LoadsWithoutErrorOrWarning(string fileName, Controller controller)
    {
        var diagnostics = new Diagnostics("cycles/" + fileName);

        CycleCatalog? catalog = CycleCatalogLoader.Load(CatalogPath(fileName), controller, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(catalog);
        Assert.Equal(controller, catalog.Controller);
    }

    // P2-03 done when: an entry for G81, CYCLE81 and CYCL DEF 200 exists in the three catalogs and maps the same NCX
    // words, SURFACE, CLEARANCE, DEPTH, SAFE, CYCLE_F, CYCLE_DWELL, CYCLE_RETRACT, each by its parameters and the
    // rules of its family (controller-mapping 5).
    [Fact]
    public void Drill_G81Cycle81AndCyclDef200_MapTheSameNcxWords()
    {
        CycleEntry fanuc = NativeEntry("fanuc.toml", Controller.Fanuc, "G81");
        CycleEntry heidenhain = NativeEntry("heidenhain.toml", Controller.Heidenhain, "200");
        CycleEntry siemens = NativeEntry("siemens.toml", Controller.Siemens, "CYCLE81");

        Assert.Equal("DRILL", fanuc.Name);
        Assert.Equal("DRILL", heidenhain.Name);
        Assert.Equal("DRILL", siemens.Name);
        foreach (string word in s_drillWords)
        {
            Assert.Contains(word, fanuc.Words);
            Assert.Contains(word, heidenhain.Words);
            Assert.Contains(word, siemens.Words);
        }

        Assert.Equal(Sorted(fanuc.Words), Sorted(heidenhain.Words));
        Assert.Equal(Sorted(fanuc.Words), Sorted(siemens.Words));
    }

    // P2-03: the Heidenhain entry converts Q203-relative depths to absolute through AbsoluteFromSurface. The values of
    // examples/sources/BOHREN.h (Q200=5, Q201=-21,732, Q203=0, Q204=5) give the R5. and Z-21.732 of BOHREN.fanuc.nc;
    // on a surface at 10 every value moves with it.
    [Fact]
    public void HeidenhainDrill_Q203RelativeValues_BecomeAbsoluteThroughAbsoluteFromSurface()
    {
        CycleEntry drill = NativeEntry("heidenhain.toml", Controller.Heidenhain, "200");

        Assert.Equal(["CLEARANCE", "DEPTH", "SAFE"], drill.AbsoluteFromSurface);
        Assert.Equal(5m, drill.ToNcx("CLEARANCE", 5m, 0m));
        Assert.Equal(-21.732m, drill.ToNcx("DEPTH", -21.732m, 0m));
        Assert.Equal(15m, drill.ToNcx("CLEARANCE", 5m, 10m));
        Assert.Equal(-11.732m, drill.ToNcx("DEPTH", -21.732m, 10m));
        Assert.Equal(15m, drill.ToNcx("SAFE", 5m, 10m));
        Assert.Equal(565.487m, drill.ToNcx("CYCLE_F", 565.487m, 10m));
    }

    // P2-03: the Fanuc turning entries are marked modal: G90, G92 and G94 repeat with every following block that
    // carries X, Z or R, G70..G76 are one-shot (language 4.7.1, fanuc 6).
    [Theory]
    [InlineData("TURN_OD", true)]
    [InlineData("THREAD", true)]
    [InlineData("FACE", true)]
    [InlineData("FINISH", false)]
    [InlineData("ROUGH_TURN", false)]
    [InlineData("ROUGH_FACE", false)]
    [InlineData("GROOVE", false)]
    [InlineData("COMPOUND_THREAD", false)]
    public void FanucTurningCycle_SimpleOrMultipleRepetitive_IsModalOrOneShot(string name, bool modal)
    {
        CycleEntry? entry = Load("fanuc.toml", Controller.Fanuc).Find(name);

        Assert.NotNull(entry);
        Assert.Equal(modal, entry.Modal);
    }

    // Language 4.7.1 and controller-mapping 5: G90 (OD/ID) is one cycle, CYCLE=TURN_OD; the specification wins over the
    // phase file that listed a TURN_ID beside it (implementation README; wave-1 question #17).
    [Fact]
    public void FanucG90_OuterAndInnerDiameter_IsTheOneCycleTurnOd()
    {
        CycleCatalog fanuc = Load("fanuc.toml", Controller.Fanuc);

        var namesOfG90 = new List<string>();
        foreach (CycleEntry entry in fanuc.Entries)
        {
            if (entry.Native == "G90")
            {
                namesOfG90.Add(entry.Name);
            }
        }

        Assert.Equal(["TURN_OD"], namesOfG90);
        Assert.Null(fanuc.Find("TURN_ID"));
    }

    // Siemens 7: "The cycle feed is the modal F", and CYCLE85 has its own FFR. So CYCLE86 (BORE), CYCLE830 (DEEP_HOLE)
    // and CYCLE840 (TAP_COMPENSATING) take CYCLE_F from the modal F as CYCLE81 to CYCLE83 do (wave-1 question #24);
    // CYCLE84 waits for D181.
    [Theory]
    [InlineData("CYCLE81", "F")]
    [InlineData("CYCLE82", "F")]
    [InlineData("CYCLE83", "F")]
    [InlineData("CYCLE85", "FFR")]
    [InlineData("CYCLE86", "F")]
    [InlineData("CYCLE830", "F")]
    [InlineData("CYCLE840", "F")]
    public void SiemensCycleFeed_EveryCycleButCycle84_IsTheModalFOrItsOwnFeed(string native, string feed)
    {
        CycleEntry entry = NativeEntry("siemens.toml", Controller.Siemens, native);

        Assert.Equal(feed, entry.CycleF);
    }

    // P2-03: a G71 entry carries Contour, the P and Q that name the first and the last block of the contour of
    // CONTOUR=name (machine-config 6, D65).
    [Fact]
    public void FanucG71_RoughTurn_CarriesTheContourRange()
    {
        CycleEntry roughTurn = NativeEntry("fanuc.toml", Controller.Fanuc, "G71");

        Assert.Equal("ROUGH_TURN", roughTurn.Name);
        Assert.Equal(["P", "Q"], roughTurn.Contour);
        Assert.Contains("CONTOUR", roughTurn.Words);
    }

    // Machine-config 6: the drilling entries of a catalog file override the built-in family; the shipped files write
    // them again as the family has them, so that a user sees which native cycle carries each one.
    [Theory]
    [MemberData(nameof(Catalogs))]
    public void ShippedCatalog_DrillingEntries_RestateTheBuiltInFamily(string fileName, Controller controller)
    {
        CycleCatalog shipped = Load(fileName, controller);
        CycleCatalog builtIn = DrillingFamily.Catalog(controller);

        foreach (string name in DrillingFamily.Names)
        {
            CycleEntry? inFile = shipped.Find(name);
            CycleEntry? inCode = builtIn.Find(name);
            Assert.NotNull(inFile);
            Assert.NotNull(inCode);
            Assert.Equal(inCode.Native, inFile.Native);
            Assert.Equal(Pairs(inCode.Params), Pairs(inFile.Params));
            Assert.Equal(inCode.AbsoluteFromSurface, inFile.AbsoluteFromSurface);
            Assert.Equal(inCode.Modal, inFile.Modal);
            Assert.Equal(inCode.Signature, inFile.Signature);
            Assert.Equal(Pairs(inCode.Fixed), Pairs(inFile.Fixed));
            Assert.Equal(inCode.RuleWords, inFile.RuleWords);
        }
    }

    // Machine-config 6, controller-mapping 5: the Siemens reader takes PECK for CYCLE83 with VARI=1 and CHIP_BREAK
    // with VARI=0.
    [Fact]
    public void SiemensCycle83_FixedVari_TellsPeckFromChipBreak()
    {
        CycleCatalog siemens = Load("siemens.toml", Controller.Siemens);

        Assert.Equal("PECK", siemens.FindNative("CYCLE83", Values("VARI", 1m))?.Name);
        Assert.Equal("CHIP_BREAK", siemens.FindNative("CYCLE83", Values("VARI", 0m))?.Name);
    }

    // Language 4.7.1, D94: CYCLE:HEIDENHAIN=251 passes through the Heidenhain catalog, where cycle 251 maps its cycle
    // words, and through no other.
    [Fact]
    public void NativeCycle_Heidenhain251_PassesThroughTheHeidenhainCatalogOnly()
    {
        CycleCatalog heidenhain = Load("heidenhain.toml", Controller.Heidenhain);

        Assert.True(heidenhain.PassesThrough("HEIDENHAIN"));
        Assert.Equal("RECT_POCKET", heidenhain.FindNative("251")?.Name);
        Assert.Equal("Q201", heidenhain.FindNative("251")?.NativeOf("DEPTH"));
        Assert.False(Load("fanuc.toml", Controller.Fanuc).PassesThrough("HEIDENHAIN"));
        Assert.False(Load("siemens.toml", Controller.Siemens).PassesThrough("HEIDENHAIN"));
    }

    // The catalogs live in cycles/ of the repository, found from the test assembly (tests/README.md).
    private static string CatalogPath(string fileName)
    {
        return Path.Combine(Fixture.RepositoryRoot(), "cycles", fileName);
    }

    // Loads a shipped catalog that loads without ERROR.
    private static CycleCatalog Load(string fileName, Controller controller)
    {
        var diagnostics = new Diagnostics("cycles/" + fileName);
        CycleCatalog? catalog = CycleCatalogLoader.Load(CatalogPath(fileName), controller, diagnostics);
        Assert.NotNull(catalog);
        return catalog;
    }

    // The entry of a shipped catalog that a reader finds for a native cycle.
    private static CycleEntry NativeEntry(string fileName, Controller controller, string native)
    {
        CycleEntry? entry = Load(fileName, controller).FindNative(native);
        Assert.NotNull(entry);
        return entry;
    }

    // The native values of a source block with one parameter.
    private static Dictionary<string, decimal> Values(string native, decimal value)
    {
        return new Dictionary<string, decimal> { [native] = value };
    }

    // A list of words sorted, so that two entries compare by their words and not by their order.
    private static List<string> Sorted(IReadOnlyList<string> words)
    {
        var sorted = new List<string>(words);
        sorted.Sort(StringComparer.Ordinal);
        return sorted;
    }

    // A table as sorted KEY=VALUE lines, so that two tables compare by their content and not by their order.
    private static List<string> Pairs(IReadOnlyDictionary<string, string> table)
    {
        var pairs = new List<string>();
        foreach (KeyValuePair<string, string> pair in table)
        {
            pairs.Add(pair.Key + "=" + pair.Value);
        }

        pairs.Sort(StringComparer.Ordinal);
        return pairs;
    }

    // The fixed values as sorted KEY=VALUE lines.
    private static List<string> Pairs(IReadOnlyDictionary<string, decimal> table)
    {
        var pairs = new List<string>();
        foreach (KeyValuePair<string, decimal> pair in table)
        {
            pairs.Add(pair.Key + "=" + pair.Value.ToString(CultureInfo.InvariantCulture));
        }

        pairs.Sort(StringComparer.Ordinal);
        return pairs;
    }
}
