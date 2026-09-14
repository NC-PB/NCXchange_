using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;
using Ncx.Core.Writing;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// Reads Heidenhain Klartext programs of the iTNC 530 and the TNC 640 into canonical NCX (controllers heidenhain.md 7;
/// controller-mapping, the Heidenhain column of every section). The structure pass lays out the file from BEGIN PGM,
/// END PGM, M30 and the labels (HeidenhainLabels); the header of D34 follows PROGRAM=BEGIN, and a subprogram runs with
/// the state of its callers (HeidenhainCalls). Every block carries its verb, and each is read in the file of its
/// concern: the motion (HeidenhainMotion, HeidenhainArcs), the tool call (HeidenhainToolCall), the machining cycles
/// (HeidenhainCycles), the cycles that act where they stand (HeidenhainFrameCycles), the tilted plane
/// (HeidenhainPlane), the labels, calls and jumps (HeidenhainFlow), the Q parameters (HeidenhainQ) and the M functions
/// (HeidenhainFunctions). What NCX cannot express stays RAW with a WARNING (D5).
/// </summary>
public sealed partial class HeidenhainReader : ReaderBase
{
    private readonly HeidenhainTokenizer _tokenizer = new();
    private HeidenhainState? _heidenhain;
    private TemplateSet? _templates;

    /// <summary>
    /// The Heidenhain family, Klartext of the iTNC 530 and the TNC 640 (controllers heidenhain.md).
    /// </summary>
    public override Controller Controller => Controller.Heidenhain;

    /// <summary>
    /// The tokenizer of Klartext programs.
    /// </summary>
    protected override ISourceTokenizer Tokenizer => _tokenizer;

    /// <summary>
    /// The structure of a Klartext block: BEGIN PGM begins the one program of the file, END PGM closes the file, M30
    /// and M2 end the program, LBL n ... LBL 0 called without REP is a subprogram, an LBL that REP or FN 9 to FN 12
    /// uses a label (controllers heidenhain.md 1, 7 rules 3 and 4; controller-mapping 1, 6; language 4.13).
    /// </summary>
    /// <param name="block">A source block with words.</param>
    protected override SourceStructure StructureOf(SourceBlock block)
    {
        HeidenhainLabels labels = Facts().Labels;
        string first = block.Words[0].Address;
        string second = block.Words.Count > 1 ? block.Words[1].Address : "";
        if (first == "BEGIN" && second == "PGM")
        {
            Match begin = ProgramLine().Match(_tokenizer.ContentOf(block));
            return new SourceStructure
            {
                Role = StructureRole.ProgramBegin,
                Name = begin.Success ? begin.Groups[1].Value.Trim() : null,
            };
        }

        // END PGM closes the one program of the file: it frames the file, and the program ends with PROGRAM=END at its
        // M30, or after its last block with a WARNING where the M30 is missing (controller-mapping 1, PROGRAM=END;
        // heidenhain 7 rule 4).
        if (first == "END" && second == "PGM")
        {
            return new SourceStructure { Role = StructureRole.FileFrame };
        }

        string? label = HeidenhainLabels.LabelOf(block);
        bool writable = label is not null && HeidenhainFlow.NameValue(label) is not null;
        if (HeidenhainLabels.IsLabel(block))
        {
            return LabelStructure(block, labels, label, writable);
        }

        var used = new List<string>();
        var calls = new List<string>();
        if (writable && HeidenhainLabels.IsRepeat(block))
        {
            used.Add(label!);
        }
        else if (writable && HeidenhainLabels.IsJump(block) && !labels.JumpsToTheEnd.Contains(block.Line))
        {
            used.Add(label!);
        }
        else if (writable && HeidenhainLabels.IsLabelCall(block) && labels.Subs.Contains(label!))
        {
            calls.Add(label!);
        }

        return new SourceStructure
        {
            Role = HeidenhainLabels.Ends(block) ? StructureRole.ProgramEnd : StructureRole.None,
            LabelsUsed = used,
            Calls = calls,
        };
    }

