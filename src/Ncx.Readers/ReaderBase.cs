using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Writing;

namespace Ncx.Readers;

/// <summary>
/// The skeleton every reader shares (architecture 7; code-guidelines 5, Template Method): tokenize the file, lay out
/// its structure, and for every source block apply the source-side state, try the configuration tables and then the
/// reader rules, and read the block with the mapping of the controller family into the builder; diagnostics carry RDR
/// codes. A reader of a controller family fills in the tokenizer, the structure of a block and the reading of a block.
/// </summary>
public abstract class ReaderBase : IReader
{
    // The word of the optional block skip (language 4.1) and of a structuring comment (controller-mapping 1).
    private const string SkipKey = "SKIP";
    private const string SectionKey = "SECTION";

    private static readonly Dictionary<string, int> s_noModalGroups = [];

    private MachineConfig? _machine;
    private Diagnostics? _diagnostics;
    private NcxBuilder? _builder;
    private SourceState? _state;
    private TemplateSet? _templates;
    private IReadOnlyList<ISourceRule> _rules = [];

    // The comment of the source block being read, until an NCX block takes it (controller-mapping 1).
    private string? _pendingComment;

    /// <summary>
    /// The controller family whose files this reader reads (machine-config 1).
    /// </summary>
    public abstract Controller Controller { get; }

    /// <summary>
    /// The tokenizer of the controller's syntax (architecture 7).
    /// </summary>
    protected abstract ISourceTokenizer Tokenizer { get; }

    /// <summary>
    /// The modal group of every modal code of the controller family, by the code without leading zeros, "G1" is
    /// group 1 on Fanuc (controllers fanuc.md 3); empty for a family without modal groups.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, int> ModalGroupOfCode => s_noModalGroups;

    /// <summary>
    /// The machine of the file being read, while Read reads it.
    /// </summary>
    protected MachineConfig Machine => _machine ?? throw NotReading();

    /// <summary>
    /// The diagnostics of the file being read, while Read reads it (D98).
    /// </summary>
    protected Diagnostics Diagnostics => _diagnostics ?? throw NotReading();

    /// <summary>
    /// The builder of the program being read, while Read reads it (architecture 7).
    /// </summary>
    protected NcxBuilder Builder => _builder ?? throw NotReading();

    /// <summary>
    /// The source-side state of the file being read, while Read reads it (architecture 7).
    /// </summary>
    protected SourceState State => _state ?? throw NotReading();

    /// <summary>
    /// Reads one file (architecture 7): never throws on bad input, reports through the diagnostics of the program,
    /// produces a program that check can run and keeps what it cannot express as RAW (code-guidelines 4, D5).
    /// </summary>
    /// <param name="source">The file, its name and its text.</param>
    /// <param name="machine">The machine the file was written for.</param>
    /// <param name="options">The reader rules of the plugins.</param>
    public NcxProgram Read(SourceFile source, MachineConfig machine, ReadOptions options)
    {
        Start(source, machine, options);

        // The tokenizer cuts the file into blocks, the reader of the family says what each block is for the file
        // structure, and the structure pass lays out the order in which the blocks are read and the blocks of the
        // structure are written (architecture 7, language 4.13).
        List<SourceBlock> blocks = [.. Tokenizer.Tokenize(source.Text)];
        var structures = new List<SourceStructure>(blocks.Count);
        foreach (SourceBlock block in blocks)
        {
            structures.Add(block.IsTrivia ? SourceStructure.None : StructureOf(block));
        }

        SourceBlock? current = null;
        foreach (ReadStep step in StructurePass.Plan(blocks, structures, Diagnostics))
        {
            if (!ReferenceEquals(step.Source, current))
            {
                KeepPendingComment();
                current = step.Source;
                _pendingComment = string.IsNullOrEmpty(current.Comment) ? null : current.Comment;
            }

            TakeStep(step);
        }

        KeepPendingComment();
        return Builder.Build();
    }

