using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;
using Ncx.Core.Writing;

namespace Ncx.Readers.Siemens;

/// <summary>
/// Reads SINUMERIK 840D sl programs into canonical NCX (controllers siemens.md 11; controller-mapping, the Siemens
/// column of every section). The structure pass lays out the file from its units: every %_N_NAME_MPF a program, every
/// %_N_NAME_SPF and PROC a subprogram, every _INI unit a program of RAW blocks (SiemensUnits); the header of D34
/// follows PROGRAM=BEGIN, and a subprogram runs with the state its callers agree on (SiemensCalls). Every block is read
/// concern by concern, each in a file of its own: the RAW statements (SiemensRaw), the jumps, structures, variables
/// and calls (SiemensFlow, SiemensStructures, SiemensVariables, SiemensCalls), the G groups (SiemensGroups), the pole
/// (SiemensPolar), the frames (SiemensFrames, SiemensTilt), the transformations and the tolerance
/// (SiemensTransformations), the tools and spindles (SiemensTools, SiemensSpindles), the channels (SiemensChannels),
/// the cycles (SiemensCycles, SiemensPatterns), the motion (SiemensMotion, SiemensArcs, SiemensCorners) and the M
/// functions (SiemensFunctions). What NCX cannot express stays RAW with a WARNING (D5).
/// </summary>
public sealed class SiemensReader : ReaderBase
{
    // The label after the header that GOTOS jumps to (controller-mapping 1, JUMP=END).
    private const string StartLabel = "START";

    private readonly SiemensTokenizer _tokenizer = new();
    private SiemensState? _siemens;
    private TemplateSet? _templates;

    /// <summary>
    /// The Siemens family, SINUMERIK 840D and 840D sl (controllers siemens.md).
    /// </summary>
    public override Controller Controller => Controller.Siemens;

    /// <summary>
    /// The tokenizer of SINUMERIK programs.
    /// </summary>
    protected override ISourceTokenizer Tokenizer => _tokenizer;

    /// <summary>
    /// The modal G codes of the SINUMERIK groups (controllers siemens.md 2).
    /// </summary>
    protected override IReadOnlyDictionary<string, int> ModalGroupOfCode => SiemensGroups.CodeGroups;

    /// <summary>
    /// The structure of a SINUMERIK block: a unit header or a PROC begins a program or a subprogram, M30 and M2 end the
    /// program, M17 and RET return from a subprogram and M17 also ends a main program, a label or a block number that a
    /// jump of its unit enters is a label, the jumps and repeats name their labels (controllers siemens.md 1, 8, 11
    /// rules
    /// 1 and 6; controller-mapping 1, 6; language 4.13).
    /// </summary>
    /// <param name="block">A source block with words.</param>
    protected override SourceStructure StructureOf(SourceBlock block)
    {
        SiemensState state = Facts();
        SiemensUnit? unit = state.Units.UnitOf(block);
        // The section of an _INI unit is named NAME_INI after its header, since the program of an archive that it
        // initializes has the name NAME (virtual machine 3.6: every section of a file has a name of its own).
        if (unit is not null && ReferenceEquals(unit.Begin, block))
        {
            return new SourceStructure
            {
                Role = unit.Kind == SiemensUnitKind.Sub ? StructureRole.SubBegin : StructureRole.ProgramBegin,
                Name = unit.Kind == SiemensUnitKind.Ini ? unit.Name + "_INI" : unit.Name,
            };
        }

        if (unit is null)
        {
            return SourceStructure.None;
        }

        if (unit.Kind == SiemensUnitKind.Ini)
        {
            // An _INI unit has no end in the source: its last block ends its section, without the WARNING of a program
            // without M30 (controllers siemens.md 11 rule 1).
            return unit.Blocks.Count > 0 && ReferenceEquals(unit.Blocks[^1], block)
                ? new SourceStructure { Role = StructureRole.ProgramEnd }
                : SourceStructure.None;
        }

        var labelsUsed = new List<string>();
        foreach (string target in SiemensUnits.TargetsOf(block))
        {
            if (unit.Targets.Contains(target))
            {
                labelsUsed.Add(target);
            }
        }

        var calls = new List<string>();
        foreach (string name in SiemensCalls.NamesCalledBy(block))
        {
            if (state.Units.FindSub(name) is not null)
            {
                calls.Add(name);
            }
        }

        // A repeated range that does not end directly before its REPEAT becomes a SUB section of the structure pass,
        // found by the labels of its first and its last block (controller-mapping 6, REPEAT + TIMES).
        SourceRepeat? repeat = null;
        if (unit.Repeats.TryGetValue(block, out SiemensRepeat? range) && range.IsRange
            && LabelOf(range.Blocks[0], unit) is string first && LabelOf(range.Blocks[^1], unit) is string last)
        {
            repeat = new SourceRepeat(first, last);
        }

        return new SourceStructure
        {
            Role = RoleOf(block, unit),
            Label = LabelOf(block, unit),
            LabelsUsed = labelsUsed,
            Calls = calls,
            Repeat = repeat,
        };
    }

