using Ncx.Core.Machine;

namespace Ncx.Core.Tests.Machine;

/// <summary>
/// One entry of a cycle catalog: the NCX words it carries, where its native parameters stand, the values the native
/// cycle gives relative to the surface, and how a later entry of the same name overrides it (machine-config 6).
/// </summary>
public sealed class CycleEntryTests
{
    // Machine-config 6: native Q201 is relative to Q203, NCX DEPTH is absolute; heidenhain 5: DEPTH = Q203 + Q201 and
    // CLEARANCE = Q203 + Q200.
    [Fact]
    public void ToNcx_WordOfAbsoluteFromSurface_AddsTheSurface()
    {
        CycleEntry drill = HeidenhainDrill();

        Assert.Equal(-15m, drill.ToNcx("DEPTH", -20m, 5m));
        Assert.Equal(7m, drill.ToNcx("CLEARANCE", 2m, 5m));
    }

    // Machine-config 6: only the words of absolute_from_surface are relative to the surface.
    [Fact]
    public void ToNcx_WordOutsideAbsoluteFromSurface_KeepsItsValue()
    {
        CycleEntry drill = HeidenhainDrill();

        Assert.Equal(565m, drill.ToNcx("CYCLE_F", 565m, 5m));
        Assert.Equal(5m, drill.ToNcx("SURFACE", 5m, 5m));
    }

    // The compiler writes the value back relative to the surface, Q201 = DEPTH - Q203 (machine-config 6, heidenhain
    // 8.7).
    [Fact]
    public void ToNative_WordOfAbsoluteFromSurface_SubtractsTheSurface()
    {
        CycleEntry drill = HeidenhainDrill();

        Assert.Equal(-21.732m, drill.ToNative("DEPTH", -21.732m, 0m));
        Assert.Equal(-20m, drill.ToNative("DEPTH", -15m, 5m));
        Assert.Equal(565m, drill.ToNative("CYCLE_F", 565m, 5m));
    }

    // Machine-config 6: a native name in params that the signature does not list is an address word of its own; the
    // CYCLE_F of CYCLE81 is the modal F before the call (controller-mapping 5).
    [Fact]
    public void IsAddressWord_SiemensModalF_StandsOutsideTheSignature()
    {
        CycleEntry drill = SiemensDrill();

        Assert.Equal("F", drill.CycleF);
        Assert.Equal("DTB", drill.CycleDwell);
        Assert.True(drill.IsAddressWord("CYCLE_F"));
        Assert.False(drill.IsAddressWord("DEPTH"));
    }

    // Machine-config 6: Fanuc entries leave the signature out, because their parameters are address words of the
    // cycle block; a word the entry does not map stands nowhere.
    [Fact]
    public void IsAddressWord_EntryWithoutSignature_EveryMappedWordIsAnAddressWord()
    {
        var drill = new CycleEntry
        {
            Name = "DRILL",
            Native = "G81",
            Params = new Dictionary<string, string> { ["DEPTH"] = "Z", ["CYCLE_F"] = "F" },
        };

        Assert.True(drill.IsAddressWord("DEPTH"));
        Assert.True(drill.IsAddressWord("CYCLE_F"));
        Assert.False(drill.IsAddressWord("PECK"));
    }

    // Controller-mapping 5, AXIS: CYCLE83 carries _AXN for the drilling axis, which is exactly AXIS; cycle 200 drills
    // along the tool axis of the TOOL CALL and no parameter carries it.
    [Fact]
    public void Axis_SiemensAxn_IsTheNativeNameOfAxis()
    {
        var peck = new CycleEntry
        {
            Name = "PECK",
            Native = "CYCLE83",
            Params = new Dictionary<string, string> { ["DEPTH"] = "DP", ["AXIS"] = "_AXN" },
        };

        Assert.Equal("_AXN", peck.Axis);
        Assert.Null(HeidenhainDrill().Axis);
    }

    // The NCX words of an entry: the words its params map, the words a rule of its controller family carries, and
    // CONTOUR when it carries a contour (machine-config 6, language 4.7, D65).
    [Fact]
    public void Words_ParamsRuleWordsAndContour_AreTheWordsOfTheEntry()
    {
        var roughTurn = new CycleEntry
        {
            Name = "ROUGH_TURN",
            Native = "G71",
            Params = new Dictionary<string, string> { ["CYCLE_F"] = "F" },
            Contour = ["P", "Q"],
            RuleWords = ["AXIS"],
        };

        Assert.Equal(["CYCLE_F", "AXIS", "CONTOUR"], roughTurn.Words);
    }

    // A word that a rule carries and params maps as well is one word of the entry.
    [Fact]
    public void Words_RuleWordThatParamsMapsToo_IsListedOnce()
    {
        var drill = new CycleEntry
        {
            Name = "DRILL",
            Native = "G81",
            Params = new Dictionary<string, string> { ["DEPTH"] = "Z" },
            RuleWords = ["DEPTH", "SAFE"],
        };

        Assert.Equal(["DEPTH", "SAFE"], drill.Words);
    }

