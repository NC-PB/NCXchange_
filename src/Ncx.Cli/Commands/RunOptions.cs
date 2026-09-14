using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using Ncx.Core.VirtualMachine;

namespace Ncx.Cli.Commands;

/// <summary>
/// The file argument and the options that check, trace and annotate share (architecture 10): --machine (D103),
/// --strict (D97), --skip-blocks (D53) and --expand-cycles (D37). Each command gets its own instances.
/// </summary>
internal sealed class RunOptions
{
    // The block skip switches of language 4.1 are numbered 1 to 9.
    private const int FirstSwitch = 1;
    private const int LastSwitch = 9;

    private readonly Argument<string> _file = new("file") { Description = "The NCX file." };

    private readonly Option<string> _machine = new("--machine")
    {
        Description = "The machine file to run against, by name in machines/ or by path. Without it: the machine that "
            + "ncx.toml names, else the built-in default machine.",
        HelpName = "toml",
    };

    private readonly Option<bool> _strict = StrictOption();

    private readonly Option<SkipBlocks> _skipBlocks = SkipBlocksOption();

    private readonly Option<bool> _expandCycles = new("--expand-cycles")
    {
        Description = "Raise every cycle call as its individual motions.",
    };

    private RunOptions()
    {
    }

    /// <summary>
    /// Adds the file argument and the shared options to a command.
    /// </summary>
    /// <param name="command">check, trace or annotate.</param>
    /// <returns>The options, to read the settings from the parse result.</returns>
    public static RunOptions AddTo(Command command)
    {
        var options = new RunOptions();
        command.Add(options._file);
        command.Add(options._machine);
        command.Add(options._strict);
        command.Add(options._skipBlocks);
        command.Add(options._expandCycles);
        return options;
    }

    /// <summary>
    /// --strict, which every command accepts: a WARNING sets the exit code 1 (architecture 10, D97).
    /// </summary>
    public static Option<bool> StrictOption()
    {
        return new Option<bool>("--strict") { Description = "Exit with 1 on a WARNING as well." };
    }

    /// <summary>
    /// --skip-blocks, the run option skip_blocks on the command line: none, all, or the block skip switches that are
    /// on (D53, language 4.1); check, trace, annotate and analyze accept it.
    /// </summary>
    public static Option<SkipBlocks> SkipBlocksOption()
    {
        return new Option<SkipBlocks>("--skip-blocks")
        {
            Description = "Which SKIP blocks the virtual machine skips: none (the default), all, or the block skip "
                + "switches that are on, 1,3.",
            HelpName = "none|all|1,3",
            DefaultValueFactory = _ => SkipBlocks.None,
            CustomParser = ParseSkipBlocks,
        };
    }

    /// <summary>
    /// The settings of the run as the command line gives them.
    /// </summary>
    /// <param name="parseResult">The parsed command line of the command the options were added to.</param>
    public RunSettings Read(ParseResult parseResult)
    {
        return new RunSettings
        {
            File = parseResult.GetRequiredValue(_file),
            MachineFile = parseResult.GetValue(_machine),
            Strict = parseResult.GetValue(_strict),
            SkipBlocks = parseResult.GetValue(_skipBlocks) ?? SkipBlocks.None,
            ExpandCycles = parseResult.GetValue(_expandCycles),
        };
    }

    // --skip-blocks is the run option skip_blocks of virtual machine 3.6 on the command line: none, all, or the numbers
    // of the block skip switches that are on, 1 to 9, separated by commas, as skip_blocks = [1, 3] (D53, language 4.1).
    // Anything else is a usage error, exit code 2 before the run starts (D97).
    private static SkipBlocks? ParseSkipBlocks(ArgumentResult result)
    {
        string text = result.Tokens.Count == 1 ? result.Tokens[0].Value : "";
        if (text == "none")
        {
            return SkipBlocks.None;
        }

        if (text == "all")
        {
            return SkipBlocks.Every;
        }

        var switches = new List<int>();
        foreach (string part in text.Split(','))
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                || number < FirstSwitch
                || number > LastSwitch)
            {
                result.AddError($"--skip-blocks \"{text}\" is neither none nor all nor block skip switches 1 to 9 "
                    + "separated by commas, as 1,3 (language 4.1, D53)");
                return null;
            }

            switches.Add(number);
        }

        return SkipBlocks.OnSwitches(switches);
    }
}