    /// <summary>
    /// A block leaves an M function undecided that neither the controller nor a table of the machine names (D40, D66).
    /// </summary>
    /// <param name="block">A source block with words.</param>
    protected override bool LeavesUndecided(SourceBlock block)
    {
        SiemensState state = Facts();
        var reading = new SiemensBlock(block, _tokenizer, state, Machine, _templates!, Diagnostics);
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "M" && !SiemensFunctions.Names(reading, word))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A program starts from the initial state of the control with the complete header of D34, and GOTOS finds its
    /// label
    /// after it; a subprogram from what its callers agree on (virtual machine 3.9); an _INI unit has no header.
    /// </summary>
    /// <param name="kind">A program or a subprogram.</param>
    /// <param name="begin">The unit header, the PROC, or the first block of a file without a header.</param>
    protected override void BeginSection(SectionKind kind, SourceBlock begin)
    {
        SiemensState state = Facts();
        SiemensStructures.ReportOpen(state, Diagnostics);
        SiemensUnit? unit = state.Units.UnitOf(begin);
        state.BeginUnit(unit);
        state.AngleSecondLine = null;
        state.StartLabel = null;
        if (kind == SectionKind.Sub)
        {
            // The SUB section of a repeated range begins with a block of its program, not with the begin of a unit, and
            // runs with the state its REPEATs agree on (controller-mapping 6, REPEAT + TIMES; virtual machine 3.9).
            bool repeated = unit is not null && !ReferenceEquals(unit.Begin, begin);
            state.Facts.TakeFrom(repeated ? state.EntryOfRepeat(begin) : state.EntryOf(unit?.Name));
            state.ForgetPositions();
            Derive(state);
            return;
        }

        state.Facts.TakeFrom(new SiemensFacts());
        state.ForgetPositions();
        Derive(state);
        if (unit?.Kind == SiemensUnitKind.Ini)
        {
            return;
        }

        WriteHeader(state, begin);
        if (unit?.JumpsToStart == true)
        {
            string label = StartLabel;
            for (int count = 2; unit.Labels.Contains(label); count++)
            {
                label = StartLabel + "_" + count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            state.StartLabel = label;
            Builder.Begin(begin.Line).Word("LABEL", null, new IdentValue(label)).End();
        }
    }

    /// <summary>
    /// Reads a SINUMERIK block concern by concern and writes the NCX blocks it reads into, or keeps it as RAW
    /// (controllers siemens.md 11).
    /// </summary>
    /// <param name="block">A source block with words that no reader rule claimed.</param>
    protected override void ReadBlock(SourceBlock block)
    {
        SiemensState state = Facts();
        var reading = new SiemensBlock(block, _tokenizer, state, Machine, _templates!, Diagnostics)
        {
            RepeatSection = RepeatOf(block),
        };
        if (state.Unit?.Kind == SiemensUnitKind.Ini)
        {
            // An _INI unit holds tool data and machine function groups, not NC code: RAW blocks of its own program
            // section (controllers siemens.md 11 rule 1; controller-mapping 6).
            EmitRaw(block, "an _INI unit holds tool data and machine function groups, not NC code (controllers "
                + "siemens.md 11 rule 1)");
            return;
        }

        // A reader never throws on bad input (code-guidelines 4): a number beyond the range the reader computes with
        // keeps its block as RAW.
        SiemensFacts before = state.Facts.Copy();
        try
        {
            ReadStructureWords(reading);

            // A block of a structure kept as RAW is RAW as well, since NCX cannot branch on its condition (D5).
            if (!SiemensStructures.ReadInRaw(reading))
            {
                SiemensRaw.Read(reading);
                SiemensFlow.Read(reading);
                SiemensGroups.Read(reading);
                SiemensPolar.ReadPole(reading);
                SiemensFrames.Read(reading);
                SiemensTilt.Read(reading);
                SiemensTransformations.Read(reading);
                SiemensTools.Read(reading);
                SiemensSpindles.Read(reading);
                SiemensChannels.Read(reading);
                SiemensCycles.Read(reading);
                SiemensMotion.Read(reading);
                SiemensFunctions.Read(reading);
                KeepUnreadAsRaw(reading);
            }
        }
        catch (OverflowException)
        {
            reading.Draft.KeepAsRaw("a number of the block is beyond the range the reader computes with");
        }

        Write(reading, before);
        if (!reading.Draft.IsRaw)
        {
            SiemensCalls.Enter(reading);
        }

        Derive(state);
        if (state.Unit is SiemensUnit unit && unit.Blocks.Count > 0 && ReferenceEquals(unit.Blocks[^1], block))
        {
            SiemensStructures.ReportOpen(state, Diagnostics);
        }
    }

    // M30 and M2 end the program and M17 also ends a main program; M17 and RET return from a subprogram
    // (controller-mapping 1, PROGRAM=END; 6, SUB=END).
    private static StructureRole RoleOf(SourceBlock block, SiemensUnit unit)
    {
        foreach (SourceWord word in block.Words)
        {
            string? code = SiemensBlock.CodeOf(word);
            if (code is "M30" or "M2")
            {
                return StructureRole.ProgramEnd;
            }

            if (code == "M17" || (word.Address == "RET" && word.Text.Length == 0))
            {
                if (unit.Kind == SiemensUnitKind.Sub)
                {
                    return StructureRole.Return;
                }

                return code == "M17" ? StructureRole.ProgramEnd : StructureRole.None;
            }
        }

        return StructureRole.None;
    }

    // The label a jump of the unit enters: NAME of NAME:, else the block number N300 (controllers siemens.md 8); else
    // the label by which a repeat names the block as the first or the last of a range that becomes a SUB section
    // (controller-mapping 6, REPEAT + TIMES).
    private static string? LabelOf(SourceBlock block, SiemensUnit unit)
    {
        if (SiemensUnits.LabelOf(block) is string label && unit.Targets.Contains(label))
        {
            return label;
        }

        if (SiemensUnits.NumberLabelOf(block) is string number && unit.Targets.Contains(number))
        {
            return number;
        }

        return unit.RangeLabels.GetValueOrDefault(block);
    }

    // Writers emit a complete header; configuration defaults apply to source readers only (D34): FEED_MODE=PER_MIN,
    // COMP=OFF, units_default, WORKPLANE=XY (wave-1 question #73), DIAMETER=ON where the X axis is programmed in
    // diameters (D60), CYCLE=OFF. The codes of the source header then write no word of their own.
    private void WriteHeader(SiemensState state, SourceBlock begin)
    {
        SiemensFacts facts = state.Facts;
        string? units = Machine.Machine.UnitsDefault?.ToUpperInvariant();
        NcxBuilder builder = Builder.Begin(begin.Line);
        Header(builder, facts, "FEED_MODE", "PER_MIN");
        Header(builder, facts, "COMP", "OFF");
        if (units is "MM" or "INCH")
        {
            Header(builder, facts, "UNITS", units);
        }

        Header(builder, facts, "WORKPLANE", "XY");
        if (Machine.ResolveAxis("X")?.Programming == Programming.Diameter)
        {
            Header(builder, facts, "DIAMETER", "ON");
            facts.DiameterMode = "DIAMON";
        }

        builder.Word("CYCLE", null, new IdentValue("OFF")).End();
    }

    private static void Header(NcxBuilder builder, SiemensFacts facts, string key, string value)
    {
        builder.Word(key, null, new IdentValue(value));
        facts.TakeChange(key, value);
    }

    // The words of the file structure are the structure pass's: the unit header, N, the program ends and returns; a
    // label that no jump enters is written as a LABEL of its own, the restart labels of the posts
    // (controller-mapping 6,
    // LABEL; machine-builders 2, Monforts), and a label a jump enters is reached with a state the blocks before it do
    // not tell (virtual machine 3.6).
    private static void ReadStructureWords(SiemensBlock reading)
    {
        SiemensUnit? unit = reading.Unit;
        foreach (SourceWord word in reading.Source.Words)
        {
            string? code = SiemensBlock.CodeOf(word);
            if (word.Address is "%" or "N" || code is "M30" or "M2" || (code == "M17" && unit is not null))
            {
                reading.MarkRead(word);
            }
            else if (word.Address == "RET" && word.Text.Length == 0)
            {
                reading.MarkRead(word);
                if (unit?.Kind != SiemensUnitKind.Sub)
                {
                    // TODO(question): siemens 1 accepts M17 in a main program as its end and does not say what RET
                    // does there; RET in a main program stays RAW.
                    reading.Draft.KeepAsRaw("RET returns from a subprogram, and this is a main program (controllers "
                        + "siemens.md 1)");
                }
            }
            else if (word.Address == ":")
            {
                ReadLabel(reading, word);
            }
        }

        if (unit is not null && SiemensUnits.NumberLabelOf(reading.Source) is string number
            && unit.Targets.Contains(number))
        {
            reading.Siemens.ForgetPositions();
            reading.Facts.Written.Clear();
        }
    }

    private static void ReadLabel(SiemensBlock reading, SourceWord word)
    {
        reading.MarkRead(word);
        SiemensUnit? unit = reading.Unit;
        string label = word.Text;
        if (!SiemensUnits.IsWritable(label))
        {
            reading.Draft.KeepAsRaw($"the label {label} is no label NCX can write (language 4.9, LABEL)");
            return;
        }

        if (unit is not null && unit.Targets.Contains(label))
        {
            reading.Siemens.ForgetPositions();
            reading.Facts.Written.Clear();
            return;
        }

        // ENDLABEL: ends the range of a REPEAT LABEL P=, which a unit may hold more than once; it belongs to the
        // repeat and is no LABEL of its own (controllers siemens.md 8; controller-mapping 6, REPEAT + TIMES).
        if (string.Equals(label, "ENDLABEL", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        reading.Draft.Before.Add(new SiemensDraftBlock().Add("LABEL", new IdentValue(label)));
    }

    // A word no concern reads is none the reader maps; the block is kept as RAW, nothing is dropped (D5).
    // TODO(question): siemens 8 calls a subprogram by its name alone or with its arguments, and siemens 11 rule 8 keeps
    // every builder cycle the configuration does not name as RAW; a name that is neither a subprogram of the file nor
    // announced by EXTERN cannot be told from a builder cycle, and stays RAW.
    private static void KeepUnreadAsRaw(SiemensBlock reading)
    {
        List<SourceWord> unread = reading.Unread();
        if (unread.Count > 0)
        {
            reading.Draft.KeepAsRaw($"{reading.SpanOf(unread[0])} has no NCX word the Siemens reader maps here; a "
                + "builder cycle the configuration does not name stays RAW (controllers siemens.md 11 rule 8)");
        }
    }

    private void Write(SiemensBlock reading, SiemensFacts before)
    {
        SiemensDraft draft = reading.Draft;
        SiemensFacts facts = reading.Facts;
        if (draft.RawReason is string reason)
        {
            EmitRaw(reading.Source, reason);
            if (!draft.RawKeepsState)
            {
                // The control runs the RAW block and the reader does not know what it changed.
                reading.Siemens.ForgetPositions();
                facts.ForgetChangesSince(before);
                facts.Written.Clear();
                facts.WrittenFeed = null;
            }

            return;
        }

        if (draft.CommentLine is string comment)
        {
            Builder.Trivia(comment);
        }

        foreach (SiemensWarning warning in draft.Warnings)
        {
            Diagnostics.Warning(reading.Line, warning.Code, warning.Message);
        }

        if (draft.RawWords.Count > 0 && draft.HoldsNoWord())
        {
            EmitRaw(reading.Source, $"{string.Join(" ", draft.RawWords)} have no NCX word, kept as RAW modal words "
                + "(controllers siemens.md 2, 11 rule 8)");
            return;
        }

        if (draft.RawWords.Count > 0)
        {
            string text = string.Join(" ", draft.RawWords);
            draft.Main.Add("RAW", RawEmitter.DialectOf(reading.Source, Machine, Controller), new StringValue(text));
            Diagnostics.Warning(reading.Line, DiagnosticCodes.SiemensWordsKeptAsRaw,
                $"{text} of the block {(draft.RawWords.Count == 1 ? "has" : "have")} no NCX word and "
                + $"{(draft.RawWords.Count == 1 ? "is" : "are")} kept as a RAW word of the block, written back in "
                + "place by the Siemens compiler (controllers siemens.md 2, 11 rule 8).");
        }

        foreach (SiemensDraftBlock block in draft.InOrder())
        {
            if (block.IsEmpty)
            {
                continue;
            }

            NcxBuilder builder = BeginBlock(reading.Source);
            if (block.Verb is not null)
            {
                builder.Verb(block.Verb, block.VerbValue);
            }

            foreach (Word word in block.Words)
            {
                builder.Word(word.Key, word.Addr, word.Value);
            }

            builder.End();
        }
    }

    // The facts of the source-side state that a reader rule reads (architecture 7): absolute or incremental, the plane,
    // the feed mode, the active cycle, the preloaded tool.
    private static void Derive(SiemensState state)
    {
        SiemensFacts facts = state.Facts;
        SourceState source = state.Source;
        source.Incremental = facts.Incremental;
        source.Workplane = facts.IsUnknown(SiemensFacts.Plane) ? null : facts.WorkingPlane;
        source.FeedMode = facts.FeedType switch
        {
            "G94" or "G961" or "G971" => FeedMode.PerMin,
            "G95" or "G96" => FeedMode.PerRev,
            _ => null,
        };
        source.ActiveCycle = facts.ModalCycle?.Native;

        // T0 selects the empty place, which the M6 after it changes to; PRELOAD=0 clears the preload (language 4.4).
        source.Preloaded = facts.PreloadedTool is { Number: 0 } ? null : facts.PreloadedTool;
    }

    // The facts of the file being read; a new read begins with a new source-side state, and with it new facts and the
    // templates of its machine.
    private SiemensState Facts()
    {
        if (_siemens is null || !ReferenceEquals(_siemens.Source, State))
        {
            _siemens = new SiemensState(State, _tokenizer.Blocks);
            _templates = new TemplateSet(Machine, new Diagnostics(Machine.Machine.Name));
        }

        return _siemens;
    }
}
