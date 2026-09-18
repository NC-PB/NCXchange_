using Ncx.Config.Templates;
using Ncx.Core.Expander;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

/// <summary>
/// The skeleton every compiler shares (architecture 8; code-guidelines 5, Template Method): expand the program, run the
/// virtual machine STATIC with the compiler subscribed, write every block from its Before and After with the templates
/// and the number format of the machine, each SUB section once however often it is called (D99), and lay the programs
/// out per program_layout (D48). A compiler of a controller family says how its controller writes a block: WriteBlock,
/// and where it needs them WriteHeader and WriteFooter.
/// </summary>
public abstract partial class CompilerBase : ICompiler
{
    // The run of one Compile.
    private NcxProgram? _program;
    private MachineConfig? _machine;
    private CompileOptions? _options;
    private Diagnostics? _diagnostics;
    private NumberFormatter? _numbers;
    private TemplateSet? _templates;
    private LookAhead? _lookAhead;
    private IReadOnlyList<BlockStep> _steps = [];

    // The step being written, and the lines of its block while WriteBlock, WriteHeader or WriteFooter writes them.
    private BlockStep? _step;
    private List<string>? _lines;

    /// <summary>
    /// The controller family whose programs this compiler writes (machine-config 1).
    /// </summary>
    public abstract Controller Controller { get; }

    /// <summary>
    /// The extension of an output file of the controller, ".nc", ".h" (machine-config 10).
    /// </summary>
    protected abstract string FileExtension { get; }

    /// <summary>
    /// What stands before a block number under block_numbers: "N" on Fanuc, nothing on Heidenhain, whose blocks are
    /// numbered from 0 (machine-config 2, controllers heidenhain.md 8 rule 1).
    /// </summary>
    protected virtual string BlockNumberPrefix => "N";

    /// <summary>
    /// True for a controller that writes the decimal point on every real number, X70. on Fanuc (controllers fanuc.md
    /// 2, 10 rule 5); false by default.
    /// </summary>
    protected virtual bool DecimalPointOnWholeNumbers => false;

    /// <summary>
    /// The machine of the compile.
    /// </summary>
    protected MachineConfig Machine => _machine ?? throw NotCompiling();

    /// <summary>
    /// The plugins and the tool table of the compile.
    /// </summary>
    protected CompileOptions Options => _options ?? throw NotCompiling();

    /// <summary>
    /// The diagnostics of the compile, where every stage reports (D98).
    /// </summary>
    protected Diagnostics Diagnostics => _diagnostics ?? throw NotCompiling();

    /// <summary>
    /// The numbers as [format] of the machine writes them (machine-config 2).
    /// </summary>
    protected NumberFormatter Numbers => _numbers ?? throw NotCompiling();

    /// <summary>
    /// The templates of the machine, each parsed once (architecture 6).
    /// </summary>
    protected TemplateSet Templates => _templates ?? throw NotCompiling();

    /// <summary>
    /// What comes after the block being written, from the STATIC pass (architecture 8, D52).
    /// </summary>
    protected LookAhead LookAhead => _lookAhead ?? throw NotCompiling();

    /// <summary>
    /// The block being written with its state and events.
    /// </summary>
    protected BlockStep Step => _step ?? throw new InvalidOperationException("No block is being written.");

    /// <summary>
    /// What the target control has active in the program or walk being written, so that a modal word is written only
    /// on change; unknown at the start of every program and of every walk of a subprogram (D99).
    /// </summary>
    protected TargetState Target => CurrentContext?.Target ?? _fileTarget;

    /// <summary>
    /// Compiles one NCX file for one machine (architecture 8): expand, check what the machine takes, run STATIC with
    /// the compiler subscribed, write every block, lay the files out. Never throws on bad input (code-guidelines 6).
    /// </summary>
    /// <param name="program">The parsed NCX file, with the diagnostics of the parser.</param>
    /// <param name="machine">The machine, with its cycle catalog.</param>
    /// <param name="options">The plugins and the tool table.</param>
    public CompileResult Compile(NcxProgram program, MachineConfig machine, CompileOptions options)
    {
        // 1. Expand: the expansion rules of the machine and the rewriters of the plugins become generated NCX blocks,
        // which the compiler writes like the rest (language 4.15; virtual machine 1; architecture 8). A channel program
        // of a job comes expanded from the job compiler, which binds the words the expansion generates as well as those
        // of the file (virtual machine 3.8 rule 2a, D56), and is not expanded twice. An ERROR of the parser or the
        // expander stops the run before its first block (virtual machine 2.9).
        NcxProgram expanded = options.Job is null ? Expander.Expand(program, machine, options.Rewriters) : program;
        Diagnostics diagnostics = expanded.Diagnostics;
        if (diagnostics.HasErrors)
        {
            return Stopped(diagnostics);
        }

        // 2. RAW of another controller or builder and a CYCLE:controller=n block of another family are ERRORs at
        // compile time, every one reported before anything is written (language 4.1, 4.7.1; controller-mapping 9;
        // D5, D94).
        CheckNativeText(expanded, machine, diagnostics);

        // A word of a table that the machine accepts only from another channel, or needs in every channel program, is
        // an ERROR of a single-channel compile; in a channel program of a job the job compiler has moved or duplicated
        // every such word, or reported why it could not (virtual machine 3.8 rule 2a, D56).
        if (options.Job is null)
        {
            ChannelBinding.Check(expanded, machine, diagnostics);
        }

        if (diagnostics.HasErrors)
        {
            return Stopped(diagnostics);
        }

        // 3. The STATIC run with the compiler subscribed: every block with its Before and After, recorded before any
        // line is written, which is the look-ahead of {next}, {b}, {c} and auto_preload (virtual machine 1, D91;
        // architecture 8; D52). The listeners of the options, the plugins' ones, read every event of the run after the
        // compiler, as a listener reads every event of any run (virtual machine 7; architecture 9; code-guidelines 5,
        // Observer). The run of a channel program of a job knows the channels of the job (virtual machine 3.7).
        var recorder = new StepRecorder();
        VmOptions vmOptions = VmOptions.ForMachine(machine) with
        {
            RaiseBlockWrite = true,
            JobChannels = options.Job?.Channels.Count,
        };
        var vm = new VirtualMachine(machine, vmOptions, diagnostics);
        vm.Subscribe(recorder);
        foreach (IVmListener listener in options.Listeners)
        {
            vm.Subscribe(listener);
        }

        if (vm.Run(expanded).Stopped || diagnostics.HasErrors)
        {
            return Stopped(diagnostics);
        }

        // 4. Every block written in walk order, each SUB once (D99), then the files of program_layout (D48). An ERROR
        // stops the run, and a stopped run writes no file (virtual machine 2.9, code-guidelines 6).
        Begin(expanded, machine, options, diagnostics, recorder.Steps);
        WriteSteps();
        IReadOnlyList<CompiledFile> files = Files();
        if (diagnostics.HasErrors)
        {
            return Stopped(diagnostics);
        }

        return new CompileResult { Files = files, Diagnostics = diagnostics };
    }

