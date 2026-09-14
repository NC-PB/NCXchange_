using Ncx.Compilers;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// A compiler of one controller family for the tests of ncx compile: it records what the command hands it and gives
/// back one file, T.nc, or an ERROR and no file (architecture 8).
/// </summary>
internal sealed class RecordingCompiler(Controller controller, bool withError = false) : ICompiler
{
    /// <summary>
    /// The text of the one file it gives.
    /// </summary>
    public const string Text = "%\nO1\nM30\n%\n";

    public Controller Controller => controller;

    /// <summary>
    /// The options of the last compile.
    /// </summary>
    public CompileOptions? Options { get; private set; }

    /// <summary>
    /// The machine of the last compile.
    /// </summary>
    public MachineConfig? Machine { get; private set; }

    public CompileResult Compile(NcxProgram program, MachineConfig machine, CompileOptions options)
    {
        Options = options;
        Machine = machine;
        if (withError)
        {
            program.Diagnostics.Error(1, "CMP010", "The machine has no template for the test.");
            return new CompileResult { Files = [], Diagnostics = program.Diagnostics };
        }

        return new CompileResult
        {
            Files = [new CompiledFile { Name = "T.nc", Text = Text }],
            Diagnostics = program.Diagnostics,
        };
    }
}
