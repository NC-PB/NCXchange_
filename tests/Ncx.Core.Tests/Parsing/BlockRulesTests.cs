using Ncx.Core.Model;

namespace Ncx.Core.Tests.Parsing;

/// <summary>
/// The block rules of language 5, each an ERROR with its PAR code (D98), and the two kinds of key the catalog does not
/// list: the machine axis words of D93 and the native cycle parameters of D94; any other unknown key is an ERROR
/// (virtual machine 3 step 1, architecture 4.1).
/// </summary>
public sealed class BlockRulesTests
{
    // A block has at most one verb (language 5 rule 1).
    [Fact]
    public void BlockRule1_TwoVerbs_IsError()
    {
        Block? block = BlockWithError("RAPID LINE X=1", DiagnosticCodes.TwoVerbs);

        Assert.Equal("RAPID", block?.Verb?.Key);
    }

    // SHIFT=RESET stands with the frame words, so it is no second verb (language 4.2, 5 rules 1 and 6; D90).
    [Fact]
    public void BlockRule1_ResetFormBesideAVerb_IsOneVerb()
    {
        Block block = ParseText.CleanBlock("SHIFT=RESET RAPID X=1");

        Assert.Equal("RAPID", block.Verb?.Key);
    }

    // Axis words require a verb that carries them (language 5 rule 2, 4.3).
    [Theory]
    [InlineData("X=10")]
    [InlineData("F=100 IX=5")]
    [InlineData("COMP=LEFT Y=2")]
    [InlineData("CENTER:X=1")]
    [InlineData("R=5")]
    [InlineData("ANGLE=90")]
    [InlineData("TX=0")]
    [InlineData("SHIFT=RESET X=1")]
    public void BlockRule2_AxisWordWithoutVerb_IsError(string text)
    {
        BlockWithError(text, DiagnosticCodes.AxisWordWithoutVerb);
    }

    // RETRACT carries no axis words (language 5 rule 2, D83).
    [Fact]
    public void BlockRule2_RetractWithAxisWords_IsError()
    {
        BlockWithError("RETRACT Z=5", DiagnosticCodes.AxisWordWithoutVerb);
    }

    // The motion verbs carry axis words; SHIFT, TILT, TILT_AXIS and SETPOS carry their own (language 5 rule 2).
    [Theory]
    [InlineData("LINE X=1 Y=2 F=100")]
    [InlineData("ARC=CW X=2 Y=7 R=5")]
    [InlineData("ARC=CCW IZ=-5.4 CENTER:X=50 CENTER:Y=50 ANGLE=737.956")]
    [InlineData("RAPID IX=-30 IY=15")]
    [InlineData("LINE X=41.786 TX=0 TY=0.5 TZ=0.866 NX=0 NY=0 NZ=1 F=2841")]
    [InlineData("CYCLE_CALL X=10 Y=10")]
    [InlineData("SHIFT X=60 Y=40 Z=-5")]
    [InlineData("TILT A=0 B=45 C=0")]
    [InlineData("TILT_AXIS A=-90 C=180 MOVE=TURN")]
    [InlineData("SETPOS C=0")]
    [InlineData("SETPOS Z2=0")]
    [InlineData("SHIFT Z2=-5")]
    [InlineData("CYCLE_CALL X={$Q1} Y=10")]
    public void BlockRule2_VerbThatCarriesAxisWords_AcceptsThem(string text)
    {
        ParseText.CleanBlock(text);
    }

    // CENTER, R, ANGLE, TX TY TZ and NX NY NZ require a motion verb; SHIFT, TILT, TILT_AXIS and SETPOS carry their own
    // axis words, the axis names of their rows, and no arc or vector word (language 5 rule 2; 4.2).
    [Theory]
    [InlineData("SHIFT R=5")]
    [InlineData("TILT CENTER:X=1 B=45")]
    [InlineData("SETPOS TX=1")]
    [InlineData("SHIFT ANGLE=90")]
    [InlineData("TILT_AXIS A=0 NZ=1")]
    public void BlockRule2_ArcOrVectorWordUnderAFrameVerb_IsError(string text)
    {
        var diagnostics = new Diagnostics("test.ncx");
        ParseText.Block(text, diagnostics);

        Diagnostic diagnostic = ParseText.Single(diagnostics, DiagnosticCodes.AxisWordWithoutVerb);
        Assert.Equal(Severity.Error, diagnostic.Severity);
        Assert.Contains("RAPID, LINE, ARC or CYCLE_CALL", diagnostic.Message);
    }

