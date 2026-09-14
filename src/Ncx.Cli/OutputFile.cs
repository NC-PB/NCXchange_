using System.Text;
using Ncx.Core.Model;

namespace Ncx.Cli;

/// <summary>
/// The file of --output, into which ncx format and ncx convert write their canonical text instead of the standard
/// output: UTF-8 without a byte order mark, and a file that cannot be written reported as a diagnostic with its name
/// (language 3, Encoding; architecture 10; code-guidelines 6).
/// </summary>
internal static class OutputFile
{
    // A diagnostic about a whole file stands on its first line, as the loaders of Ncx.Config report one.
    private const int FileLine = 1;

    // UTF-8 that writes no byte order mark of its own (language 3, Encoding).
    private static readonly UTF8Encoding s_utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Writes the canonical text into the file of --output.
    /// </summary>
    /// <param name="outputFile">The file as the command line names it; the diagnostic carries this name (D98).</param>
    /// <param name="canonical">The canonical text.</param>
    /// <param name="diagnostics">Where the ERROR goes when the file cannot be written.</param>
    public static void Write(string outputFile, string canonical, Diagnostics diagnostics)
    {
        // The canonical text goes into the file of --output as UTF-8 (language 3, Encoding). A file that cannot be
        // written is an ERROR of the run, which has started, so the exit code is 1 (D97, architecture 10;
        // code-guidelines 6).
        try
        {
            File.WriteAllText(outputFile, canonical, s_utf8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            diagnostics.Add(new Diagnostic
            {
                Severity = Severity.Error,
                File = outputFile,
                Line = FileLine,
                Code = DiagnosticCodes.OutputUnwritable,
                Message = $"The canonical text cannot be written into the file: {exception.Message.TrimEnd('.')} "
                    + "(architecture 10).",
            });
        }
    }
}
