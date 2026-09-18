using System.Text.RegularExpressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Writing;
using Ncx.Readers;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx convert --batch &lt;folder&gt; --machine &lt;toml&gt; --report &lt;file&gt; [--strict]: converts every file of a
/// folder as ncx convert converts one program, the reader of the machine's controller, the canonical writer and the
/// STATIC check, never stops on an error, and writes a summary into the file of --report: per file its blocks, its RAW
/// blocks per word and its diagnostics per code, and the totals (implementation 13, P3-07; architecture 10, 11;
/// controllers sample-corpus 3). The diagnostics of every file go to the standard error as ncx convert reports them
/// (D98). Exit code 2 when the folder or the machine file cannot be read or no machine file is named, decided before
/// the first file; otherwise 1 when a file has an ERROR or crashed, or a WARNING under --strict, and 0 without (D97).
/// </summary>
internal static class BatchCommand
{
    // A diagnostic about a whole file stands on its first line, as the loaders of Ncx.Config report one.
    private const int FileLine = 1;

    /// <summary>
    /// Converts every file of the folder and writes the report.
    /// </summary>
    /// <param name="settings">The folder as the command line names it in File, the machine, the folders and --strict.
    /// </param>
    /// <param name="reportFile">The file of --report.</param>
    /// <param name="readers">The readers by controller family, registered in Program.cs.</param>
    /// <param name="error">The standard error.</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(RunSettings settings, string reportFile, ReaderRegistry readers, TextWriter error)
    {
        string folder = settings.File;
        var diagnostics = new Diagnostics(folder);

        // Code 2 is decided before the run starts and takes precedence: the folder, ncx.toml, the machine file and its
        // cycle catalog are read first, and one that cannot be found or read stops everything; the batch, like
        // convert, never runs against the built-in default machine (D77, D97, D103; architecture 10). All of them are
        // read, so that all are reported.
        List<string>? files = FilesOf(folder, diagnostics);
        RunMachine runMachine = RunMachine.Select(settings, diagnostics);
        if (runMachine.IsDefault)
        {
            ConvertCommand.ReportMachineRequired(diagnostics);
        }

        if (files is null || !runMachine.InputsRead || runMachine.IsDefault)
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.NotStarted;
        }

