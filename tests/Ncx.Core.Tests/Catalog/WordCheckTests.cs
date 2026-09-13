using Ncx.Core.Catalog;
using Ncx.Core.Model;

namespace Ncx.Core.Tests.Catalog;

/// <summary>
/// A word is checked against its catalog entry: its address and its value (language 3, 4); what the entry does not
/// accept is an ERROR with a code of the catalog (D98).
/// </summary>
public sealed class WordCheckTests
{
    // Every value form of the language: identifier sets, numbers, lists, strings, expressions, bare words
    // (language 3, 4; the forms of finding F8).
    [Theory]
    [InlineData("SPINDLE=CW")]
    [InlineData("SPINDLE:MAIN=CCW")]
    [InlineData("SPINDLE=OFF")]
    [InlineData("COOLANT=ON")]
    [InlineData("COOLANT:THROUGH=OFF")]
    [InlineData("TILT A=0 B=45 C=0 MOVE=TURN")]
    [InlineData("TILT_AXIS A=-90 C=180 MOVE=MOVE")]
    [InlineData("TILT B=45 MOVE=STAY ROT=COORD")]
    [InlineData("TOOL")]
    [InlineData("TOOL=4")]
    [InlineData("TOOL=\"DRILL_D8\"")]
    [InlineData("TOOL:TURRET1=3")]
    [InlineData("PRELOAD=0")]
    [InlineData("RETRACT")]
    [InlineData("RETRACT=50")]
    [InlineData("SKIP")]
    [InlineData("SKIP=3")]
    [InlineData("TOLERANCE=OFF")]
    [InlineData("TOLERANCE=0.02 TOLERANCE:ROTARY=0.05 TOLERANCE_MODE=FINISH")]
    [InlineData("MIRROR=X,Y")]
    [InlineData("MIRROR=X")]
    [InlineData("MIRROR=OFF")]
    [InlineData("SHIFT X=1")]
    [InlineData("SHIFT=RESET")]
    [InlineData("ROTATE=RESET")]
    [InlineData("SUB=BEGIN NAME=100")]
    [InlineData("SUB=BEGIN NAME=DRILL_ROW")]
    [InlineData("PROGRAM=BEGIN NAME=\"SLOT ROW\" NUMBER=3 CHANNEL=1")]
    [InlineData("HOME X Z")]
    [InlineData("HOME C POINT=2")]
    [InlineData("CYCLE:HEIDENHAIN=251")]
    [InlineData("CYCLE=RECT_POCKET")]
    [InlineData("CYCLE=OFF")]
    [InlineData("ARC=CCW CENTER:IX=-0.534 CENTER:IY=2 ANGLE=737.956")]
    [InlineData("ARC=CW X=30 C=0 CENTER:X=0 CENTER:C=0")]
    [InlineData("OFFSET:LEN=1 OFFSET:RAD=1")]
    [InlineData("OFFSET=56")]
    [InlineData("VAR:Q1={$Q1 + 20}")]
    [InlineData("VAR:Q2=\"TEXT\"")]
    [InlineData("SYNC=100 WITH=1,2")]
    [InlineData("SYNC=100 WITH=1")]
    [InlineData("SPINDLE_SYNC=MAIN,SUB PHASE=113.5")]
    [InlineData("SPINDLE_SYNC=OFF")]
    [InlineData("JUMP=1 IF={$Q3 < $Q2}")]
    [InlineData("JUMP=END")]
    [InlineData("CALL=100 TIMES={$N}")]
    [InlineData("CALL=\"O9010\" ARG:A=1")]
    [InlineData("FUNC:CHIP_CONVEYOR=ON")]
    [InlineData("RAW:FANUC=\"G411\"")]
    [InlineData("ORIENT:MAIN=90")]
    [InlineData("SPINDLE_MODE:SUB=AXIS")]
    [InlineData("LINE X={$Q1} TX=0 TY=0.5 TZ=0.866 NX=0 NY=0 NZ=1 F=2841")]
    [InlineData("CYCLE=DRILL AXIS=X SURFACE=50 CLEARANCE=54 DEPTH=30 CYCLE_RETRACT=CLEARANCE CYCLE_F=0.05")]
    [InlineData("CYCLE=TURN_OD CONTOUR=FACE_A")]
    [InlineData("STOP=OPTIONAL DWELL=1.5")]
    [InlineData("CSS:MAIN=ON VC=140 RPM_MAX=3000")]
    public void Accepts_ValueOfItsEntry_ReportsNothing(string text)
    {
        Diagnostics diagnostics = Check(text);

        Assert.Empty(diagnostics.Items);
    }