    // The native name that carries a word, or nothing for a word the entry does not map (machine-config 6).
    [Fact]
    public void NativeOf_MappedAndUnmappedWord_GivesTheNativeNameOrNull()
    {
        CycleEntry drill = HeidenhainDrill();

        Assert.Equal("Q201", drill.NativeOf("DEPTH"));
        Assert.Null(drill.NativeOf("PITCH"));
    }

    // Machine-config 6 and 5a: an entry written again overrides the keys it writes and keeps the others; the Doosan
    // PECK writes name, native and pre only.
    [Fact]
    public void OverriddenBy_EntryWritingNameNativeAndPre_KeepsTheOtherKeys()
    {
        var machineEntry = new CycleEntry
        {
            Name = "PECK",
            Native = "G83",
            Rule = new ExpansionRule { Pre = ["FUNC:PECK_MODE=RETRACT"] },
            WrittenKeys = ["name", "native", "pre"],
        };

        CycleEntry peck = FanucPeck().OverriddenBy(machineEntry);

        Assert.Equal("G83", peck.Native);
        Assert.Equal("Q", peck.NativeOf("PECK"));
        Assert.True(peck.Modal);
        Assert.Equal(["FUNC:PECK_MODE=RETRACT"], peck.Rule?.Pre);
        Assert.Equal(["SURFACE", "SAFE", "CYCLE_RETRACT", "AXIS"], peck.RuleWords);
    }

    // A key the later entry writes replaces the earlier value even when it writes the default, modal = false.
    [Fact]
    public void OverriddenBy_EntryWritingModalFalse_IsNoLongerModal()
    {
        var machineEntry = new CycleEntry
        {
            Name = "PECK",
            Native = "G83",
            WrittenKeys = ["name", "native", "modal"],
        };

        CycleEntry peck = FanucPeck().OverriddenBy(machineEntry);

        Assert.False(peck.Modal);
        Assert.Equal("Z", peck.NativeOf("DEPTH"));
    }

    // The four keys of the expansion rule override one by one like the other keys (machine-config 5a, 6).
    [Fact]
    public void OverriddenBy_OneRuleKey_KeepsTheOtherRuleKeys()
    {
        CycleEntry catalogEntry = FanucPeck() with
        {
            Rule = new ExpansionRule { Pre = ["FUNC:PECK_MODE=RETRACT"] },
        };
        var machineEntry = new CycleEntry
        {
            Name = "PECK",
            Native = "G83",
            Rule = new ExpansionRule { Post = ["FUNC:CHIP_CONVEYOR=ON"] },
            WrittenKeys = ["name", "native", "post"],
        };

        ExpansionRule? rule = catalogEntry.OverriddenBy(machineEntry).Rule;

        Assert.NotNull(rule);
        Assert.Equal(["FUNC:PECK_MODE=RETRACT"], rule.Pre);
        Assert.Equal(["FUNC:CHIP_CONVEYOR=ON"], rule.Post);
    }

    // Cycle 200 as the Heidenhain catalog writes it (heidenhain 5, examples/sources/BOHREN.h).
    private static CycleEntry HeidenhainDrill()
    {
        return new CycleEntry
        {
            Name = "DRILL",
            Native = "200",
            Signature = ["Q200", "Q201", "Q206", "Q202", "Q210", "Q203", "Q204", "Q211"],
            Params = new Dictionary<string, string>
            {
                ["CLEARANCE"] = "Q200",
                ["DEPTH"] = "Q201",
                ["CYCLE_F"] = "Q206",
                ["SURFACE"] = "Q203",
                ["SAFE"] = "Q204",
                ["CYCLE_DWELL"] = "Q211",
            },
            AbsoluteFromSurface = ["CLEARANCE", "DEPTH", "SAFE"],
        };
    }

    // CYCLE81 as the Siemens catalog writes it (siemens 7, controller-mapping 5).
    private static CycleEntry SiemensDrill()
    {
        return new CycleEntry
        {
            Name = "DRILL",
            Native = "CYCLE81",
            Signature = ["RTP", "RFP", "SDIS", "DP", "DPR", "DTB", "_GMODE", "_DMODE", "_AMODE"],
            Params = new Dictionary<string, string>
            {
                ["SAFE"] = "RTP",
                ["SURFACE"] = "RFP",
                ["CLEARANCE"] = "SDIS",
                ["DEPTH"] = "DP",
                ["CYCLE_DWELL"] = "DTB",
                ["CYCLE_F"] = "F",
            },
            AbsoluteFromSurface = ["CLEARANCE"],
        };
    }

    // G83 as the Fanuc catalog writes it, with the words of the Fanuc rules (fanuc 6, controller-mapping 5).
    private static CycleEntry FanucPeck()
    {
        return new CycleEntry
        {
            Name = "PECK",
            Native = "G83",
            Modal = true,
            Params = new Dictionary<string, string>
            {
                ["DEPTH"] = "Z",
                ["CLEARANCE"] = "R",
                ["PECK"] = "Q",
                ["CYCLE_DWELL"] = "P",
                ["CYCLE_F"] = "F",
            },
            RuleWords = ["SURFACE", "SAFE", "CYCLE_RETRACT", "AXIS"],
        };
    }
}
