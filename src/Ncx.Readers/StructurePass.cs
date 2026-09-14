using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers;

/// <summary>
/// The structure pass on the reader side (language 4.13, D48, D89; controller-mapping 1 and 6): from the role the
/// reader of a controller family gives every source block it lays out the reading plan, the source blocks in file
/// order with the blocks of the file structure among them. The file is framed by FILE=BEGIN NCX=1 and FILE=END, the
/// programs and subprograms of the source become PROGRAM and SUB sections, code a source keeps after its program end
/// stands in front of PROGRAM=END behind a JUMP=END, a Fanuc M99 in a program loops to a LABEL after the header, and
/// the contour a cycle names by a block range becomes a SUB section behind its program (language 4.7.1, D65).
/// </summary>
internal sealed partial class StructurePass
{
    private readonly IReadOnlyList<SourceBlock> _blocks;
    private readonly IReadOnlyList<SourceStructure> _structures;
    private readonly Diagnostics _diagnostics;

    // The written blocks before and after the reading of each source block, made when the first one is added, and the
    // frame block that stands instead of the reading of the leading and the closing % of a Fanuc file.
    private readonly List<ReadStep>?[] _before;
    private readonly List<ReadStep>?[] _after;
    private readonly ReadStep?[] _frame;

    // The first and the last source block with words; -1 in a file without one.
    private readonly int _first = -1;
    private readonly int _last = -1;

    private StructurePass(
        IReadOnlyList<SourceBlock> blocks, IReadOnlyList<SourceStructure> structures, Diagnostics diagnostics)
    {
        _blocks = blocks;
        _structures = structures;
        _diagnostics = diagnostics;
        _before = new List<ReadStep>?[blocks.Count];
        _after = new List<ReadStep>?[blocks.Count];
        _frame = new ReadStep?[blocks.Count];
        for (int index = 0; index < blocks.Count; index++)
        {
            if (!blocks[index].IsTrivia)
            {
                _first = _first < 0 ? index : _first;
                _last = index;
            }
        }
    }

    /// <summary>
    /// Lays out the reading plan of a file.
    /// </summary>
    /// <param name="blocks">Every source block of the file in file order, trivia lines included.</param>
    /// <param name="structures">The structure of each block, in the same order.</param>
    /// <param name="diagnostics">Where the WARNINGs about the structure of the source go.</param>
    /// <param name="contours">The contour sections the plan makes, which the reader of the family asks for.</param>
    /// <returns>The steps in the order the reader takes them.</returns>
    public static List<ReadStep> Plan(IReadOnlyList<SourceBlock> blocks, IReadOnlyList<SourceStructure> structures,
        Diagnostics diagnostics, out ContourLayout contours)
    {
        var pass = new StructurePass(blocks, structures, diagnostics);
        if (pass._first < 0)
        {
            contours = ContourLayout.None;
            return pass.PlanEmptyFile();
        }

        pass.PlanFileBegin();
        List<SourceSection> sections = pass.FindSections();
        pass.DecideKinds(sections);
        pass.FindContours(sections);
        for (int index = 0; index < sections.Count; index++)
        {
            int? nextBegin = index + 1 < sections.Count ? sections[index + 1].Begin : null;
            pass.PlanSection(sections[index], nextBegin);
            pass.PlanContours(sections[index], nextBegin);
        }

        pass.PlanFileEnd();
        contours = pass.Layout();
        return pass.Steps();
    }

    // FILE=BEGIN NCX=1 is the first block of every file: the leading % of a Fanuc file, the start of the file on
    // Heidenhain and Siemens (language 4.1, controller-mapping 1).
    private void PlanFileBegin()
    {
        SourceBlock first = _blocks[_first];
        IReadOnlyList<Word> words = [Frame(FileKey, BeginValue), Number(NcxKey, NcxVersion)];
        if (IsFileBegin(_first))
        {
            _frame[_first] = InPlace(_first, words) with { CarriesSkip = false };
            return;
        }

        Before(_first).Add(Write(first, first.Line, words));
    }

    // FILE=END is the last block of every file: the closing % of a Fanuc file, the end of the file on Heidenhain and
    // Siemens (language 4.1, controller-mapping 1).
    private void PlanFileEnd()
    {
        IReadOnlyList<Word> words = [Frame(FileKey, EndValue)];
        if (IsFileEnd(_last))
        {
            _frame[_last] = InPlace(_last, words) with { CarriesSkip = false };
            return;
        }

        SourceBlock last = _blocks[_last];
        After(_last).Add(Write(last, last.Line, words));
    }

    // A file without a block holds only trivia; it keeps its frame, and the parser reports the missing program
    // (language 4.1, 4.13).
    private List<ReadStep> PlanEmptyFile()
    {
        var steps = new List<ReadStep>();
        foreach (SourceBlock block in _blocks)
        {
            steps.Add(new ReadStep { Kind = ReadStepKind.Trivia, Source = block });
        }

        var frame = new SourceBlock { Line = 1, Text = "" };
        steps.Add(Write(frame, frame.Line, [Frame(FileKey, BeginValue), Number(NcxKey, NcxVersion)]));
        steps.Add(Write(frame, frame.Line, [Frame(FileKey, EndValue)]));
        return steps;
    }

