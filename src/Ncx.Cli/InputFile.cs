using System.Text;
using Ncx.Core.Model;

namespace Ncx.Cli;

/// <summary>
/// The files a command reads, the NCX file and the machine file of --machine: UTF-8 text, and a file that cannot be
/// read reported as a diagnostic with its name, which decides exit code 2 before the run starts (language 3, Encoding;
/// D97; code-guidelines 6).
/// </summary>
internal static class InputFile
{
    /// <summary>
    /// The byte order mark as it stands at the start of a decoded text.
    /// </summary>
    public const string ByteOrderMark = "\uFEFF";

    // A diagnostic about a whole file stands on its first line, as the loaders of Ncx.Config report one.
    private const int FileLine = 1;

    // UTF-8 that refuses bytes that are no UTF-8 (language 3, Encoding).
    private static readonly UTF8Encoding s_utf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads a file as UTF-8 text.
    /// </summary>
    /// <param name="file">The file as the command line names it; the diagnostic carries this name (D98).</param>
    /// <param name="code">The code of the rule: CLI002 for the NCX file, CLI100 for the machine file.</param>
    /// <param name="subject">What the file is, the start of the message: "The file".</param>
    /// <param name="sections">The sections the message cites: "language 3, Encoding".</param>
    /// <param name="diagnostics">Where the ERROR goes when the file cannot be read.</param>
    /// <returns>The text with its byte order mark, if it has one; null when the file cannot be read.</returns>
    public static string? Read(string file, string code, string subject, string sections, Diagnostics diagnostics)
    {
        // An input that cannot be read, or whose bytes are no UTF-8 text, decides exit code 2 before the run starts;
        // the I/O error is reported as a diagnostic with the file name (D97; language 3, Encoding; code-guidelines 6).
        // A path the file system refuses and bytes that are no UTF-8 raise an ArgumentException.
        try
        {
            return s_utf8.GetString(File.ReadAllBytes(file));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            diagnostics.Add(new Diagnostic
            {
                Severity = Severity.Error,
                File = file,
                Line = FileLine,
                Code = code,
                Message = $"{subject} cannot be read as UTF-8 text: {exception.Message.TrimEnd('.')} ({sections}).",
            });
            return null;
        }
    }
}
