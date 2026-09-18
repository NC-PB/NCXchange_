using System.Text;
using Ncx.Core.Model;

namespace Ncx.Cli;

/// <summary>
/// The controller program that ncx convert reads, alone or as one file of --batch: its text as the reader gets it,
/// UTF-8 when its bytes are UTF-8, Windows-1252 with a WARNING otherwise, without a byte order mark (D229; language 3,
/// Encoding; wave-1 question #80).
/// </summary>
internal static class ControllerProgram
{
    // A diagnostic about a whole file stands on its first line, as the loaders of Ncx.Config report one.
    private const int FileLine = 1;

    // Windows-1252, the code page of the older controls and editors (D229).
    private const int Windows1252CodePage = 1252;

    // UTF-8 that refuses bytes that are no UTF-8 (language 3, Encoding).
    private static readonly UTF8Encoding s_utf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads a controller program.
    /// </summary>
    /// <param name="file">The program as the command line names it; the diagnostics carry this name (D98).</param>
    /// <param name="diagnostics">Where the ERROR goes when the file cannot be read, and the WARNING when it is read as
    /// Windows-1252.</param>
    /// <returns>The text without a byte order mark; null when the file cannot be read.</returns>
    public static string? Read(string file, Diagnostics diagnostics)
    {
        // A program that cannot be read decides exit code 2 before the run of ncx convert starts; the I/O error is
        // reported as a diagnostic with the file name (D97; code-guidelines 6). A path the file system refuses raises
        // an ArgumentException.
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException)
        {
            diagnostics.Error(FileLine, DiagnosticCodes.InputUnreadable,
                $"The controller program cannot be read: {exception.Message.TrimEnd('.')} (D97).");
            return null;
        }

        // TODO(question): D229, as recommended: no document gives the encoding of a controller program. It is read as
        // UTF-8 when its bytes are valid UTF-8, and otherwise as Windows-1252 with a WARNING that names the code page,
        // until D229 is answered.
        string text;
        try
        {
            text = s_utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            text = CodePagesEncodingProvider.Instance.GetEncoding(Windows1252CodePage)!.GetString(bytes);
            diagnostics.Warning(FileLine, DiagnosticCodes.ProgramReadAsWindows1252,
                "The controller program is no UTF-8 text, so it is read as Windows-1252, the code page older controls "
                + "and editors write umlauts in; the NCX text is UTF-8 (D229; language 3, Encoding).");
        }

        // A byte order mark is no part of the text a reader reads, as it is none of the text the parser reads (wave-1
        // question #80).
        return text.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal)
            ? text.Substring(InputFile.ByteOrderMark.Length)
            : text;
    }
}