    /// <summary>
    /// Writes one block as the controller writes it, with <see cref="Line"/>: from the block, its state before and
    /// after (architecture 8), the look-ahead and the target state. Called for every step of the walk, FILE=BEGIN and
    /// FILE=END, PROGRAM=BEGIN and END, SUB=BEGIN and END among them; a block of a subprogram once per walk, of which
    /// the first gives the text (D99).
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="before">The state of the channel before it.</param>
    /// <param name="after">The state of the channel after it.</param>
    protected abstract void WriteBlock(Block block, ChannelSnapshot before, ChannelSnapshot after);

    /// <summary>
    /// Writes what stands in front of a program's PROGRAM=BEGIN lines, BEGIN PGM on Heidenhain (heidenhain.md 8 rule
    /// 1); nothing by default.
    /// </summary>
    /// <param name="program">The program section.</param>
    /// <param name="state">The state after its first block with a verb, where its complete header stands (D34).</param>
    protected virtual void WriteHeader(Section program, ChannelSnapshot state)
    {
    }

    /// <summary>
    /// Writes what follows a program and the subprograms placed after it, END PGM on Heidenhain (heidenhain.md 8 rule
    /// 1); nothing by default.
    /// </summary>
    /// <param name="program">The program section.</param>
    /// <param name="state">The state after its PROGRAM=END.</param>
    protected virtual void WriteFooter(Section program, ChannelSnapshot state)
    {
    }

    /// <summary>
    /// A comment line of the controller around a text already in the machine's charset, "( TEXT )" on Fanuc, "; TEXT"
    /// on Heidenhain (controller-mapping 1); the warning block of D10 is written with it.
    /// </summary>
    /// <param name="text">The text of the comment.</param>
    protected abstract string CommentLine(string text);

    /// <summary>
    /// Whether a line gets a block number under block_numbers: every line by default; not the % and O lines of Fanuc,
    /// not the continuation lines of a Klartext cycle (machine-config 2).
    /// </summary>
    /// <param name="line">The line without block number.</param>
    protected virtual bool TakesBlockNumber(string line)
    {
        return true;
    }

    /// <summary>
    /// The block number a line carries itself, which block_numbers gives no other line of the file: the label N20 of
    /// a Fanuc program; null for a line without one, the default (machine-config 2).
    /// </summary>
    /// <param name="line">The line without block number.</param>
    protected virtual int? OwnBlockNumber(string line)
    {
        return null;
    }

    /// <summary>
    /// Writes one line of the block being written, or several where the text holds line breaks, since a template may
    /// span lines (machine-config 3).
    /// </summary>
    /// <param name="text">The line in the syntax of the controller, without block number.</param>
    protected void Line(string text)
    {
        List<string> lines = _lines ?? throw new InvalidOperationException("Lines are written in WriteBlock only.");
        lines.AddRange(text.Split('\n'));
    }

    /// <summary>
    /// A comment text in the charset of the machine, the umlauts transliterated before writing (machine-config 2).
    /// </summary>
    /// <param name="text">The text as the program writes it.</param>
    protected string CommentText(string text)
    {
        return CommentCharset.Transliterate(text, Machine.Format?.CommentCharset, Step.Block, Diagnostics);
    }

    // The run of one Compile: the program as expanded, the machine, the options, the recorded steps, and what they
    // need to be written. The templates were checked on the lines of the machine file when it loaded (P2-02); a
    // machine whose file had a template that cannot be parsed does not load, so this set reports nothing.
    private void Begin(NcxProgram program, MachineConfig machine, CompileOptions options, Diagnostics diagnostics,
        IReadOnlyList<BlockStep> steps)
    {
        _program = program;
        _machine = machine;
        _options = options;
        _diagnostics = diagnostics;
        _steps = steps;
        _numbers = new NumberFormatter(machine, diagnostics, DecimalPointOnWholeNumbers);
        _templates = new TemplateSet(machine, new Diagnostics(machine.Machine.Name));
        _lookAhead = new LookAhead(steps, machine);
        ResetWrite();
    }

    private static CompileResult Stopped(Diagnostics diagnostics)
    {
        return new CompileResult { Files = [], Diagnostics = diagnostics };
    }

    private static InvalidOperationException NotCompiling()
    {
        return new InvalidOperationException("A compiler reads its run only while Compile runs.");
    }
}