    /// <summary>
    /// A block leaves an M function undecided that neither the controller nor a table of the machine names (D40, D66).
    /// </summary>
    /// <param name="block">A source block with words.</param>
    protected override bool LeavesUndecided(SourceBlock block)
    {
        Facts();
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "M" && !HeidenhainFunctions.Names(word, _templates!))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A program starts from the initial state of the control with the complete header of D34; a subprogram from what
    /// its callers agree on (virtual machine 3.9).
    /// </summary>
    /// <param name="kind">A program or a subprogram.</param>
    /// <param name="begin">The BEGIN PGM block, or the LBL of the subprogram.</param>
    protected override void BeginSection(SectionKind kind, SourceBlock begin)
    {
        HeidenhainState state = Facts();
        if (kind == SectionKind.Sub)
        {
            if (state.Labels.SubBegins.TryGetValue(begin.Line, out string? name))
            {
                state.Calls.Enter(name, state);
            }

            State.Workplane = state.Plane;
            State.ActiveCycle = state.Definition?.Native;
            return;
        }

        StartProgram(state);

        // Writers emit a complete header; configuration defaults apply to source readers only (D34): the units of BEGIN
        // PGM (controller-mapping 1, UNITS), else units_default, the plane XY until a TOOL CALL names another axis
        // (wave-1 question #73), DIAMETER=ON where the X axis is programmed in diameters (D60).
        string? units = ProgramLine().Match(_tokenizer.ContentOf(begin)) is { Success: true } line
            && line.Groups[2].Success
            ? line.Groups[2].Value.ToUpperInvariant()
            : Machine.Machine.UnitsDefault?.ToUpperInvariant();
        NcxBuilder builder = Builder.Begin(begin.Line)
            .Word("FEED_MODE", null, new IdentValue("PER_MIN"))
            .Word("COMP", null, new IdentValue("OFF"));
        if (units is "MM" or "INCH")
        {
            builder.Word("UNITS", null, new IdentValue(units));
        }

        builder.Word("WORKPLANE", null, new IdentValue("XY"));
        if (Machine.ResolveAxis("X")?.Programming == Programming.Diameter)
        {
            builder.Word("DIAMETER", null, new IdentValue("ON"));
        }

        builder.Word("CYCLE", null, new IdentValue("OFF")).End();
    }

    /// <summary>
    /// Reads a Klartext block concern by concern and writes the NCX blocks it reads into, or keeps it as RAW
    /// (controllers heidenhain.md 7).
    /// </summary>
    /// <param name="block">A source block with words that no reader rule claimed.</param>
    protected override void ReadBlock(SourceBlock block)
    {
        HeidenhainState state = Facts();
        var reading = new HeidenhainBlock(block, _tokenizer.ContentOf(block), State, state, Machine, _templates!,
            Diagnostics);

        // A line before a CHF or RND block ends where the corner begins, which the reader computes from where the line
        // starts, once the whole line is read (D58, HeidenhainCorners).
        HeidenhainPoint? start = state.Position();

        // A reader never throws on bad input (code-guidelines 4): a number beyond the range the reader computes with
        // keeps its block as RAW.
        try
        {
            ReadStructureWords(reading);
            ReadConcern(reading);
            HeidenhainCycles.ReadM99(reading);
            HeidenhainFunctions.Read(reading);
            KeepUnreadAsRaw(reading);
            HeidenhainCorners.Prepare(reading, start);
        }
        catch (OverflowException)
        {
            reading.Draft.KeepAsRaw("a number of the block is beyond the range the reader computes with");
            state.Corner = null;
        }

        Write(reading);
    }

    // The words of the file structure are the structure pass's: BEGIN PGM, M30 and M2 (language 4.13).
    private static void ReadStructureWords(HeidenhainBlock reading)
    {
        if (reading.Keyword(0) == "BEGIN" && reading.Keyword(1) == "PGM")
        {
            reading.MarkAllRead();
            return;
        }

        foreach (SourceWord word in reading.Source.Words)
        {
            if (word.Address == "M" && NativeCode.Of(word) is "M30" or "M2")
            {
                reading.MarkRead(word);
            }
        }
    }

    // Every block carries its verb (heidenhain 7 rule 1): the first words say which concern reads it.
    private static void ReadConcern(HeidenhainBlock reading)
    {
        string first = reading.Keyword(0);
        string second = reading.Keyword(1);
        switch (first)
        {
            case "*":
                // * - title is a structuring block, SECTION (controller-mapping 1).
                reading.MarkAllRead();
                reading.Draft.Main.Add("SECTION", new StringValue(reading.Source.Words[0].Text));
                return;
            case "BEGIN" or "M" or "":
                return;
            case "L":
                HeidenhainMotion.ReadLine(reading);
                return;
            case "LN":
                HeidenhainMotion.ReadVectorLine(reading);
                return;
            case "LP":
                HeidenhainMotion.ReadPolarLine(reading);
                return;
            case "CC":
                HeidenhainArcs.ReadPole(reading);
                return;
            case "C":
                HeidenhainArcs.ReadCenterArc(reading);
                return;
            case "CR":
                HeidenhainArcs.ReadRadiusArc(reading);
                return;
            case "CT":
                HeidenhainArcs.ReadTangentArc(reading);
                return;
            case "CP":
                HeidenhainArcs.ReadPolarArc(reading);
                return;
            case "CHF" or "RND":
                HeidenhainCorners.ReadCorner(reading);
                return;
            case "TOOL" when second is "CALL" or "DEF":
                HeidenhainToolCall.Read(reading);
                return;
            case "CYCL" when second == "DEF":
                ReadCycleDefinition(reading);
                return;
            case "CYCL" when second == "CALL":
                HeidenhainCycles.ReadCall(reading);
                return;
            case "PATTERN" when second == "DEF":
                HeidenhainCycles.ReadPattern(reading);
                return;
            case "PLANE":
                HeidenhainPlane.Read(reading);
                return;
            case "LBL":
                HeidenhainFlow.ReadLabel(reading);
                return;
            case "CALL":
                HeidenhainFlow.ReadCall(reading);
                return;
            case "FN":
                ReadFunction(reading);
                return;
            case "STOP":
                // STOP stops the program, as M0 does (controllers heidenhain.md 1).
                reading.MarkLeading(1);
                reading.Draft.AddState("STOP", null, new IdentValue("PROGRAM"));
                return;
            default:
                ReadOther(reading, first);
                return;
        }
    }

