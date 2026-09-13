using Ncx.Core.Model;

namespace Ncx.Core.Tests.Parsing;

/// <summary>
/// Every value form of finding F8: a word takes the value types its catalog entry gives it, several for many words,
/// and the parser converts each value by its form (language 3, value types; 4.1, 4.2, 4.3, 4.4, 4.9, 4.13; D90, D96).
/// </summary>
public sealed class ValueFormTests
{
    // SUB=BEGIN NAME=100: the name of a subprogram section may be an integer (language 3, the EBNF line of sub; D90).
    [Fact]
    public void F8_SubBeginNameInteger_IsAnInteger()
    {
        Block block = ParseText.CleanBlock("SUB=BEGIN NAME=100");

        Assert.Equal(new IntegerValue(100, "100"), block.Find("NAME")?.Value);
    }

    // NAME="SLOT ROW": the program name is a string (language 4.1).
    [Fact]
    public void F8_NameString_IsAString()
    {
        Block block = ParseText.CleanBlock("PROGRAM=BEGIN NAME=\"SLOT ROW\"");

        Assert.Equal(new StringValue("SLOT ROW"), block.Find("NAME")?.Value);
    }

    // TOOL: the bare form changes to the preloaded tool; TOOL="DRILL_D8": a tool by name (language 4.4, D91).
    [Theory]
    [InlineData("TOOL", "")]
    [InlineData("TOOL=\"DRILL_D8\"", "\"DRILL_D8\"")]
    [InlineData("TOOL=4", "4")]
    public void F8_Tool_TakesNoValueAStringOrAnInteger(string text, string value)
    {
        Block block = ParseText.CleanBlock(text);

        Assert.Equal(value, Assert.Single(block.Words).Value.ToCanonical());
    }

    // TOOL and PRELOAD target a holder by its role, the default holder without one (language 4.4, 4.10; virtual
    // machine 2.3, TOOL[:r] and PRELOAD[:r]; 3.8 rule 2).
    [Theory]
    [InlineData("TOOL:TURRET1=3")]
    [InlineData("PRELOAD:TURRET1=5")]
    [InlineData("PRELOAD=\"DRILL_D8\"")]
    public void Language410_ToolAndPreloadWithAHolderRole_AreAccepted(string text)
    {
        Block block = ParseText.CleanBlock(text);

        Assert.Equal(text, Assert.Single(block.Words).ToCanonical());
    }

    // RETRACT: a verb, bare to the axis limit or with the distance as its value (language 4.3, D83).
    [Theory]
    [InlineData("RETRACT", "")]
    [InlineData("RETRACT=50", "50")]
    public void F8_Retract_IsTheVerbBareOrWithADistance(string text, string value)
    {
        Block block = ParseText.CleanBlock(text);

        Assert.Equal("RETRACT", block.Verb?.Key);
        Assert.Equal(value, block.Verb?.Value.ToCanonical());
    }

    // SKIP: bare, or the number 1 to 9 of the block skip switch (language 4.1, D53).
    [Theory]
    [InlineData("SKIP LINE X=1", null)]
    [InlineData("SKIP=3 LINE X=1", 3)]
    public void F8_Skip_IsBareOrASwitchNumber(string text, int? switchNumber)
    {
        Block block = ParseText.CleanBlock(text);

        Assert.True(block.Skip);
        Assert.Equal(switchNumber, block.SkipNumber);
    }

    // TOLERANCE: a number or OFF (language 4.1, D85).
    [Theory]
    [InlineData("TOLERANCE=OFF")]
    [InlineData("TOLERANCE=0.02")]
    public void F8_Tolerance_IsANumberOrOff(string text)
    {
        Block block = ParseText.CleanBlock(text);

        Assert.Equal(text, Assert.Single(block.Words).ToCanonical());
    }

    // MIRROR: an axis list, or OFF (language 4.2).
    [Fact]
    public void F8_MirrorList_IsAList()
    {
        Block block = ParseText.CleanBlock("MIRROR=X,Y");

        Assert.Equal(new ListValue(["X", "Y"]), Assert.Single(block.Words).Value);
    }

    // MIRROR=OFF is an identifier (language 4.2).
    [Fact]
    public void F8_MirrorOff_IsAnIdentifier()
    {
        Block block = ParseText.CleanBlock("MIRROR=OFF");

        Assert.Equal(new IdentValue("OFF"), Assert.Single(block.Words).Value);
    }

    // SHIFT with axis words is the verb; SHIFT=RESET stands with the frame words and is no verb (language 4.2, 5
    // rules 1 and 6; D90).
    [Fact]
    public void F8_ShiftWithAxisWords_IsTheVerb()
    {
        Block block = ParseText.CleanBlock("SHIFT X=1");

        Assert.Equal("SHIFT", block.Verb?.Key);
        Assert.Equal(NoValue.Instance, block.Verb?.Value);
    }

    // SHIFT=RESET removes the shifts and what follows them; the block has no verb (language 4.2, D90).
    [Fact]
    public void F8_ShiftReset_IsNoVerb()
    {
        Block block = ParseText.CleanBlock("SHIFT=RESET");

        Assert.Null(block.Verb);
        Assert.Equal(new IdentValue("RESET"), Assert.Single(block.Words).Value);
    }

    // HOME X Z: the verb with bare axis names (language 4.3, 5 rule 2).
    [Fact]
    public void F8_HomeWithBareAxisNames_HasNoValues()
    {
        Block block = ParseText.CleanBlock("HOME X Z");

        Assert.Equal("HOME", block.Verb?.Key);
        Assert.Equal(NoValue.Instance, block.Find("X")?.Value);
        Assert.Equal(NoValue.Instance, block.Find("Z")?.Value);
    }

    // CYLINDER=30: the reference radius switches the transformation on; there is no ON form (language 4.2, D96).
    [Fact]
    public void F8_CylinderRadius_IsANumber()
    {
        Block block = ParseText.CleanBlock("CYLINDER=30");

        Assert.Equal(new IntegerValue(30, "30"), Assert.Single(block.Words).Value);
    }

    // CYLINDER=ON is the ERROR of the catalog: the value of CYLINDER is the radius (D96).
    [Fact]
    public void D96_CylinderOn_IsError()
    {
        var diagnostics = new Diagnostics("test.ncx");
        ParseText.Block("CYLINDER=ON", diagnostics);

        ParseText.Single(diagnostics, DiagnosticCodes.WordValueNotAccepted);
    }

    // A value type the entry does not take is the ERROR of the catalog, reported by the parser (language 3, 4).
    [Theory]
    [InlineData("SPINDLE=UP")]
    [InlineData("TOOL=1.5")]
    [InlineData("PROGRAM=BEGIN NAME=SHAFT")]
    [InlineData("F=FAST")]
    public void Value_TypeTheEntryDoesNotTake_IsError(string text)
    {
        var diagnostics = new Diagnostics("test.ncx");
        ParseText.Block(text, diagnostics);

        ParseText.Single(diagnostics, DiagnosticCodes.WordValueNotAccepted);
    }
}
