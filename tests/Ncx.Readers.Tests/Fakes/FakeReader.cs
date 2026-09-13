using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Writing;

namespace Ncx.Readers.Tests.Fakes;

/// <summary>
/// A reader of a small Fanuc-like syntax on top of the framework: the structure of O, N, %, M30, M2, M99, GOTO and the
/// calls of M98 P, the
/// motion of G0, G1, G28 with G90 and G91, F, M3, M5, M98 P L, the functions of the machine's tables, MFUNC for any
/// other M, RAW for G10 and the builder codes of [raw], SECTION for "* - title".
/// </summary>
internal sealed class FakeReader : ReaderBase
{
    private static readonly Dictionary<string, int> s_modalGroups = new()
    {
        ["G0"] = 1,
        ["G1"] = 1,
        ["G90"] = 3,
        ["G91"] = 3,
    };

    // The M codes of the controller itself, which no table of the machine needs to name.
    private static readonly string[] s_ownCodes = ["M2", "M3", "M5", "M30", "M98", "M99"];

    private static readonly string[] s_axes = ["X", "Y", "Z"];

    public override Controller Controller => Controller.Fanuc;

    protected override ISourceTokenizer Tokenizer { get; } = new FakeTokenizer();

    protected override IReadOnlyDictionary<string, int> ModalGroupOfCode => s_modalGroups;

    protected override SourceStructure StructureOf(SourceBlock block)
    {
        StructureRole role = StructureRole.None;
        long? number = null;
        string? label = null;
        var labelsUsed = new List<string>();
        var calls = new List<string>();
        foreach (SourceWord word in block.Words)
        {
            string? code = Code(word);
            if (code == "M98" && block.Find("P") is SourceWord called && called.Number is decimal program)
            {
                calls.Add(decimal.ToInt64(program).ToString(CultureInfo.InvariantCulture));
            }

            if (word.Address == "%")
            {
                role = StructureRole.FileFrame;
            }
            else if (word.Address == "O")
            {
                role = StructureRole.SectionBegin;
                number = (long?)word.Number;
            }
            else if (word.Address == "N")
            {
                label = word.Text.TrimStart('0');
            }
            else if (word.Address == "GOTO")
            {
                labelsUsed.Add(word.Text.TrimStart('0'));
            }
            else if (code is "M30" or "M2")
            {
                role = StructureRole.ProgramEnd;
            }
            else if (code == "M99")
            {
                role = StructureRole.Return;
            }
        }

        return new SourceStructure
        {
            Role = role,
            Number = number,
            Label = label,
            LabelsUsed = labelsUsed,
            Calls = calls,
        };
    }

    protected override bool LeavesUndecided(SourceBlock block)
    {
        foreach (SourceWord word in block.Words)
        {
            string? code = Code(word);
            if (word.Address == "M" && code is not null && !s_ownCodes.Contains(code) && FindFunction(code) is null)
            {
                return true;
            }
        }

        return false;
    }

    protected override void ReadBlock(SourceBlock block)
    {
        if (block.Find("*") is SourceWord title)
        {
            EmitSection(block, title.Text);
            return;
        }

        foreach (SourceWord word in block.Words)
        {
            if (Code(word) == "G10" || IsBuilderCode(word))
            {
                EmitRaw(block, $"{Code(word)} has no NCX word");
                return;
            }
        }

        var words = new List<Word>();
        string? verb = Motion(block, words);
        foreach (SourceWord word in block.Words)
        {
            AddWord(block, word, words);
        }

        if (verb is null && words.Count == 0)
        {
            return;
        }

        NcxBuilder builder = BeginBlock(block);
        if (verb is not null)
        {
            builder.Verb(verb);
        }

        foreach (Word word in words)
        {
            builder.Word(word.Key, word.Addr, word.Value);
        }

        builder.End();
    }

    private string? Motion(SourceBlock block, List<Word> words)
    {
        bool home = false;
        foreach (SourceWord word in block.Words)
        {
            home |= Code(word) == "G28";
        }

        bool incremental = State.ActiveCode(3) == "G91";
        string? verb = null;
        foreach (string axis in s_axes)
        {
            if (block.Find(axis) is not SourceWord word)
            {
                continue;
            }

            if (home)
            {
                words.Add(new Word { Key = axis });
                verb = "HOME";
                continue;
            }

            words.Add(new Word { Key = incremental ? "I" + axis : axis, Value = word.ToNcxNumber()! });
            verb = State.ActiveCode(1) == "G1" ? "LINE" : "RAPID";
        }

        return verb;
    }

    private void AddWord(SourceBlock block, SourceWord word, List<Word> words)
    {
        string? code = Code(word);
        if (word.Address == "F")
        {
            words.Add(new Word { Key = "F", Value = word.ToNcxNumber()! });
        }
        else if (word.Address == "GOTO")
        {
            words.Add(new Word { Key = "JUMP", Value = new IntegerValue((long)word.Number!, word.Text) });
        }
        else if (code == "M3" || code == "M5")
        {
            words.Add(new Word { Key = "SPINDLE", Value = new IdentValue(code == "M3" ? "CW" : "OFF") });
        }
        else if (code == "M98")
        {
            words.Add(new Word { Key = "CALL", Value = block.Find("P")!.ToNcxNumber()! });
            if (block.Find("L") is SourceWord times)
            {
                words.Add(new Word { Key = "TIMES", Value = times.ToNcxNumber()! });
            }
        }
        else if (word.Address == "M" && code is not null && !s_ownCodes.Contains(code))
        {
            words.Add(FunctionWord(code, word));
        }
    }

    // The state of a function table, COOLANT:STANDARD=ON, is the word of the language: the default coolant channel
    // is COOLANT without an address (language 4.6); a code no table names is MFUNC.
    private Word FunctionWord(string code, SourceWord word)
    {
        string? state = FindFunction(code);
        if (state is null)
        {
            return new Word { Key = "MFUNC", Value = word.ToNcxNumber()! };
        }

        string[] keyAndValue = state.Split('=');
        string[] keyAndAddr = keyAndValue[0].Split(':');
        string? addr = keyAndAddr.Length > 1 && keyAndAddr[1] != "STANDARD" ? keyAndAddr[1] : null;
        return new Word { Key = keyAndAddr[0], Addr = addr, Value = new IdentValue(keyAndValue[1]) };
    }

    private static string? Code(SourceWord word)
    {
        return word.Address is "G" or "M" ? NativeCode.Of(word) : null;
    }
}