    // HOME carries bare axis names, a machine axis of the D93 form among them (language 5 rule 2, 4.3, D93).
    [Theory]
    [InlineData("HOME X=0")]
    [InlineData("HOME IX")]
    [InlineData("HOME R=5")]
    [InlineData("HOME Z2=5")]
    [InlineData("HOME IZ2")]
    public void BlockRule2_HomeWithAValueOrNoAxisName_IsError(string text)
    {
        BlockWithError(text, DiagnosticCodes.HomeTakesBareAxisNames);
    }

    // An axis word is bare only under HOME, otherwise it takes a number or an expression (language 3, KEY; 4.3;
    // D93).
    [Theory]
    [InlineData("LINE X F=100")]
    [InlineData("RAPID Z2")]
    [InlineData("SETPOS C")]
    public void BlockRule2_BareAxisWordOutsideHome_IsError(string text)
    {
        BlockWithError(text, DiagnosticCodes.AxisWordNeedsValue);
    }

    // A key the catalog does not list that has the form of a machine axis is a machine axis word in a block whose
    // verb takes axis words; the virtual machine resolves it (D93).
    [Theory]
    [InlineData("RAPID Z2=-58", "Z2")]
    [InlineData("LINE IZ2=5 F=100", "IZ2")]
    [InlineData("RAPID W={$Q1}", "W")]
    [InlineData("HOME Z2", "Z2")]
    [InlineData("HOME X W Z2", "W")]
    [InlineData("LINE C2=90 F=300", "C2")]
    public void D93_MachineAxisWordUnderAVerbThatTakesAxisWords_IsAccepted(string text, string key)
    {
        Block block = ParseText.CleanBlock(text);

        Word word = block.Find(key)!;
        Assert.Null(word.Definition);
    }

    // A machine axis word counts as an axis word for rule 2 (D93, language 5 rule 2).
    [Fact]
    public void D93_MachineAxisWordWithoutVerb_IsAxisWordWithoutVerb()
    {
        BlockWithError("Z2=5", DiagnosticCodes.AxisWordWithoutVerb);
    }

    // A machine axis word takes a number or an expression (D93).
    [Theory]
    [InlineData("RAPID Z2=OFF")]
    [InlineData("RAPID Z2=\"5\"")]
    public void D93_MachineAxisWordWithAnotherValue_IsError(string text)
    {
        BlockWithError(text, DiagnosticCodes.WordValueNotAccepted);
    }

    // A machine axis word has no address (language 3, ADDR; D93).
    [Fact]
    public void D93_MachineAxisWordWithAnAddress_IsError()
    {
        BlockWithError("RAPID Z2:MAIN=5", DiagnosticCodes.WordTakesNoAddress);
    }

    // In a CYCLE:controller=n block every key the catalog does not know is a native parameter with a number or an
    // expression value, kept in source order (language 4.7.1, D94).
    [Fact]
    public void D94_NativeParameters_AreKeptInSourceOrder()
    {
        Block block = ParseText.CleanBlock("CYCLE:HEIDENHAIN=251 Q218=60 Q215=0 Q219={$Q1}");

        Assert.Equal(["CYCLE", "Q218", "Q215", "Q219"], Keys(block));
        Assert.Null(block.Find("Q215")?.Definition);
    }

    // In such a block a key of the machine-axis form is a native parameter as well, and needs no verb (D94).
    [Fact]
    public void D94_MachineAxisFormInANativeBlock_IsANativeParameter()
    {
        ParseText.CleanBlock("CYCLE:HEIDENHAIN=251 Z2=5 Q200=2");
    }

    // A native parameter takes a number or an expression (D94).
    [Theory]
    [InlineData("CYCLE:HEIDENHAIN=251 Q215=ON")]
    [InlineData("CYCLE:HEIDENHAIN=251 Q215")]
    public void D94_NativeParameterWithAnotherValue_IsError(string text)
    {
        BlockWithError(text, DiagnosticCodes.WordValueNotAccepted);
    }

    // Any other key the catalog does not know is an ERROR, and the block is kept with its source text and the word
    // without a definition (virtual machine 3 step 1, architecture 4.1).
    [Theory]
    [InlineData("RAPID FOO=1 X=1")]
    [InlineData("Q215=0")]
    [InlineData("SPINDLE=CW GEAR=2")]
    public void UnknownKey_OutsideTheTwoProvisionalForms_IsError(string text)
    {
        Block? block = BlockWithError(text, DiagnosticCodes.UnknownKey);

        Assert.Equal(text, block?.SourceText);
    }

