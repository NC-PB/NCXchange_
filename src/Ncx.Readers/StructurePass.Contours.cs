using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers;

// The contour a cycle names by a block range as a SUB section of the file (language 4.7, CONTOUR, and 4.7.1;
// machine-config 6, contour; D65).
internal sealed partial class StructurePass
{
    // The name of a contour section: CONTOUR_ and the labels of its first and its last block.
    private const string ContourPrefix = "CONTOUR_";

    // The indices of the source blocks that stand in a contour section, trivia included.
    private readonly HashSet<int> _moved = [];

    // The contour sections of each program or subprogram of the source, in the order their ranges stand in the file.
    private readonly Dictionary<SourceSection, List<ContourRange>> _contours = [];

    // The name of the contour section each cycle block names, by the line of the cycle block.
    private readonly Dictionary<int, string> _contourOfCycle = [];

    // The names the sections of the file take, which a contour section does not take again.
    private HashSet<string>? _sectionNames;

    // A cycle names its contour by the labels of its first and its last block, the range of the contour that follows
    // the cycle block: the reader turns the range into a SUB section of the file, and the compiler writes the block
    // numbers of that section (language 4.7.1; machine-config 6, contour; D65). A range moves when its blocks are plain
    // blocks of its section, without a begin, an end or a return, a label a jump of the section enters, or a jump, a
    // call or a contour of their own, since a SUB section is entered and left as a whole. Another cycle of the section
    // that names the same range, G70 after G71, names the same section; a cycle whose range does not follow it or
    // cannot move names none, and the reader keeps it as its source writes it.
    private void FindContours(List<SourceSection> sections)
    {
        foreach (SourceSection section in sections)
        {
            List<int> blocks = section.Blocks;
            HashSet<string> targets = LabelsUsed(section);
            var ranges = new List<ContourRange>();
            for (int position = 0; position < blocks.Count; position++)
            {
                if (_structures[blocks[position]].Contour is SourceContour contour
                    && FollowingRange(blocks, position, contour, targets) is int last)
                {
                    var range = new ContourRange(contour, NewContourName(contour), blocks[position + 1], blocks[last]);
                    ranges.Add(range);
                    _contourOfCycle[_blocks[blocks[position]].Line] = range.Name;
                }
            }

            foreach (int index in blocks)
            {
                SourceContour? named = _structures[index].Contour;
                List<ContourRange> same = ranges.FindAll(range => range.Contour == named);
                if (named is not null && same.Count == 1)
                {
                    _contourOfCycle.TryAdd(_blocks[index].Line, same[0].Name);
                }
            }

            foreach (ContourRange range in ranges)
            {
                for (int index = range.First; index <= range.Last; index++)
                {
                    _moved.Add(index);
                }
            }

            section.Blocks.RemoveAll(_moved.Contains);
            if (ranges.Count > 0)
            {
                _contours[section] = ranges;
            }
        }
    }

    // The position of the last block of the range that follows the cycle at the position, when the range can move;
    // null otherwise.
    private int? FollowingRange(List<int> blocks, int position, SourceContour contour, HashSet<string> targets)
    {
        if (position + 1 >= blocks.Count || _structures[blocks[position + 1]].Label != contour.First)
        {
            return null;
        }

        for (int last = position + 1; last < blocks.Count; last++)
        {
            SourceStructure structure = _structures[blocks[last]];
            bool plain = structure.Role == StructureRole.None && structure.LabelsUsed.Count == 0
                && structure.Calls.Count == 0 && structure.Contour is null
                && (structure.Label is null || !targets.Contains(structure.Label));
            if (!plain)
            {
                return null;
            }

            if (structure.Label == contour.Last)
            {
                return last;
            }
        }

        return null;
    }

    // The contour sections of a section stand behind its end, in front of the next section or of FILE=END, each as
    // SUB=BEGIN NAME=, its blocks with the trivia among them, and SUB=END: subprograms stand in the file next to the
    // programs, never inside one (language 4.9, 4.13). The SUB=BEGIN starts the section on the reader's side as every
    // SUB=BEGIN does (ReaderBase.BeginSection).
    private void PlanContours(SourceSection section, int? nextBegin)
    {
        if (!_contours.TryGetValue(section, out List<ContourRange>? ranges))
        {
            return;
        }

        List<ReadStep> boundary = Boundary(nextBegin);
        foreach (ContourRange range in ranges)
        {
            SourceBlock first = _blocks[range.First];
            SourceBlock last = _blocks[range.Last];
            boundary.Add(Write(first, first.Line,
                [Frame(SubKey, BeginValue), new Word { Key = NameKey, Value = NameOrLabelValue(range.Name) }]));
            for (int index = range.First; index <= range.Last; index++)
            {
                SourceBlock block = _blocks[index];
                boundary.Add(new ReadStep
                {
                    Kind = block.IsTrivia ? ReadStepKind.Trivia : ReadStepKind.Read,
                    Source = block,
                });
            }

            boundary.Add(Write(last, last.Line, [Frame(SubKey, EndValue)]));
        }
    }

    // CONTOUR_10_20, with a count after it where a section of the file has that name already.
    private string NewContourName(SourceContour contour)
    {
        _sectionNames ??= SectionNames();
        string name = ContourPrefix + contour.First + "_" + contour.Last;
        string unique = name;
        for (int count = 2; !_sectionNames.Add(unique); count++)
        {
            unique = name + "_" + count.ToString(CultureInfo.InvariantCulture);
        }

        return unique;
    }

    // The names the programs and subprograms of the source take: their names and their numbers.
    private HashSet<string> SectionNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (SourceStructure structure in _structures)
        {
            if (structure.Name is not null)
            {
                names.Add(structure.Name);
            }

            if (structure.Number is long number)
            {
                names.Add(number.ToString(CultureInfo.InvariantCulture));
            }
        }

        return names;
    }

    // The contour sections by the lines of the source blocks, for the reader of the family.
    private ContourLayout Layout()
    {
        var moved = new HashSet<int>();
        foreach (int index in _moved)
        {
            moved.Add(_blocks[index].Line);
        }

        return new ContourLayout { Cycles = _contourOfCycle, Moved = moved };
    }

    // A contour range of a section: the labels the cycle names, the name of its SUB section, and the indices of its
    // first and its last block among the source blocks.
    private sealed record ContourRange(SourceContour Contour, string Name, int First, int Last);
}
