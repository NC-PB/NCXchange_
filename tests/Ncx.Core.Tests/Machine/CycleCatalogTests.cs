using Ncx.Core.Machine;

namespace Ncx.Core.Tests.Machine;

/// <summary>
/// The lookups of a cycle catalog that readers, compilers and the expander share: by NCX name, by native cycle, by
/// the fixed values of a native cycle, the override of a later file, and the native cycles of D94 (machine-config 6,
/// language 4.7.1, architecture 6).
/// </summary>
public sealed class CycleCatalogTests
{
    // Architecture 6: Find takes the NCX name of the cycle.
    [Fact]
    public void Find_NcxName_GivesItsEntry()
    {
        CycleEntry? peck = DrillingFamily.Catalog(Controller.Fanuc).Find("PECK");

        Assert.NotNull(peck);
        Assert.Equal("G83", peck.Native);
    }

    // A name that no entry carries finds nothing; the caller reports it.
    [Fact]
    public void Find_UnknownName_GivesNull()
    {
        Assert.Null(DrillingFamily.Catalog(Controller.Heidenhain).Find("RECT_POCKET"));
    }

    // Architecture 6: FindNative takes the native cycle; of two entries of one cycle the first in catalog order is
    // found, cycle 200 is DRILL before DRILL_DWELL (controller-mapping 5).
    [Fact]
    public void FindNative_TwoEntriesOfOneNativeCycle_GivesTheFirst()
    {
        CycleCatalog heidenhain = DrillingFamily.Catalog(Controller.Heidenhain);

        Assert.Equal("DRILL", heidenhain.FindNative("200")?.Name);
        Assert.Equal("PECK", heidenhain.FindNative("203")?.Name);
        Assert.Null(heidenhain.FindNative("251"));
    }

    // Machine-config 6: the reader takes the entry whose fixed values the source block carries; CYCLE83 with VARI=1
    // is PECK, with VARI=0 CHIP_BREAK (controller-mapping 5).
    [Fact]
    public void FindNative_FixedValues_TakesTheEntryWhoseFixedValuesTheBlockCarries()
    {
        CycleCatalog siemens = DrillingFamily.Catalog(Controller.Siemens);

        Assert.Equal("CHIP_BREAK", siemens.FindNative("CYCLE83", Values("VARI", 0m))?.Name);
        Assert.Equal("PECK", siemens.FindNative("CYCLE83", Values("VARI", 1m))?.Name);
    }

    // An entry without fixed values fits every block of its native cycle.
    [Fact]
    public void FindNative_EntryWithoutFixedValues_FitsEveryBlock()
    {
        CycleCatalog siemens = DrillingFamily.Catalog(Controller.Siemens);

        Assert.Equal("DRILL", siemens.FindNative("CYCLE81", Values("DTB", 0.5m))?.Name);
    }

    // A block that leaves out a fixed value of every entry of its cycle fits none of them.
    // TODO(question) in CycleCatalog: the default of an empty position (siemens 7) is not in the documents.
    [Fact]
    public void FindNative_FixedValueTheBlockLeavesOut_GivesNull()
    {
        CycleCatalog siemens = DrillingFamily.Catalog(Controller.Siemens);

        Assert.Null(siemens.FindNative("CYCLE83", new Dictionary<string, decimal>()));
    }

    // Machine-config 6: an entry of a later file takes the place of the entry of the same name.
    [Fact]
    public void Override_EntryOfTheSameName_TakesItsPlace()
    {
        CycleCatalog fanuc = DrillingFamily.Catalog(Controller.Fanuc);

        CycleCatalog machine = fanuc.Override([DoosanEntry("PECK", "FUNC:PECK_MODE=RETRACT")]);

        Assert.Equal(7, machine.Entries.Count);
        Assert.Equal("PECK", machine.Entries[2].Name);
        Assert.Equal(["FUNC:PECK_MODE=RETRACT"], machine.Find("PECK")?.Rule?.Pre);
        Assert.Equal(Controller.Fanuc, machine.Controller);
    }

    // Machine-config 6: an entry of a name the catalog does not have is added at the end.
    [Fact]
    public void Override_NewName_IsAddedAtTheEnd()
    {
        var slot = new CycleEntry
        {
            Name = "SLOT",
            Native = "253",
            WrittenKeys = ["name", "native"],
        };

        CycleCatalog machine = DrillingFamily.Catalog(Controller.Heidenhain).Override([slot]);

        Assert.Equal(8, machine.Entries.Count);
        Assert.Equal("SLOT", machine.Entries[7].Name);
    }

    // Language 4.7.1: the Doosan selects the chip breaking of G83 with a mode M code before the cycle; CHIP_BREAK moved
    // from G73 to G83 keeps the address words of the family.
    [Fact]
    public void Override_ChipBreakMovedToG83_KeepsTheParametersOfTheFamily()
    {
        CycleCatalog machine = DrillingFamily.Catalog(Controller.Fanuc).Override(
        [
            DoosanEntry("PECK", "FUNC:PECK_MODE=RETRACT"),
            DoosanEntry("CHIP_BREAK", "FUNC:PECK_MODE=CHIP_BREAK"),
        ]);

        CycleEntry? chipBreak = machine.Find("CHIP_BREAK");
        Assert.NotNull(chipBreak);
        Assert.Equal("G83", chipBreak.Native);
        Assert.Equal("Q", chipBreak.NativeOf("PECK"));
        Assert.Equal(["FUNC:PECK_MODE=CHIP_BREAK"], chipBreak.Rule?.Pre);
        Assert.Null(machine.FindNative("G73"));
    }

    // Language 4.7.1, D94: a native cycle CYCLE:<controller>=n compiles only to its own controller family; it passes
    // through the catalog of that family and no other.
    [Fact]
    public void PassesThrough_ControllerAddress_OnlyOfTheOwnFamily()
    {
        CycleCatalog heidenhain = DrillingFamily.Catalog(Controller.Heidenhain);

        Assert.True(heidenhain.PassesThrough("HEIDENHAIN"));
        Assert.False(heidenhain.PassesThrough("FANUC"));
        Assert.False(heidenhain.PassesThrough("SIEMENS"));
    }

    // The empty catalog of the default machine (D103) belongs to no family, so no native cycle passes through it.
    [Fact]
    public void PassesThrough_CatalogWithoutController_PassesNothing()
    {
        Assert.False(new CycleCatalog().PassesThrough("HEIDENHAIN"));
    }

    // A Doosan [[cycle]] entry: the cycle on G83 with the mode function before it (machine-config 5a).
    private static CycleEntry DoosanEntry(string name, string modeFunction)
    {
        return new CycleEntry
        {
            Name = name,
            Native = "G83",
            Rule = new ExpansionRule { Pre = [modeFunction] },
            WrittenKeys = ["name", "native", "pre"],
        };
    }

    // The native values of a source block with one parameter.
    private static Dictionary<string, decimal> Values(string native, decimal value)
    {
        return new Dictionary<string, decimal> { [native] = value };
    }
}