    /// <summary>
    /// Says what a source block is for the structure of the file: the file frame, the begin of a program or
    /// subprogram, the program end, the return, its label and the labels it jumps to (language 4.13). The structure
    /// pass writes the words of the structure from it; ReadBlock writes the other words of the block.
    /// </summary>
    /// <param name="block">A source block with words.</param>
    protected abstract SourceStructure StructureOf(SourceBlock block);

    /// <summary>
    /// Tells whether the controller's own words and the configuration tables of the machine leave a word of the block
    /// undecided, such as an M code that no table names. Only such a block is offered to the reader rules, because the
    /// tables are tried first (D40, D66, machine-config 5).
    /// </summary>
    /// <param name="block">A source block with words.</param>
    protected abstract bool LeavesUndecided(SourceBlock block);

    /// <summary>
    /// Reads a source block into NCX blocks, each begun with BeginBlock(block), with the mapping of the controller
    /// family; the words of the file structure are the structure pass's (architecture 7).
    /// </summary>
    /// <param name="block">A source block with words that no reader rule claimed.</param>
    protected abstract void ReadBlock(SourceBlock block);

    /// <summary>
    /// Begins an NCX block read from a source block: with SKIP or SKIP=n when the source block carries the block skip
    /// (language 4.1), and with the comment of the source block on the first NCX block read from it (controller-mapping
    /// 1). Add the words and End() it.
    /// </summary>
    /// <param name="block">The source block being read.</param>
    /// <returns>The builder with the block begun.</returns>
    protected NcxBuilder BeginBlock(SourceBlock block)
    {
        Builder.Begin(block.Line);
        AddSkip(block);
        TakeComment();
        return Builder;
    }

    /// <summary>
    /// Keeps a source block that NCX cannot express as RAW:controller, or RAW:builder when it holds a code of the
    /// machine's [raw] table, with its text verbatim and a WARNING (D5, controller-mapping 9).
    /// </summary>
    /// <param name="block">The source block being read.</param>
    /// <param name="reason">Why the block cannot be read, in the words of the machine.</param>
    protected void EmitRaw(SourceBlock block, string reason)
    {
        // The RAW text holds the comment of the block, verbatim (language 4.1).
        _pendingComment = null;
        RawEmitter.Emit(Builder, block, RawEmitter.DialectOf(block, Machine, Controller), reason, Diagnostics);
    }

    /// <summary>
    /// Writes a structuring comment as SECTION="title", Heidenhain * - title (controller-mapping 1).
    /// </summary>
    /// <param name="block">The source block being read.</param>
    /// <param name="title">The text of the structuring comment.</param>
    protected void EmitSection(SourceBlock block, string title)
    {
        BeginBlock(block).Word(SectionKey, null, new StringValue(title)).End();
    }

    /// <summary>
    /// Maps a native code to the state of the function table of the machine that writes it, codes compared by number:
    /// "M08" is "COOLANT:STANDARD=ON" where the table writes M8 (machine-config 5, D105).
    /// </summary>
    /// <param name="nativeCode">The native text of the function, "M88", "M03 P11".</param>
    /// <returns>The state, the word of its table with the state as its value; null when no table names the code.
    /// </returns>
    protected string? FindFunction(string nativeCode)
    {
        // The state names the word of its table, COOLANT:STANDARD=ON; the word of the language, COOLANT=ON for the
        // default channel, is the family's to write (wave-1 question #62), and of a code several states write the
        // first is named (wave-1 question #63).
        return (_templates ?? throw NotReading()).FindFunctionByCode(nativeCode);
    }

    /// <summary>
    /// Tells whether a word is a builder code of the machine's [raw] table, compared by number (machine-config 5,
    /// D105).
    /// </summary>
    /// <param name="word">The source word.</param>
    protected bool IsBuilderCode(SourceWord word)
    {
        return RawEmitter.IsBuilderCode(word, Machine.Raw);
    }