    // A value the entry does not accept, an address it does not take or lacks, a switch outside 1 to 9: one ERROR
    // with the code of the rule (language 3, 4; D98).
    [Theory]
    [InlineData("SPINDLE=UP", DiagnosticCodes.WordValueNotAccepted)]
    [InlineData("UNITS=5", DiagnosticCodes.WordValueNotAccepted)]
    [InlineData("TOOL=1.5", DiagnosticCodes.WordValueNotAccepted)]
    [InlineData("F", DiagnosticCodes.WordValueNotAccepted)]
    [InlineData("ARC=LEFT", DiagnosticCodes.WordValueNotAccepted)]
    [InlineData("NCX=1.0", DiagnosticCodes.WordValueNotAccepted)]
    [InlineData("MFUNC=M8", DiagnosticCodes.WordValueNotAccepted)]
    [InlineData("IF=1", DiagnosticCodes.WordValueNotAccepted)]
    [InlineData("SPINDLE_SYNC=MAIN", DiagnosticCodes.WordValueNotAccepted)]
    [InlineData("TOLERANCE:ROTARY=OFF", DiagnosticCodes.WordValueNotAccepted)]
    [InlineData("CYCLE:HEIDENHAIN=POCKET", DiagnosticCodes.WordValueNotAccepted)]
    [InlineData("CYCLE=251", DiagnosticCodes.WordValueNotAccepted)]
    [InlineData("SKIP=10", DiagnosticCodes.SkipSwitchOutOfRange)]
    [InlineData("SKIP=0", DiagnosticCodes.SkipSwitchOutOfRange)]
    [InlineData("LINE:X", DiagnosticCodes.WordTakesNoAddress)]
    [InlineData("UNITS:MAIN=MM", DiagnosticCodes.WordTakesNoAddress)]
    [InlineData("CENTER=5", DiagnosticCodes.WordNeedsAddress)]
    [InlineData("VAR=1", DiagnosticCodes.WordNeedsAddress)]
    [InlineData("FUNC=ON", DiagnosticCodes.WordNeedsAddress)]
    [InlineData("OFFSET:LENGTH=1", DiagnosticCodes.WordAddressNotAccepted)]
    [InlineData("CENTER:A=1", DiagnosticCodes.WordAddressNotAccepted)]
    [InlineData("TOLERANCE:LINEAR=0.01", DiagnosticCodes.WordAddressNotAccepted)]
    public void Accepts_WordItsEntryDoesNotAccept_IsAnError(string text, string code)
    {
        Diagnostics diagnostics = Check(text);

        Diagnostic diagnostic = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, diagnostic.Severity);
        Assert.Equal(code, diagnostic.Code);
    }

    // CYLINDER=30 switches the transformation on with the reference radius as its value; a decimal and an expression
    // are numbers as well; CYLINDER=OFF switches it off (language 4.2, D96).
    [Theory]
    [InlineData("CYLINDER=30")]
    [InlineData("CYLINDER=12.5")]
    [InlineData("CYLINDER={$R}")]
    [InlineData("CYLINDER=OFF")]
    public void D96_CylinderWithTheReferenceRadiusOrOff_IsAccepted(string text)
    {
        Assert.Empty(Check(text).Items);
    }

    // There is no ON form: the value of CYLINDER is the reference radius (language 4.2, D96).
    [Fact]
    public void D96_CylinderOn_IsAnErrorNamingWhatCylinderTakes()
    {
        Diagnostic diagnostic = Assert.Single(Check("CYLINDER=ON").Items);

        Assert.Equal(DiagnosticCodes.WordValueNotAccepted, diagnostic.Code);
        Assert.Equal("CYLINDER takes a number, an expression or OFF, not ON (language 4.2).", diagnostic.Message);
    }

    // A message names the rule and the block, in the words of the specification (code-guidelines 2, 6).
    [Theory]
    [InlineData("SPINDLE=UP", "SPINDLE takes CW, CCW or OFF, not UP (language 4.5).")]
    [InlineData("TOOL=1.5", "TOOL takes no value, an integer or a string, not 1.5 (language 4.4).")]
    [InlineData("F", "F needs a value: a number or an expression (language 4.3).")]
    [InlineData("LINE:X", "LINE takes no address, not LINE:X (language 4.3).")]
    [InlineData("VAR=1", "VAR needs an address, the variable name (language 4.9).")]
    [InlineData("OFFSET:LENGTH=1", "OFFSET takes the address LEN or RAD, not OFFSET:LENGTH (language 4.4).")]
    [InlineData("SKIP=12", "SKIP takes a block skip switch 1 to 9, not 12 (language 4.1).")]
    [InlineData("PROGRAM=BEGIN NAME=SHAFT",
        "NAME takes a string, not SHAFT; an integer or an identifier only with SUB=BEGIN (language 4.1).")]
    [InlineData("SUB=BEGIN NAME=1.5", "NAME takes an integer, a string or an identifier, not 1.5 (language 4.1).")]
    public void Accepts_WordItsEntryDoesNotAccept_SaysWhatTheEntryTakes(string text, string message)
    {
        Diagnostic diagnostic = Assert.Single(Check(text).Items);

        Assert.Equal(message, diagnostic.Message);
    }

    // NAME with PROGRAM=BEGIN is the program name, a string; with START_CHANNEL it selects a program by that name
    // (language 4.1, 4.8). An identifier or an integer is the name of a SUB section only (D90).
    [Theory]
    [InlineData("PROGRAM=BEGIN NAME=SHAFT")]
    [InlineData("PROGRAM=BEGIN NAME=1 NUMBER=1")]
    [InlineData("START_CHANNEL=2 NAME=1")]
    [InlineData("START_CHANNEL=2 NAME=SHAFT_OP2")]
    public void Language41_NameWithProgramBeginOrStartChannel_TakesAStringOnly(string text)
    {
        Diagnostic diagnostic = Assert.Single(Check(text).Items);

        Assert.Equal(Severity.Error, diagnostic.Severity);
        Assert.Equal(DiagnosticCodes.WordValueNotAccepted, diagnostic.Code);
        Assert.EndsWith("(language 4.1).", diagnostic.Message, StringComparison.Ordinal);
    }

    // NAME takes a string with every partner, and with SUB=BEGIN an identifier or an integer as well (language 4.1;
    // language 3, the EBNF line of sub; D90).
    [Theory]
    [InlineData("SUB=BEGIN NAME=100")]
    [InlineData("SUB=BEGIN NAME=DRILL_ROW")]
    [InlineData("SUB=BEGIN NAME=\"DRILL ROW\"")]
    [InlineData("PROGRAM=BEGIN NAME=\"SHAFT\" NUMBER=1")]
    [InlineData("START_CHANNEL=2 NAME=\"SHAFT_OP2\"")]
    public void Language41_NameWithItsPartner_IsAccepted(string text)
    {
        Assert.Empty(Check(text).Items);
    }

    // The value types of a word in its block, by which the parser converts its value (P0-04): an address or a partner
    // word can change those of the entry (language 4.1, 4.7; D90, D94).
    [Theory]
    [InlineData("SUB=BEGIN NAME=100", "NAME", ValueKinds.Integer | ValueKinds.Ident | ValueKinds.String)]
    [InlineData("PROGRAM=BEGIN NAME=\"SHAFT\"", "NAME", ValueKinds.String)]
    [InlineData("START_CHANNEL=2 NAME=\"SHAFT_OP2\"", "NAME", ValueKinds.String)]
    [InlineData("CYCLE:HEIDENHAIN=251", "CYCLE", ValueKinds.Integer)]
    [InlineData("CYCLE=DRILL", "CYCLE", ValueKinds.Ident)]
    public void ValueKindsOf_WordInItsBlock_AreThoseItsAddressOrPartnerGive(string text, string key, ValueKinds kinds)
    {
        Block block = Assert.Single(ExampleBlocks.Read(text));
        Word word = block.Find(key)!;

        Assert.Equal(kinds, WordCheck.ValueKindsOf(word, word.Definition!, block));
    }

    // The diagnostic stands on the block, so a generated block carries the line it was generated for (D98).
    [Fact]
    public void Accepts_WordOfAGeneratedBlock_ReportsTheOriginLine()
    {
        var spindle = new Word { Key = "SPINDLE", Value = new IdentValue("UP") };
        var block = new Block { Line = 4, Words = [spindle], IsGenerated = true, OriginLine = 12 };
        var diagnostics = new Diagnostics("test.ncx");

        bool accepted = WordCheck.Accepts(spindle, WordCatalog.Lookup("SPINDLE")!, block, diagnostics);

        Assert.False(accepted);
        Assert.Equal(12, Assert.Single(diagnostics.Items).OriginLine);
    }

    // @SAVE takes a state key KEY[:ADDR] (D95).
    [Fact]
    public void D95_SaveWithAStateKey_IsAccepted()
    {
        var save = new Word { Key = "@SAVE", Value = new StateKeyValue("SPINDLE", "MAIN") };
        var block = new Block { Line = 1, Words = [save], IsGenerated = true, OriginLine = 7 };
        var diagnostics = new Diagnostics("test.ncx");

        Assert.True(WordCheck.Accepts(save, WordCatalog.Lookup("@SAVE")!, block, diagnostics));
        Assert.Empty(diagnostics.Items);
    }

    // A state key is the value of the pseudo-words only, never of a user word (language 3, value types; D95).
    [Fact]
    public void D95_UserWordWithAStateKey_IsAnError()
    {
        var spindle = new Word { Key = "SPINDLE", Value = new StateKeyValue("SPINDLE", "MAIN") };
        var block = new Block { Line = 1, Words = [spindle] };
        var diagnostics = new Diagnostics("test.ncx");

        Assert.False(WordCheck.Accepts(spindle, WordCatalog.Lookup("SPINDLE")!, block, diagnostics));
        Assert.Equal(DiagnosticCodes.WordValueNotAccepted, Assert.Single(diagnostics.Items).Code);
    }

    // Every word of the text that the catalog lists is checked against its entry.
    private static Diagnostics Check(string text)
    {
        var diagnostics = new Diagnostics("test.ncx");
        foreach (Block block in ExampleBlocks.Read(text))
        {
            foreach (Word word in block.Words)
            {
                if (word.Definition is WordDefinition definition)
                {
                    WordCheck.Accepts(word, definition, block, diagnostics);
                }
            }
        }

        return diagnostics;
    }
}
