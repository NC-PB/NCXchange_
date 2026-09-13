using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers;

// The programs and subprograms of the source as PROGRAM and SUB sections (language 4.13, D48, D89; controller-mapping
// 1 and 6).
internal sealed partial class StructurePass
{
    private void PlanSection(SourceSection section, int? nextBegin)
    {
        if (section.Kind == SectionKind.Program)
        {
            PlanProgram(section, nextBegin);
        }
        else
        {
            PlanSub(section, nextBegin);
        }
    }

    // A program runs from PROGRAM=BEGIN to PROGRAM=END, which is its last block and appears once (language 4.13).
    private void PlanProgram(SourceSection section, int? nextBegin)
    {
        // PROGRAM=BEGIN with NAME and NUMBER: Oxxxx (name), BEGIN PGM name; blocks before the first begin get a
        // program without a name (controller-mapping 1).
        int header = section.Begin ?? section.Blocks[0];
        if (section.Begin is int begin)
        {
            Before(begin).Add(ProgramBegin(begin));
        }
        else
        {
            Before(header).Add(Write(_blocks[header], _blocks[header].Line, [Frame(ProgramKey, BeginValue)]));
        }

        // M99 in a program, where no subprogram is active, jumps back to the first block: the reader writes
        // LABEL=START after the header and JUMP=START for the M99, SKIP JUMP=START for /M99 (controller-mapping 6,
        // language 4.13).
        if (FindRole(section, StructureRole.Return, unskippedOnly: false) is not null)
        {
            Before(header).Add(Write(_blocks[header], _blocks[header].Line, [Frame(LabelKey, StartLabel)]));
        }

        // The end the main flow reaches is the first M30 or M2 without a block skip; a /M30 is not the program end,
        // it is SKIP JUMP=END, and so is an end inside the code that the source keeps after the end, the N2 M2 case
        // (controller-mapping 6, language 4.13).
        HashSet<string> targets = LabelsUsed(section);
        int? mainEnd = FindRole(section, StructureRole.ProgramEnd, unskippedOnly: true);
        int last = section.Blocks.Count > 0 ? section.Blocks[^1] : header;
        foreach (int index in section.Blocks)
        {
            PlanLabel(index, targets);
            StructureRole role = _structures[index].Role;
            if (role == StructureRole.Return)
            {
                After(index).Add(InPlace(index, [Frame(JumpKey, StartLabel)]));
            }
            else if (role == StructureRole.ProgramEnd && index == mainEnd && index == last)
            {
                After(index).Add(InPlace(index, [Frame(ProgramKey, EndValue)]) with { CarriesSkip = false });
            }
            else if (role == StructureRole.ProgramEnd)
            {
                After(index).Add(InPlace(index, [Frame(JumpKey, EndValue)]));
            }
        }

        // Code that a source keeps after its end has no place after PROGRAM=END, because nothing follows the end of a
        // program: it stays in front of the end behind the JUMP=END written for the M30, and PROGRAM=END keeps the
        // line of the M30 (language 4.13, D89). Code after the end that no jump enters is kept the same way, where the
        // virtual machine reports it unreachable (language 4.13).
        if (mainEnd is int end && end != last)
        {
            WriteAtBoundary(_blocks[end].Line, [Frame(ProgramKey, EndValue)], nextBegin);
            return;
        }

        if (mainEnd is not null)
        {
            return;
        }

        // A program without an end in the source ends after its last block; that is the M99 loop, or a WARNING when
        // the source has no end at all (language 4.13; controller-mapping 1, the missing M30 of Klartext).
        // TODO(question): controller-mapping 1 makes the missing M30 of Klartext a WARNING and says nothing of a Fanuc
        // or Siemens program without M30, M2 or M17; every reader reports it with the same WARNING.
        bool loops = _structures[last].Role == StructureRole.Return && !_blocks[last].BlockSkip;
        if (!loops && FindRole(section, StructureRole.ProgramEnd, unskippedOnly: false) is null)
        {
            _diagnostics.Warning(_blocks[header].Line, DiagnosticCodes.ProgramEndMissing, string.Create(
                CultureInfo.InvariantCulture,
                $"The program of line {_blocks[header].Line} has no end in the source (M30, M2); the reader ends it "
                + $"with PROGRAM=END after its last block, the last block of every program (language 4.13)."));
        }

        WriteAtBoundary(_blocks[last].Line, [Frame(ProgramKey, EndValue)], nextBegin);
    }