    // A key may appear once per block (language 5 rule 4).
    [Theory]
    [InlineData("LINE X=1 X=2 F=100")]
    [InlineData("RAPID Z2=1 Z2=2")]
    [InlineData("SPINDLE:MAIN=CW SPINDLE:MAIN=OFF")]
    [InlineData("CYCLE:HEIDENHAIN=251 Q215=0 Q215=1")]
    public void BlockRule4_KeyTwiceWithTheSameAddress_IsError(string text)
    {
        BlockWithError(text, DiagnosticCodes.DuplicateKey);
    }

    // Keys with different addresses are different words (language 5 rule 4).
    [Fact]
    public void BlockRule4_KeyWithDifferentAddresses_AreDifferentWords()
    {
        ParseText.CleanBlock("OFFSET:LEN=1 OFFSET:RAD=1 COOLANT=ON COOLANT:AIR=ON SPINDLE:MAIN=CW SPINDLE:SUB=CCW");
    }

    // IF, ARG, TIMES, WITH, FRAME, MOVE, ROT, POINT and PHASE need their verb or partner word in the same block
    // (language 5 rule 5).
    [Theory]
    [InlineData("IF={$Q1 < 1}")]
    [InlineData("LABEL=1 IF={$Q1 < 1}")]
    [InlineData("ARG:A=1")]
    [InlineData("TIMES=3")]
    [InlineData("WITH=1,2")]
    [InlineData("FRAME=MACHINE")]
    [InlineData("SETPOS C=0 FRAME=MACHINE")]
    [InlineData("MOVE=TURN")]
    [InlineData("SHIFT X=1 ROT=TABLE")]
    [InlineData("POINT=2")]
    [InlineData("RAPID X=1 POINT=2")]
    [InlineData("PHASE=113.5")]
    public void BlockRule5_WordWithoutItsPartner_IsError(string text)
    {
        BlockWithError(text, DiagnosticCodes.PartnerWordMissing);
    }

    // ARG is the argument of the CALL in the same block, and only of a CALL: a REPEAT has no callee whose local
    // variable the argument could become (language 4.9, ARG row; virtual machine 3.6, 5; wave-1 question #57).
    [Fact]
    public void BlockRule5_ArgBesideRepeatWithoutCall_IsError()
    {
        var diagnostics = new Diagnostics("test.ncx");
        ParseText.Block("REPEAT=1 TIMES=3 ARG:A=1", diagnostics);

        Diagnostic diagnostic = ParseText.Single(diagnostics, DiagnosticCodes.PartnerWordMissing);
        Assert.Equal(Severity.Error, diagnostic.Severity);
        Assert.Equal("ARG needs CALL in the same block (language 5 rule 5).", diagnostic.Message);
    }

    // With the partner in the block the words are accepted (language 5 rule 5, 4.2, 4.3, 4.5, 4.8, 4.9).
    [Theory]
    [InlineData("JUMP=1 IF={$Q3 < $Q2}")]
    [InlineData("CALL=100 IF={$Q1 > 0}")]
    [InlineData("CALL=\"O9010\" ARG:A=1")]
    [InlineData("CALL=100 TIMES=4")]
    [InlineData("REPEAT=1 TIMES=3")]
    [InlineData("SYNC=100 WITH=1,2")]
    [InlineData("RAPID Z=0 FRAME=MACHINE")]
    [InlineData("LINE X=0 F=100 FRAME=MACHINE")]
    [InlineData("TILT B=45 MOVE=TURN ROT=COORD")]
    [InlineData("TILT_AXIS A=-90 C=180 MOVE=TURN")]
    [InlineData("HOME C POINT=2")]
    [InlineData("SPINDLE_SYNC=MAIN,SUB PHASE=113.5")]
    public void BlockRule5_WordWithItsPartner_IsAccepted(string text)
    {
        ParseText.CleanBlock(text);
    }

    // Parses one line and asserts that the rule's ERROR is among what the parser reported, once.
    private static Block? BlockWithError(string text, string code)
    {
        var diagnostics = new Diagnostics("test.ncx");
        Block? block = ParseText.Block(text, diagnostics);

        Diagnostic diagnostic = ParseText.Single(diagnostics, code);
        Assert.Equal(Severity.Error, diagnostic.Severity);
        return block;
    }

    private static List<string> Keys(Block block)
    {
        var keys = new List<string>();
        foreach (Word word in block.Words)
        {
            keys.Add(word.Key);
        }

        return keys;
    }
}