        // ncx.toml, a machine file or a cycle catalog that reads but loads with an ERROR stops the run before it
        // starts, exit code 1, and so does a controller that ncx has no reader for (D97; architecture 7).
        if (runMachine.Machine is not MachineConfig machine
            || ConvertCommand.ReaderOf(machine, readers, diagnostics) is null)
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.Error;
        }

        // Every file is converted as ncx convert converts one program, and the batch never stops on an error: an
        // ERROR of a file is reported on the file and counted, a crash as well (implementation 13, P3-07).
        // TODO(question): D209, as recommended: the batch writes nothing but its report where the command line names
        // it, and no NCX file, until D209 is answered.
        // TODO(question): implementation 13 converts "every file of a folder" with one machine and does not say whether
        // the files of its folders count, whether a file whose name is no program of the machine's controller does, nor
        // whether the plugins of the working directory read along. Every file of the folder and of its folders counts,
        // whatever its name, read by the reader of the machine's controller, and the plugins are not loaded, since a
        // plugin reports on the file it was loaded with; until that is answered.
        error.Write(diagnostics.ToText());

        // A run that has started ends with 1 when the machine, a file or the report had an ERROR, a crash among them,
        // or a WARNING under --strict, and with 0 otherwise (D97).
        int exitCode = ExitCodes.OfRun(diagnostics, settings.Strict);
        var converted = new List<BatchFile>();
        foreach (string name in files)
        {
            var fileDiagnostics = new Diagnostics(Path.Join(folder, name));
            converted.Add(ConvertFile(name, machine, readers, fileDiagnostics));
            error.Write(fileDiagnostics.ToText());
            exitCode = Math.Max(exitCode, ExitCodes.OfRun(fileDiagnostics, settings.Strict));
        }

        // The report goes into the file of --report (implementation 13, P3-07); a file that cannot be written is an
        // ERROR of the run, which has started (D97, architecture 10).
        var reportDiagnostics = new Diagnostics(reportFile);
        OutputFile.Write(reportFile, BatchReport.Write(MachineOf(machine, runMachine), converted), reportDiagnostics);
        error.Write(reportDiagnostics.ToText());
        return Math.Max(exitCode, ExitCodes.OfRun(reportDiagnostics, settings.Strict));
    }

    // One file: read, converted and checked as ncx convert does it, the canonical text written in memory only. A crash
    // is reported on the file with the name of its exception; the report never holds its message, which may quote the
    // program (implementation 13, risks).
    private static BatchFile ConvertFile(string name, MachineConfig machine, ReaderRegistry readers,
        Diagnostics diagnostics)
    {
        string? text = ControllerProgram.Read(diagnostics.File, diagnostics);
        if (text is null)
        {
            return BatchFile.Of(name, BatchOutcome.Unreadable, null, null, diagnostics);
        }

        try
        {
            IReader reader = readers.Create(machine.Machine.Controller!.Value)!;
            NcxProgram program = ConvertCommand.Convert(new SourceFile(diagnostics.File, text), machine, reader, null,
                diagnostics);
            NcxWriter.Write(program);
            return BatchFile.Of(name, BatchOutcome.Converted, null, program, diagnostics);
        }
        catch (Exception exception) when (IsCrash(exception))
        {
            // Controllers sample-corpus 3: convert must never crash on a program, and a crash is a bug.
            diagnostics.Error(FileLine, DiagnosticCodes.BatchFileCrashed,
                $"The conversion of the program stopped with {exception.GetType().Name}: "
                + $"{exception.Message.TrimEnd('.')}; a crash is a bug of ncx, and the batch goes on with the next "
                + "file (controllers sample-corpus 3; implementation 13, P3-07).");
            return BatchFile.Of(name, BatchOutcome.Crashed, exception.GetType().Name, null, diagnostics);
        }
    }

    // Every file of the folder and of its folders, hidden files included, by its path from the folder with forward
    // slashes, in ordinal order, so that two reports of one folder list the files alike. A folder that cannot be read
    // or listed is an input that cannot be read, exit code 2 (D97).
    private static List<string>? FilesOf(string folder, Diagnostics diagnostics)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = 0,
        };
        try
        {
            var names = new List<string>();
            foreach (string path in Directory.EnumerateFiles(folder, "*", options))
            {
                names.Add(Path.GetRelativePath(folder, path).Replace('\\', '/'));
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            diagnostics.Error(FileLine, DiagnosticCodes.BatchFolderUnreadable,
                $"The folder of --batch cannot be read: {exception.Message.TrimEnd('.')} (D97; implementation 13, "
                + "P3-07).");
            return null;
        }
    }

    // What a defect of a reader, the writer, the expander or the virtual machine throws; an exception that says the
    // process itself cannot go on, out of memory, is none of them (code-guidelines 6).
    private static bool IsCrash(Exception exception)
    {
        return exception is InvalidOperationException or ArgumentException or FormatException
            or IndexOutOfRangeException or ArithmeticException or NullReferenceException or KeyNotFoundException
            or InvalidCastException or NotSupportedException or NotImplementedException
            or InsufficientExecutionStackException or RegexMatchTimeoutException;
    }

    // The machine as the head of the report names it: its file by name, its name and its controller.
    private static string MachineOf(MachineConfig machine, RunMachine runMachine)
    {
        string file = runMachine.MachineFile is FoundFile found ? Path.GetFileName(found.FullPath) : "";
        return $"{file} ({machine.Machine.Name}, {machine.Machine.Controller})";
    }
}