    // A subprogram runs from SUB=BEGIN NAME= to SUB=END, the return to the caller, which is its last block; RETURN
    // returns before it (language 4.9, 4.13; controller-mapping 6).
    private void PlanSub(SourceSection section, int? nextBegin)
    {
        int begin = section.Begin ?? section.Blocks[0];
        Before(begin).Add(SubBegin(begin));

        // The M99 or LBL 0 that is the last block of the subprogram is its SUB=END; a return before it, with code
        // after it that a jump enters, or under a block skip, is RETURN (language 4.9, 4.13). An M30 in a subprogram,
        // such as one in a Fanuc O section that M98 calls, ends the program from the call: JUMP=END (virtual machine
        // 3.6).
        HashSet<string> targets = LabelsUsed(section);
        int last = section.Blocks.Count > 0 ? section.Blocks[^1] : begin;
        bool endsWithReturn = last != begin
            && _structures[last].Role == StructureRole.Return
            && !_blocks[last].BlockSkip;
        foreach (int index in section.Blocks)
        {
            PlanLabel(index, targets);
            StructureRole role = _structures[index].Role;
            if (role == StructureRole.Return && index == last && endsWithReturn)
            {
                After(index).Add(InPlace(index, [Frame(SubKey, EndValue)]) with { CarriesSkip = false });
            }
            else if (role == StructureRole.Return)
            {
                After(index).Add(InPlace(index, [new Word { Key = ReturnKey }]));
            }
            else if (role == StructureRole.ProgramEnd)
            {
                After(index).Add(InPlace(index, [Frame(JumpKey, EndValue)]));
            }
        }

        if (endsWithReturn)
        {
            return;
        }

        // A subprogram without a return in the source ends after its last block (language 4.13).
        if (FindRole(section, StructureRole.Return, unskippedOnly: true) is null)
        {
            _diagnostics.Warning(_blocks[begin].Line, DiagnosticCodes.SubReturnMissing, string.Create(
                CultureInfo.InvariantCulture,
                $"The subprogram of line {_blocks[begin].Line} has no return in the source (M99, LBL 0, RET); the "
                + $"reader ends it with SUB=END after its last block, the last block of every subprogram "
                + $"(language 4.13)."));
        }

        WriteAtBoundary(_blocks[last].Line, [Frame(SubKey, EndValue)], nextBegin);
    }

    // PROGRAM=BEGIN NAME="..." NUMBER=n; the comment after O is the name when the source gives none, Oxxxx (name)
    // (controller-mapping 1, language 4.1).
    // TODO(question): examples/2.5D_FRAESEN.ncx keeps the name as written, NAME="2.5D FRAESEN" for O0001 (2.5D
    // FRAESEN), while examples/INCREMENTAL_SUB.ncx writes NAME="SLOT_ROW" for O0003 (SLOT ROW); the name is taken as
    // written, trimmed, the form of the example that phase 3 converts from its source.
    private ReadStep ProgramBegin(int begin)
    {
        SourceStructure structure = _structures[begin];
        string? comment = _blocks[begin].Comment;
        bool usesComment = structure.Name is null && !string.IsNullOrEmpty(comment);
        string? name = usesComment ? comment : structure.Name;
        var words = new List<Word> { Frame(ProgramKey, BeginValue) };
        if (name is not null)
        {
            words.Add(new Word { Key = NameKey, Value = new StringValue(name) });
        }

        if (structure.Number is long number)
        {
            words.Add(Number(NumberKey, number));
        }

        return InPlace(begin, words) with { CarriesSkip = false, TakesComment = !usesComment, UsesComment = usesComment };
    }

    // SUB=BEGIN NAME=n: the name the source gives, else its number, O0100 as NAME=100 (controller-mapping 1, 6;
    // language 4.9).
    private ReadStep SubBegin(int begin)
    {
        SourceStructure structure = _structures[begin];
        var words = new List<Word> { Frame(SubKey, BeginValue) };
        if (structure.Name is not null)
        {
            words.Add(new Word { Key = NameKey, Value = NameOrLabelValue(structure.Name) });
        }
        else if (structure.Number is long number)
        {
            words.Add(Number(NameKey, number));
        }

        return InPlace(begin, words) with { CarriesSkip = false };
    }

    // LABEL: a label of the source that a block of its program or subprogram jumps to, Nn targeted by a GOTO; the
    // labels are local to their section (controller-mapping 6; language 4.9).
    private void PlanLabel(int index, HashSet<string> targets)
    {
        string? label = _structures[index].Label;
        if (label is not null && targets.Contains(label))
        {
            Before(index).Add(InPlace(index, [new Word { Key = LabelKey, Value = NameOrLabelValue(label) }]));
        }
    }

    // The end that a section gets after its last block stands after the trivia that follow it, in front of the next
    // section or of FILE=END; written there, the trivia keep their place under the line numbers of the builder
    // (wave-1 question #77).
    private void WriteAtBoundary(int line, IReadOnlyList<Word> words, int? nextBegin)
    {
        int host = nextBegin ?? _last;
        ReadStep end = Write(_blocks[host], line, words);
        if (nextBegin is not null || IsFileEnd(_last))
        {
            Before(host).Add(end);
        }
        else
        {
            After(host).Add(end);
        }
    }

    private int? FindRole(SourceSection section, StructureRole role, bool unskippedOnly)
    {
        foreach (int index in section.Blocks)
        {
            if (_structures[index].Role == role && !(unskippedOnly && _blocks[index].BlockSkip))
            {
                return index;
            }
        }

        return null;
    }

    private HashSet<string> LabelsUsed(SourceSection section)
    {
        var labels = new HashSet<string>(StringComparer.Ordinal);
        if (section.Begin is int begin)
        {
            labels.UnionWith(_structures[begin].LabelsUsed);
        }

        foreach (int index in section.Blocks)
        {
            labels.UnionWith(_structures[index].LabelsUsed);
        }

        return labels;
    }
}