    // A formula assigns a Q parameter (controllers heidenhain.md 6); the blocks NCX keeps as RAW are named by what they
    // are (heidenhain 7 rule 9; controller-mapping 9).
    private static void ReadOther(HeidenhainBlock reading, string first)
    {
        if (HeidenhainQ.IsParameter(first))
        {
            HeidenhainQ.ReadFormula(reading);
            return;
        }

        reading.MarkAllRead();
        reading.Draft.KeepAsRaw(first switch
        {
            "BLK" => "BLK FORM describes the blank for the graphic, header information NCX has no word for "
                + "(controllers heidenhain.md 1, 7 rule 9)",
            "APPR" or "DEP" => "APPR and DEP, the approach and departure blocks, are kept RAW in 1.0 (controllers "
                + "heidenhain.md 2, 7 rule 9)",
            "TCH" => "TCH PROBE, the probing cycles, are kept RAW (controllers heidenhain.md 5, 7 rule 9)",
            "FUNCTION" => "the FUNCTION blocks, TCPM with its options and the turning mode, have no NCX word (D86; "
                + "controllers heidenhain.md 4)",
            "END" => "END PGM is not the last block of the file",
            _ => $"{first} has no NCX word the Heidenhain reader maps (controller-mapping 9)",
        });
    }

    // CYCL DEF n: the cycles 7, 8, 9, 10, 19, 32 and 247 act where they stand, the others are machining cycles
    // (controllers heidenhain.md 2, 3, 5). The definition stays active until the next CYCL DEF (controllers
    // heidenhain.md 5): a machining cycle the reader keeps as RAW, one written over several blocks such as CYCL DEF
    // 12.0 PGM CALL or the older 1.0 TIEFBOHREN, and a CYCL DEF whose cycle number the reader cannot read replace the
    // active definition all the same, and the calls after them stay RAW (heidenhain 7 rule 9).
    private static void ReadCycleDefinition(HeidenhainBlock reading)
    {
        if (reading.Source.Words.Count < 3
            || !HeidenhainFrameCycles.NumberOf(reading.Source.Words[2], out int number, out int part))
        {
            reading.MarkAllRead();
            string written = reading.Source.Words.Count < 3
                ? ""
                : reading.Source.Words[2].Address + reading.Source.Words[2].Text;
            HeidenhainCycles.DefineAsRaw(reading, written, "CYCL DEF names no cycle number");
            return;
        }

        string text = reading.Source.Words[2].Text;
        if (HeidenhainCycles.IsFrameCycle(number))
        {
            HeidenhainFrameCycles.Read(reading, number, part);
        }
        else if (part == 0 && !text.Contains('.') && !text.Contains(','))
        {
            HeidenhainCycles.ReadDefinition(reading, number);
        }
        else
        {
            reading.MarkAllRead();
            HeidenhainCycles.DefineAsRaw(reading, number.ToString(CultureInfo.InvariantCulture),
                $"CYCL DEF {text} is no cycle NCX reads");
        }
    }

    // FN 0 to FN 5 assign, FN 9 to FN 12 jump (controllers heidenhain.md 6); FN 14 error messages, FN 16 F-PRINT, FN 18
    // SYSREAD, FN 19 PLC values, FN 20 WAIT FOR and the table functions are RAW (heidenhain 7 rule 9).
    private static void ReadFunction(HeidenhainBlock reading)
    {
        int function = reading.Source.Words[0].Number is decimal number ? decimal.ToInt32(number) : -1;
        if (function is >= 0 and <= 5)
        {
            HeidenhainQ.ReadFunction(reading, function);
        }
        else if (function is >= 9 and <= 12)
        {
            HeidenhainFlow.ReadJump(reading, function);
        }
        else
        {
            reading.MarkAllRead();
            reading.Draft.KeepAsRaw($"FN {function} has no NCX word (controllers heidenhain.md 6, 7 rule 9)");
        }
    }

