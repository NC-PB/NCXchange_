namespace MyShopRules.Tests;

/// <summary>
/// The test of CoolantClutchRule, to copy for a rule of your own: an NCX program in, and out
/// the program the virtual machine runs, with the blocks the rule inserted.
/// </summary>
public sealed class CoolantClutchRuleTests
{
    // The block that switches the through-spindle coolant on gets the spindle stop before it
    // and the restart after it; every other block stays as it is.
    [Fact]
    public void CoolantClutchRule_ThroughCoolantOn_StopsTheSpindleBeforeAndStartsItAfter()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="CLUTCH"
            UNITS=MM
            SPINDLE:MAIN=CW RPM:MAIN=1500
            COOLANT:THROUGH=ON
            PROGRAM=END
            FILE=END

            """;

        string expected = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="CLUTCH"
            UNITS=MM
            SPINDLE:MAIN=CW RPM:MAIN=1500
            @SAVE=SPINDLE:MAIN
            SPINDLE:MAIN=OFF
            COOLANT:THROUGH=ON
            @RESTORE=SPINDLE:MAIN
            PROGRAM=END
            FILE=END

            """;

        Assert.Equal(expected, Expand(program, new CoolantClutchRule()));
    }

    // The program as the expander hands it to the virtual machine: parsed, the rule asked
    // about every block, and written as NCX with the blocks the rule inserted.
    private static string Expand(string program, IProgramRewriter rule)
    {
        NcxProgram parsed = Parser.Parse(program, "test.ncx", new ParserOptions());
        NcxProgram expanded = Expander.Expand(parsed, DefaultMachine.Create(), [rule]);
        return NcxWriter.Write(expanded, new WriterOptions { IncludeGenerated = true });
    }
}
