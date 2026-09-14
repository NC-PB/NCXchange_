using Ncx.Core.Model;

namespace Ncx.Readers;

// The block range a repeat names, copied into a SUB section of the file that the repeat calls (controller-mapping 6,
// REPEAT + TIMES: a block range, lowered to a SUB; language 4.9, 4.13).
internal sealed partial class StructurePass
{
    // The name of a repeat section: REPEAT_ and the labels of its first and its last block, the label once for a range
    // of one block.
    private const string RepeatPrefix = "REPEAT_";

    // The repeat sections of each program or subprogram of the source, in the order their repeats stand in the file.
    private readonly Dictionary<SourceSection, List<RepeatRange>> _repeats = [];

    // The name of the repeat section each repeat block calls, by the line of the repeat block.
    private readonly Dictionary<int, string> _repeatOfBlock = [];

    // A repeat names a block range before it in its section by the labels of its first and its last block: the first
    // block is the last one before the repeat that carries the first label, the last block the first one from there on
    // that carries the last label. The range becomes a SUB section that the repeat calls, and its blocks stay where
    // they stand, since the control runs them there as well. A range is copied when its blocks are plain blocks of its
    // section, without a begin, an end or a return, or a jump, a call, a contour or a repeat of their own, since a SUB
    // section is entered and left as a whole. Another repeat of the section that names the same range calls the same
    // section; a repeat whose range cannot be copied calls none, and the reader keeps it as its source writes it.
    // TODO(question): controller-mapping 6 lowers REPEAT start end P=k to a SUB, but does not say whether the range
    // leaves its place for the SUB, called there as well, or stays where it stands with the SUB a copy of it, nor how
    // the SUB is named; the range stays and is copied, so that every source block still reads where the control runs
    // it, into a SUB named REPEAT_ and its labels.
    private void FindRepeats(List<SourceSection> sections)
    {
        foreach (SourceSection section in sections)
        {
            List<int> blocks = section.Blocks;
            var ranges = new List<RepeatRange>();
            for (int position = 0; position < blocks.Count; position++)
            {
                if (_structures[blocks[position]].Repeat is not SourceRepeat repeat
                    || PrecedingRange(blocks, position, repeat) is not (int first, int last))
                {
                    continue;
                }

                List<int> range = blocks.GetRange(first, last - first + 1);
                RepeatRange? same = ranges.Find(candidate => candidate.Blocks.SequenceEqual(range));
                if (same is null)
                {
                    string name = RepeatPrefix + (repeat.First == repeat.Last ? repeat.First
                        : repeat.First + "_" + repeat.Last);
                    same = new RepeatRange(NewSectionName(name), range);
                    ranges.Add(same);
                }

                _repeatOfBlock[_blocks[blocks[position]].Line] = same.Name;
            }

            if (ranges.Count > 0)
            {
                _repeats[section] = ranges;
            }
        }
    }

    // The positions of the first and the last block of the range the repeat at the position names, when the range can
    // be copied; null otherwise.
    private (int First, int Last)? PrecedingRange(List<int> blocks, int position, SourceRepeat repeat)
    {
        int first = position - 1;
        while (first >= 0 && _structures[blocks[first]].Label != repeat.First)
        {
            first--;
        }

        for (int last = first; first >= 0 && last < position; last++)
        {
            SourceStructure structure = _structures[blocks[last]];
            bool plain = structure.Role == StructureRole.None && structure.LabelsUsed.Count == 0
                && structure.Calls.Count == 0 && structure.Contour is null && structure.Repeat is null;
            if (!plain)
            {
                return null;
            }

            if (structure.Label == repeat.Last)
            {
                return (first, last);
            }
        }

        return null;
    }

    // The repeat sections of a section stand behind its end and its contour sections, in front of the next section or
    // of FILE=END, each as SUB=BEGIN NAME=, its blocks read once more, and SUB=END: subprograms stand in the file next
    // to the programs, never inside one (language 4.9, 4.13). The SUB=BEGIN starts the section on the reader's side as
    // every SUB=BEGIN does (ReaderBase.BeginSection).
    private void PlanRepeats(SourceSection section, int? nextBegin)
    {
        if (!_repeats.TryGetValue(section, out List<RepeatRange>? ranges))
        {
            return;
        }

        List<ReadStep> boundary = Boundary(nextBegin);
        foreach (RepeatRange range in ranges)
        {
            SourceBlock first = _blocks[range.Blocks[0]];
            SourceBlock last = _blocks[range.Blocks[^1]];
            boundary.Add(Write(first, first.Line,
                [Frame(SubKey, BeginValue), new Word { Key = NameKey, Value = NameOrLabelValue(range.Name) }]));
            foreach (int index in range.Blocks)
            {
                boundary.Add(new ReadStep { Kind = ReadStepKind.Read, Source = _blocks[index] });
            }

            boundary.Add(Write(last, last.Line, [Frame(SubKey, EndValue)]));
        }
    }

    // A repeat range of a section: the name of its SUB section and the indices of its blocks with words among the
    // source blocks, in file order.
    private sealed record RepeatRange(string Name, List<int> Blocks);
}