    // A word no concern reads is none the reader maps; the block is kept as RAW, nothing is dropped (D5).
    private static void KeepUnreadAsRaw(HeidenhainBlock reading)
    {
        List<SourceWord> unread = reading.Unread();
        if (unread.Count > 0)
        {
            reading.Draft.KeepAsRaw($"{unread[0].Address}{unread[0].Text} has no NCX word the Heidenhain reader maps "
                + "here");
        }
    }

    // The definition stays active until the next CYCL DEF and there is no cancel word, so the reader writes CYCLE=OFF
    // before the next non-cycle motion (controllers heidenhain.md 5; controller-mapping 5, CYCLE=OFF).
    // TODO(question): heidenhain 5 has CYCLE=OFF written "before the next non-cycle motion", which for a definition
    // followed by its positioning blocks and then M99 would switch the cycle off before its first call; the reader
    // writes it before the first non-cycle motion after a call of the cycle, and the definition again where the cycle
    // is called while NCX has it off.
    private void Write(HeidenhainBlock reading)
    {
        HeidenhainState state = reading.Heidenhain;
        if (reading.Draft.RawReason is string reason)
        {
            EmitRaw(reading.Source, reason);

            // The control runs the RAW block and the reader does not know where it leaves the tool.
            state.ForgetPositions();
            return;
        }

        foreach (HeidenhainWarning warning in reading.Draft.Warnings)
        {
            Diagnostics.Warning(reading.Line, warning.Code, warning.Message);
        }

        foreach (HeidenhainDraftBlock draft in reading.Draft.InOrder())
        {
            if (draft.IsEmpty)
            {
                continue;
            }

            bool motion = draft.Verb is "RAPID" or "LINE" or "ARC" or "RETRACT" or "HOME" && !draft.PositionsCall;
            if (motion && (state.CycleOn is null || (state.CycleOn == true && state.CycleCalled)))
            {
                BeginBlock(reading.Source).Word("CYCLE", null, new IdentValue("OFF")).End();
                state.CycleOn = false;
                state.CycleCalled = false;
            }

            NcxBuilder builder = BeginBlock(reading.Source);
            if (draft.Verb is not null)
            {
                builder.Verb(draft.Verb, draft.VerbValue);
            }

            foreach (Word word in draft.Words)
            {
                builder.Word(word.Key, word.Addr, word.Value);
            }

            builder.End();
        }
    }

    // A program starts from the initial state of the control, which its header writes (D34; virtual machine 2): the
    // plane XY, no pole, no cycle, an empty chain, TCPM off, nothing preloaded.
    private void StartProgram(HeidenhainState state)
    {
        state.ForgetFrame();
        state.Plane = Workplane.XY;
        state.Definition = null;
        state.DefinitionUnknown = false;
        state.CycleOn = false;
        state.CycleCalled = false;
        state.Chain.Clear();
        state.DatumShift.Clear();
        state.Tcpm = false;
        state.Pattern = null;
        State.Workplane = Workplane.XY;
        State.Preloaded = null;
        State.ActiveCycle = null;
    }

    // The facts of the file being read; a new read begins with a new source-side state, and with it new facts and the
    // templates of its machine.
    private HeidenhainState Facts()
    {
        if (_heidenhain is null || !ReferenceEquals(_heidenhain.Source, State))
        {
            _heidenhain = new HeidenhainState(State, _tokenizer.Blocks);
            _templates = new TemplateSet(Machine, new Diagnostics(Machine.Machine.Name));
        }

        return _heidenhain;
    }

    // The structure of an LBL block: the SUB=BEGIN of a subprogram, the SUB=END of its LBL 0, or the LABEL of a label a
    // REP or an FN jump uses (heidenhain 7 rule 3).
    private static SourceStructure LabelStructure(SourceBlock block, HeidenhainLabels labels, string? label,
        bool writable)
    {
        if (labels.SubBegins.TryGetValue(block.Line, out string? name))
        {
            return new SourceStructure { Role = StructureRole.SubBegin, Name = name };
        }

        if (labels.SubEnds.Contains(block.Line))
        {
            return new SourceStructure { Role = StructureRole.Return };
        }

        return new SourceStructure
        {
            Label = writable && label != "0" && labels.Targets.Contains(label!) ? label : null,
        };
    }

    // BEGIN PGM 2.5D FRAESEN MM: the name of the program and its units (controllers heidenhain.md 1).
    [GeneratedRegex(@"^(?:BEGIN|END)\s+PGM\s+(.+?)(?:\s+(MM|INCH))?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ProgramLine();
}
