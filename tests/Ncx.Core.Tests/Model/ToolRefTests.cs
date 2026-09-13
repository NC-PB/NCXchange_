using Ncx.Core.Model;

namespace Ncx.Core.Tests.Model;

/// <summary>
/// The tool of TOOL and PRELOAD is an integer or a string (language 4.4).
/// </summary>
public sealed class ToolRefTests
{
    [Fact]
    public void ToolRef_SameNumber_IsTheSameTool()
    {
        Assert.Equal(new ToolRef(4), new ToolRef(4));
        Assert.NotEqual(new ToolRef(4), new ToolRef(5));
    }

    [Fact]
    public void ToolRef_SameName_IsTheSameTool()
    {
        Assert.Equal(new ToolRef("DRILL_D8"), new ToolRef("DRILL_D8"));
    }

    // TOOL=4 and TOOL="4" are two different tools: a number and a name.
    [Fact]
    public void ToolRef_NumberAndNameWithTheSameDigits_AreDifferentTools()
    {
        Assert.NotEqual(new ToolRef(4), new ToolRef("4"));
    }

    [Fact]
    public void ToolRef_ByNumber_HasNoName()
    {
        var tool = new ToolRef(9001);

        Assert.Equal(9001, tool.Number);
        Assert.Null(tool.Name);
    }

    [Fact]
    public void ToolRef_ByName_HasNoNumber()
    {
        var tool = new ToolRef("DRILL_D8");

        Assert.Equal("DRILL_D8", tool.Name);
        Assert.Null(tool.Number);
    }

    // The tool as NCX writes it: the number, or the name as a string (language 4.4).
    [Fact]
    public void ToString_Number_WritesTheNumber()
    {
        Assert.Equal("4", new ToolRef(4).ToString());
    }

    [Fact]
    public void ToString_Name_WritesTheNameAsAString()
    {
        Assert.Equal(
            """
            "DRILL_D8"
            """,
            new ToolRef("DRILL_D8").ToString());
    }
}
