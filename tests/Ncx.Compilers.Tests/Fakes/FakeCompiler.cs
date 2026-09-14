using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Tests.Fakes;

/// <summary>
/// A compiler of a small Fanuc-like syntax on top of the framework: % for FILE=BEGIN and FILE=END, O and the name for
/// PROGRAM=BEGIN, O and the NAME for SUB=BEGIN, program_end and sub_end, RAW verbatim, CYCL DEF n with the native
/// parameters, G0 and G1 written on change with X Y Z and the feed of the virtual machine written on change, the tool
/// change and the preload of the framework, M98 P for CALL, comments in parentheses.
/// </summary>
internal sealed class FakeCompiler(Controller controller) : CompilerBase
{
    private static readonly string[] s_axes = ["X", "Y", "Z"];

    public override Controller Controller => controller;

    protected override string FileExtension => ".nc";

    protected override bool DecimalPointOnWholeNumbers => true;

    protected override string CommentLine(string text)
    {
        return "(" + text + ")";
    }

    // The % and O lines and the comment lines take no block number, as in a Fanuc program (controllers fanuc.md 1).
    protected override bool TakesBlockNumber(string line)
    {
        return line.Length > 0 && line[0] is not ('%' or 'O' or '(');
    }

    protected override void WriteBlock(Block block, ChannelSnapshot before, ChannelSnapshot after)
    {
        if (block.Has("FILE"))
        {
            Line("%");
            return;
        }

        if (block.Has("PROGRAM", null, "BEGIN"))
        {
            Line($"O{after.Program.Number} ({after.Program.Name})");
            return;
        }

        if (block.Has("PROGRAM", null, "END"))
        {
            WriteProgramEnd();
            return;
        }

        if (block.Has("SUB", null, "BEGIN"))
        {
            Line("O" + Step.Section?.Name);
            return;
        }

        if (block.Has("SUB", null, "END"))
        {
            WriteSubEnd();
            return;
        }

        if (RawText(block) is string raw)
        {
            Line(raw);
            return;
        }

        if (NativeCycleOf(block) is NativeCycle cycle)
        {
            WriteNativeCycle(cycle, block);
            return;
        }

        WriteMotion(block, after);
        WriteToolChange();
        WritePreload();
        if (block.Find("CALL") is Word call)
        {
            Line("M98 P" + call.Value.ToCanonical());
        }

        if (block.Find("COMMENT")?.Value is StringValue comment)
        {
            Line(CommentLine(CommentText(comment.Content)));
        }
    }

    // G0 and G1 are modal and written on change; the axis words of the block as written; the feed of the virtual
    // machine on a LINE, written on change.
    private void WriteMotion(Block block, ChannelSnapshot after)
    {
        string? code = block.Verb?.Key switch
        {
            "RAPID" => "G0",
            "LINE" => "G1",
            _ => null,
        };
        if (code is null)
        {
            return;
        }

        var words = new List<string>();
        if (Target.Changes("G01", code))
        {
            words.Add(code);
        }

        foreach (string axis in s_axes)
        {
            if (block.Find(axis) is Word word && NumberOf(word) is decimal value)
            {
                words.Add(axis + Numbers.Format(axis, value, block));
            }
        }

        if (code == "G1" && after.Motion.Feed is decimal feed)
        {
            string text = Numbers.FormatFeed(feed, block);
            if (Target.Changes("F", text))
            {
                words.Add("F" + text);
            }
        }

        Line(string.Join(" ", words));
    }

    private void WriteNativeCycle(NativeCycle cycle, Block block)
    {
        var words = new List<string> { "CYCL DEF " + cycle.Number };
        foreach (Word parameter in cycle.Parameters)
        {
            string value = NumberOf(parameter) is decimal number
                ? Numbers.Format(parameter.Key, number, block)
                : parameter.Value.ToCanonical();
            words.Add(parameter.Key + "=" + value);
        }

        Line(string.Join(" ", words));
    }

    private static decimal? NumberOf(Word word)
    {
        return word.Value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue value => value.Number,
            _ => null,
        };
    }
}
