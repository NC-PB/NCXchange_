namespace CoolantClutch.Tests;

/// <summary>
/// The coolant clutch rule of code-guidelines 11: an NCX program in, and out the program the
/// virtual machine runs, with the blocks the rule inserted (architecture 5.5, 9; virtual
/// machine 3.10).
/// </summary>
public sealed class CoolantClutchRuleTests
{
    // Code-guidelines 11: the block that switches the through-spindle coolant on gets the
    // spindle saved and stopped before it and restored after it.
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

        Assert.Equal(expected, Expand(program));
    }

    // Code-guidelines 11: every block the rule is not about stays as it is, the standard
    // coolant and the through-spindle coolant switched off among them.
    [Fact]
    public void CoolantClutchRule_OtherCoolantWords_LeaveTheProgramAsItIs()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="CLUTCH"
            UNITS=MM
            SPINDLE:MAIN=CW RPM:MAIN=1500
            COOLANT=ON
            COOLANT:THROUGH=OFF
            PROGRAM=END
            FILE=END

            """;

        Assert.Equal(program, Expand(program));
    }

    // The program as the expander hands it to the virtual machine: parsed, the rule asked
    // about every block, and written as NCX with the blocks the rule inserted (language 4.15).
    private static string Expand(string program)
    {
        NcxProgram parsed = Parser.Parse(program, "test.ncx", new ParserOptions());
        NcxProgram expanded = Expander.Expand(parsed, DefaultMachine.Create(), [new CoolantClutchRule()]);
        return NcxWriter.Write(expanded, new WriterOptions { IncludeGenerated = true });
    }
}