    // Programs and subprograms stand side by side, each from its begin block to the next begin or the end of the
    // file; blocks before the first begin, a Fanuc file without an O line, are a program of their own (language 4.13).
    private List<SourceSection> FindSections()
    {
        var sections = new List<SourceSection>();
        SourceSection? current = null;
        for (int index = _first; index <= _last; index++)
        {
            StructureRole role = _structures[index].Role;
            if (_blocks[index].IsTrivia || _frame[index] is not null || IsFileEnd(index))
            {
                continue;
            }

            if (role is StructureRole.ProgramBegin or StructureRole.SubBegin or StructureRole.SectionBegin)
            {
                current = new SourceSection
                {
                    Begin = index,
                    Kind = role == StructureRole.SubBegin ? SectionKind.Sub : SectionKind.Program,
                    Undecided = role == StructureRole.SectionBegin,
                };
                sections.Add(current);
                continue;
            }

            if (current is null)
            {
                current = new SourceSection { Kind = SectionKind.Program };
                sections.Add(current);
            }

            current.Blocks.Add(index);
        }

        return sections;
    }

    // A section the source does not name, the Fanuc O program, is a subprogram when a block of the file calls it: a
    // CALL enters a subprogram with the state of its caller, the other programs of the file run only when the job runs
    // them, and a CALL of a program is an ERROR because programs are entered from the job only (language 4.13; virtual
    // machine 3.6 and 3.9); the subprogram is Onnnn ... M99 after the caller's M30 (controller-mapping 1, 6). An M30
    // does not make a called section a program: in a subprogram it ends the program from the call, as a Fanuc M30 in a
    // subprogram does (virtual machine 3.6).
    // TODO(question): wave-2 question #18, which widens #14. The documents do not say what an O section is that no
    // block of the file calls: an O program of its own (several O programs in one file are several programs,
    // controller-mapping 1) whose M99 loops as in a main program (controller-mapping 6), or a subprogram that another
    // file calls (Onnnn ... M99 in its own file, controller-mapping 1, 6). Until that is answered it is a program when
    // it holds an M30 or M2 and a subprogram otherwise, and when no section of the file is a program the first such
    // section is, since a file holds at least one program (language 4.13). A subprogram in its own file therefore
    // reads as a program: one that holds an M30 (virtual machine 3.6) with its M99 as the loop, one that holds only
    // Onnnn ... M99 as a program that loops.
    private void DecideKinds(List<SourceSection> sections)
    {
        HashSet<string> called = CalledNames();
        bool hasProgram = false;
        foreach (SourceSection section in sections)
        {
            if (section.Undecided)
            {
                bool isProgram = !IsCalled(section, called)
                    && FindRole(section, StructureRole.ProgramEnd, unskippedOnly: false) is not null;
                section.Kind = isProgram ? SectionKind.Program : SectionKind.Sub;
            }

            hasProgram |= section.Kind == SectionKind.Program;
        }

        if (hasProgram)
        {
            return;
        }

        foreach (SourceSection section in sections)
        {
            if (section.Undecided && !IsCalled(section, called))
            {
                section.Kind = SectionKind.Program;
                return;
            }
        }
    }

    // The subprograms the blocks of the file call, by name or number (language 4.9, CALL).
    private HashSet<string> CalledNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (SourceStructure structure in _structures)
        {
            names.UnionWith(structure.Calls);
        }

        return names;
    }

    // A section is called by the name its SUB=BEGIN gets: the name the source gives, else its number, O0100 as 100
    // (controller-mapping 1, 6; language 4.9).
    private bool IsCalled(SourceSection section, HashSet<string> called)
    {
        if (section.Begin is not int begin)
        {
            return false;
        }

        SourceStructure structure = _structures[begin];
        string? name = structure.Name ?? structure.Number?.ToString(CultureInfo.InvariantCulture);
        return name is not null && called.Contains(name);
    }

    private List<ReadStep> Steps()
    {
        var steps = new List<ReadStep>();
        for (int index = 0; index < _blocks.Count; index++)
        {
            // A block of a contour stands in its SUB section behind the end of its program (PlanContours); what the
            // plan writes in its place, the end of that program, stays here.
            if (_moved.Contains(index))
            {
                steps.AddRange(_before[index] ?? []);
                steps.AddRange(_after[index] ?? []);
                continue;
            }

            SourceBlock block = _blocks[index];
            if (block.IsTrivia)
            {
                steps.Add(new ReadStep { Kind = ReadStepKind.Trivia, Source = block });
                continue;
            }

            steps.AddRange(_before[index] ?? []);
            steps.Add(_frame[index] ?? new ReadStep { Kind = ReadStepKind.Read, Source = block });
            steps.AddRange(_after[index] ?? []);
        }

        return steps;
    }

    private List<ReadStep> Before(int index)
    {
        return _before[index] ??= [];
    }

    private List<ReadStep> After(int index)
    {
        return _after[index] ??= [];
    }

    // The leading % of a Fanuc file is FILE=BEGIN, the closing % FILE=END (controller-mapping 1).
    private bool IsFileBegin(int index)
    {
        return index == _first && _structures[index].Role == StructureRole.FileFrame;
    }

    private bool IsFileEnd(int index)
    {
        return index == _last && index != _first && _structures[index].Role == StructureRole.FileFrame;
    }
}