    private void Start(SourceFile source, MachineConfig machine, ReadOptions options)
    {
        _machine = machine;
        _diagnostics = new Diagnostics(source.Name);
        _builder = new NcxBuilder(_diagnostics);
        _state = new SourceState(machine.Machine.GcodeSystem, ModalGroupOfCode);
        _rules = options.Rules;
        _pendingComment = null;

        // The templates of the machine, which the tables are matched through (architecture 6). A template the machine
        // file cannot use is an ERROR of the machine file; the machine record keeps neither its file nor the lines of
        // its templates (wave-1 question #61), so the machine's name stands for the file.
        var machineDiagnostics = new Diagnostics(machine.Machine.Name);
        _templates = new TemplateSet(machine, machineDiagnostics);
        foreach (Diagnostic diagnostic in machineDiagnostics.Items)
        {
            _diagnostics.Add(diagnostic);
        }
    }

    private void TakeStep(ReadStep step)
    {
        switch (step.Kind)
        {
            case ReadStepKind.Trivia:
                // A blank or comment-only source line is trivia, kept in its place (D92; controller-mapping 1).
                Builder.Trivia(_pendingComment is null ? "" : "; " + _pendingComment);
                _pendingComment = null;
                break;
            case ReadStepKind.Read:
                ReadSourceBlock(step.Source);
                break;
            default:
                WriteStructure(step);
                break;
        }
    }

    private void ReadSourceBlock(SourceBlock block)
    {
        // The modal codes of a block act for the block itself, so it is read with them (controllers fanuc.md 1, 3).
        State.Apply(block);

        // The configuration tables are tried first; a reader rule decides what they leave undecided, and a block no
        // rule claims is read by the mapping of the controller family (D40, D66, machine-config 5).
        // TODO(question): architecture 9 and virtual machine 7 give folding M5, M51, M3 S1500 back into
        // COOLANT:THROUGH=ON as the work of a reader rule, while the tables name each of the three codes and are
        // tried first (machine-config 5, D40); a rule is offered only the blocks the tables leave undecided, so it
        // cannot fold a sequence of codes the tables know.
        if (LeavesUndecided(block) && ClaimedByRule(block))
        {
            return;
        }

        ReadBlock(block);
    }

    // The first rule that claims the block reads it, and its claim covers that one block (D40, D66).
    // TODO(question): how a rule claims a sequence of source blocks (D66, architecture 9) through the one method
    // Read(SourceBlock, SourceState, NcxBuilder): a block the tables decide is not offered to the rules and nothing is
    // called after the last block, so a rule holding blocks back would see them reordered or lost (ISourceRule).
    private bool ClaimedByRule(SourceBlock block)
    {
        foreach (ISourceRule rule in _rules)
        {
            if (rule.Read(block, State, Builder))
            {
                return true;
            }
        }

        return false;
    }

    private void WriteStructure(ReadStep step)
    {
        Builder.Begin(step.Line);
        if (step.CarriesSkip)
        {
            AddSkip(step.Source);
        }

        foreach (Word word in step.Words)
        {
            Builder.Word(word.Key, word.Addr, word.Value);
        }

        if (step.UsesComment)
        {
            _pendingComment = null;
        }
        else if (step.TakesComment)
        {
            TakeComment();
        }

        Builder.End();
    }

    // The optional block skip, / or /n in front of the block, is SKIP or SKIP=n (language 4.1, controller-mapping 1).
    private void AddSkip(SourceBlock block)
    {
        if (!block.BlockSkip)
        {
            return;
        }

        Value skipSwitch = block.SkipSwitch is int number
            ? new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture))
            : NoValue.Instance;
        Builder.Word(SkipKey, null, skipSwitch);
    }

    // A source comment on a block becomes the comment of the first NCX block read from it (controller-mapping 1).
    private void TakeComment()
    {
        if (_pendingComment is not null)
        {
            Builder.Comment("; " + _pendingComment);
            _pendingComment = null;
        }
    }

    // Comments are kept (D5): the comment of a source block that no NCX block took, because the block wrote none
    // (a Fanuc G90 alone) or a reader rule claimed it, stays as a comment-only line after its blocks (D92).
    private void KeepPendingComment()
    {
        if (_pendingComment is not null)
        {
            Builder.Trivia("; " + _pendingComment);
            _pendingComment = null;
        }
    }

    private static InvalidOperationException NotReading()
    {
        return new InvalidOperationException("A reader has a file, a machine and a builder only while Read reads.");
    }
}
