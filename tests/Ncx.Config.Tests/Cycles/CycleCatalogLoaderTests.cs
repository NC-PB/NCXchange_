using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Tests.Cycles;

/// <summary>
/// A cycle catalog file loaded entry by entry over the built-in drilling family, every mistake reported with its line
/// (machine-config 6, phase 2 P2-03, the loading rules of P2-01).
/// </summary>
public sealed class CycleCatalogLoaderTests
{
    // P2-03: a catalog entry with a pre rule loads into an ExpansionRule; the four keys of machine-config 5a.
    [Fact]
    public void Entry_WithTheFourRuleKeys_LoadsIntoAnExpansionRule()
    {
        CycleCatalog catalog = LoadClean("""
            [[cycle]]
            name = "PECK"
            native = "G83"
            pre = ["FUNC:PECK_MODE=RETRACT"]
            post = ["FUNC:CHIP_CONVEYOR=ON"]
            requires = { SPINDLE = "OFF" }
            restore = ["SPINDLE"]
            """, Controller.Fanuc);

        ExpansionRule? rule = catalog.Find("PECK")?.Rule;

        Assert.NotNull(rule);
        Assert.Equal(["FUNC:PECK_MODE=RETRACT"], rule.Pre);
        Assert.Equal(["FUNC:CHIP_CONVEYOR=ON"], rule.Post);
        Assert.Equal("OFF", rule.Requires["SPINDLE"]);
        Assert.Equal(["SPINDLE"], rule.Restore);
    }

    // Machine-config 6: an entry of the built-in family written again overrides the keys it writes; the address
    // words of G83 stay.
    [Fact]
    public void Entry_OfTheBuiltInFamily_OverridesOnlyTheKeysItWrites()
    {
        CycleCatalog catalog = LoadClean("""
            [[cycle]]
            name = "PECK"
            native = "G83"
            pre = ["FUNC:PECK_MODE=RETRACT"]
            """, Controller.Fanuc);

        CycleEntry? peck = catalog.Find("PECK");

        Assert.NotNull(peck);
        Assert.Equal("Q", peck.NativeOf("PECK"));
        Assert.Equal("Z", peck.NativeOf("DEPTH"));
        Assert.True(peck.Modal);
        Assert.Equal(7, catalog.Entries.Count);
    }

    // Machine-config 6: native = 251 is the cycle number; it is kept as the text a reader compares.
    [Fact]
    public void Entry_NativeAsNumber_IsKeptAsItsText()
    {
        CycleCatalog catalog = LoadClean("""
            [[cycle]]
            name = "RECT_POCKET"
            native = 251
            params = { LENGTH = "Q218", WIDTH = "Q219", DEPTH = "Q201", SURFACE = "Q203" }
            absolute_from_surface = ["DEPTH"]
            """, Controller.Heidenhain);

        CycleEntry? pocket = catalog.FindNative("251");

        Assert.NotNull(pocket);
        Assert.Equal("RECT_POCKET", pocket.Name);
        Assert.Equal("Q218", pocket.NativeOf("LENGTH"));
        Assert.Equal(8, catalog.Entries.Count);
    }

    // Machine-config 6: one contour word names the contour subprogram itself, Siemens CYCLE95 NPP.
    [Fact]
    public void Entry_ContourOfOneWord_NamesTheContourSubprogram()
    {
        CycleCatalog catalog = LoadClean("""
            [[cycle]]
            name = "ROUGH_TURN"
            native = "CYCLE95"
            contour = ["NPP"]
            """, Controller.Siemens);

        Assert.Equal(["NPP"], catalog.Find("ROUGH_TURN")?.Contour);
    }

    // Machine-config 6: fixed values load as numbers.
    [Fact]
    public void Entry_FixedValue_LoadsAsANumber()
    {
        CycleCatalog catalog = LoadClean("""
            [[cycle]]
            name = "PECK"
            native = "CYCLE83"
            fixed = { VARI = 1 }
            """, Controller.Siemens);

        Assert.Equal(1m, catalog.Find("PECK")?.Fixed["VARI"]);
    }

