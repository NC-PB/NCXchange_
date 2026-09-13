using System.CommandLine;
using Ncx.Cli.History;
using Ncx.Core.Model;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx annotate &lt;file&gt; and the options of check: a copy of the program through the canonical writer on the
/// standard output, with the previous values appended to each block's comment, which the parser keeps as the block's
/// comment and the virtual machine ignores (virtual machine 6, D92); the diagnostics on the standard error (D98) and
/// the exit codes of D97, as ncx check has them (architecture 10).
/// </summary>
internal static class AnnotateCommand
{
    /// <summary>
    /// The annotate command of the ncx root command (architecture 10).
    /// </summary>
    /// <param name="output">The standard output, for the annotated copy.</param>
    /// <param name="error">The standard error, for the diagnostics.</param>
    public static Command Create(TextWriter output, TextWriter error)
    {
        var command = new Command(
            "annotate",
            "Write a copy of an NCX file with the previous value of every state variable a block changes as the "
            + "block's comment.");
        RunOptions options = RunOptions.AddTo(command);
        command.SetAction(parseResult => Run(options.Read(parseResult), output, error));
        return command;
    }

    /// <summary>
    /// Annotates one file.
    /// </summary>
    /// <param name="settings">The file, the machine and the shared options.</param>
    /// <param name="output">The standard output.</param>
    /// <param name="error">The standard error.</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(RunSettings settings, TextWriter output, TextWriter error)
    {
        var annotations = new AnnotationListener();
        PipelineRun run = Pipeline.Run(settings, [annotations]);

        // The copy carries the values of every block the run executed, and the byte order mark of the file where it
        // was, as ncx format keeps it (virtual machine 6; wave-1 question #80); there is none when there was no
        // program to run.
        // TODO(question): virtual machine 6 does not say what annotate writes when an ERROR stops the run; it writes
        // the copy with the values of the blocks executed before the ERROR, and nothing when the file, the machine
        // file, the parser or the expander stopped the run before its first block, until that is answered.
        if (run.Program is NcxProgram program)
        {
            output.Write(run.ByteOrderMark + AnnotatedText.Write(program, annotations));
        }

        error.Write(run.Diagnostics.ToText());
        return run.ExitCode(settings.Strict);
    }
}