    // P2-01 applied to the catalog: an entry without its name is an ERROR on its header.
    [Fact]
    public void Entry_WithoutName_ReportsTheMissingKeyOnItsHeader()
    {
        Diagnostic error = SingleReport("""
            [[cycle]]
            native = "G81"
            """, Controller.Fanuc);

        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.MissingKey, error.Code);
        Assert.Equal(1, error.Line);
        Assert.Contains("name", error.Message, StringComparison.Ordinal);
    }

    // Machine-config 6: every entry names its native cycle, the entries of machine-config 5a and 6 included.
    [Fact]
    public void Entry_WithoutNative_ReportsTheMissingKeyOnItsHeader()
    {
        var diagnostics = new Diagnostics("test-cycles.toml");

        CycleCatalog? catalog = CycleCatalogLoader.LoadText("""
            [[cycle]]
            name = "RECT_POCKET"
            params = { DEPTH = "Q201" }
            """, Controller.Heidenhain, diagnostics);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.MissingKey, error.Code);
        Assert.Equal(1, error.Line);
        Assert.Contains("native", error.Message, StringComparison.Ordinal);
        Assert.Null(catalog);
    }

    // P2-01 applied to the catalog: a wrong type is an ERROR on its line, and the file gives no catalog.
    [Fact]
    public void Entry_ParamsAsString_ReportsAWrongTypeOnItsLine()
    {
        Diagnostic error = SingleReport("""
            [[cycle]]
            name = "SLOT"
            native = 253
            params = "Q201"
            """, Controller.Heidenhain);

        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.WrongType, error.Code);
        Assert.Equal(4, error.Line);
    }

    // A file with an ERROR gives no catalog, because the run stops (virtual machine 2.9).
    [Fact]
    public void File_WithAnError_GivesNoCatalog()
    {
        var diagnostics = new Diagnostics("test-cycles.toml");

        CycleCatalog? catalog = CycleCatalogLoader.LoadText("""
            [[cycle]]
            name = "DRILL"
            native = "G81"
            modal = "yes"
            """, Controller.Fanuc, diagnostics);

        Assert.Null(catalog);
        Assert.Equal(DiagnosticCodes.WrongType, Assert.Single(diagnostics.Items).Code);
    }

    // A file that is not TOML reports the syntax error with its line and gives no catalog (P2-01).
    [Fact]
    public void File_NotToml_ReportsTheSyntaxError()
    {
        var diagnostics = new Diagnostics("test-cycles.toml");

        CycleCatalog? catalog = CycleCatalogLoader.LoadText("""
            [[cycle]
            name = "DRILL"
            """, Controller.Fanuc, diagnostics);

        Assert.Null(catalog);
        Assert.Equal(DiagnosticCodes.TomlSyntax, diagnostics.Items[0].Code);
    }

    // P2-01 applied to the catalog: an unknown key is a WARNING on its line that names the nearest known key.
    [Fact]
    public void Entry_UnknownKey_WarnsWithTheNearestKey()
    {
        Diagnostic warning = SingleReport("""
            [[cycle]]
            name = "SLOT"
            native = 253
            parms = { DEPTH = "Q201" }
            """, Controller.Heidenhain);

        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(DiagnosticCodes.UnknownKey, warning.Code);
        Assert.Equal(4, warning.Line);
        Assert.Contains("parms", warning.Message, StringComparison.Ordinal);
        Assert.Contains("params", warning.Message, StringComparison.Ordinal);
    }

    // A catalog file holds [[cycle]] entries only; another table is a WARNING that names [[cycle]].
    [Fact]
    public void File_UnknownTable_WarnsAndSuggestsCycle()
    {
        Diagnostic warning = SingleReport("""
            [[cycles]]
            name = "DRILL"
            native = "G81"
            """, Controller.Fanuc);

        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(DiagnosticCodes.UnknownKey, warning.Code);
        Assert.Equal(1, warning.Line);
        Assert.Contains("[[cycle]]", warning.Message, StringComparison.Ordinal);
    }

    // The catalog finds its entries by name, so one file names each cycle once (machine-config 6).
    [Fact]
    public void Entry_NameWrittenTwice_ReportsTheSecondEntry()
    {
        Diagnostic error = SingleReport("""
            [[cycle]]
            name = "SLOT"
            native = 253

            [[cycle]]
            name = "SLOT"
            native = 254
            """, Controller.Heidenhain);

        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.CycleNameTwice, error.Code);
        Assert.Equal(5, error.Line);
    }

    // Language 3, 4.7.1: a program names the cycle as the value of CYCLE, so the name is an NCX identifier.
    [Fact]
    public void Entry_NameNotAnNcxIdentifier_ReportsIt()
    {
        Diagnostic error = SingleReport("""
            [[cycle]]
            name = "Rect-Pocket"
            native = 251
            """, Controller.Heidenhain);

        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.CycleWordMalformed, error.Code);
        Assert.Equal(2, error.Line);
    }

    // Language 3, 4.7.1: a parameter of the entry is a word of the cycle block, so its name is an NCX key.
    [Fact]
    public void Entry_ParamWordNotAnNcxKey_ReportsIt()
    {
        Diagnostic error = SingleReport("""
            [[cycle]]
            name = "SLOT"
            native = 253
            params = { depth = "Q201" }
            """, Controller.Heidenhain);

        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.CycleWordMalformed, error.Code);
        Assert.Equal(4, error.Line);
        Assert.Contains("depth", error.Message, StringComparison.Ordinal);
    }

    // Machine-config 6: a word of absolute_from_surface is converted from its native value, so params maps it.
    [Fact]
    public void Entry_AbsoluteFromSurfaceWordNotMapped_Warns()
    {
        Diagnostic warning = SingleReport("""
            [[cycle]]
            name = "SLOT"
            native = 253
            params = { DEPTH = "Q201", SURFACE = "Q203" }
            absolute_from_surface = ["DEPTH", "SAFE"]
            """, Controller.Heidenhain);

        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(DiagnosticCodes.CycleAbsoluteWordUnmapped, warning.Code);
        Assert.Equal(5, warning.Line);
        Assert.Contains("SAFE", warning.Message, StringComparison.Ordinal);
    }

    // Machine-config 6: NCX = surface + native, so an entry with absolute_from_surface maps SURFACE.
    [Fact]
    public void Entry_AbsoluteFromSurfaceWithoutSurface_Warns()
    {
        Diagnostic warning = SingleReport("""
            [[cycle]]
            name = "SLOT"
            native = 253
            params = { DEPTH = "Q201" }
            absolute_from_surface = ["DEPTH"]
            """, Controller.Heidenhain);

        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(DiagnosticCodes.CycleAbsoluteWithoutSurface, warning.Code);
        Assert.Equal(5, warning.Line);
    }

    // Machine-config 6: two contour words are the first and the last block, one word the contour subprogram; three
    // mean nothing.
    [Fact]
    public void Entry_ContourOfThreeWords_ReportsIt()
    {
        Diagnostic error = SingleReport("""
            [[cycle]]
            name = "ROUGH_TURN"
            native = "G71"
            contour = ["P", "Q", "R"]
            """, Controller.Fanuc);

        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.CycleContourWordCount, error.Code);
        Assert.Equal(4, error.Line);
    }

    // Machine-config 6: Heidenhain and Siemens entries leave modal out, their calls are words of their own.
    [Fact]
    public void Entry_ModalOnHeidenhain_Warns()
    {
        Diagnostic warning = SingleReport("""
            [[cycle]]
            name = "SLOT"
            native = 253
            modal = true
            """, Controller.Heidenhain);

        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(DiagnosticCodes.CycleKeyOutsideItsFamily, warning.Code);
        Assert.Equal(4, warning.Line);
    }

    // Machine-config 6: Fanuc entries leave the signature out, their parameters are address words of the cycle block.
    [Fact]
    public void Entry_SignatureOnFanuc_Warns()
    {
        Diagnostic warning = SingleReport("""
            [[cycle]]
            name = "ROUGH_TURN"
            native = "G71"
            signature = ["P", "Q"]
            """, Controller.Fanuc);

        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(DiagnosticCodes.CycleKeyOutsideItsFamily, warning.Code);
        Assert.Equal(4, warning.Line);
    }

    // Loads a catalog that is expected to be clean; a failure prints the diagnostics.
    private static CycleCatalog LoadClean(string toml, Controller controller)
    {
        var diagnostics = new Diagnostics("test-cycles.toml");
        CycleCatalog? catalog = CycleCatalogLoader.LoadText(toml, controller, diagnostics);
        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(catalog);
        return catalog;
    }

    // Loads a catalog that is expected to report exactly one thing and returns it.
    private static Diagnostic SingleReport(string toml, Controller controller)
    {
        var diagnostics = new Diagnostics("test-cycles.toml");
        CycleCatalogLoader.LoadText(toml, controller, diagnostics);
        return Assert.Single(diagnostics.Items);
    }
}
