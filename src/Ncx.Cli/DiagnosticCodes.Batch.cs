namespace Ncx.Cli;

// The codes of ncx convert --batch, CLI350-CLI399 (P3-07): the folder the batch converts, the crash of one of its
// files, and the code page of a controller program that is no UTF-8 text, which convert reads with or without --batch
// (D229).
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CLI350: the folder of ncx convert --batch cannot be read: it is not there, it is no folder, or one of its
    /// folders cannot be listed; decided before the run starts, exit code 2 (D97; implementation 13, P3-07).
    /// </summary>
    public const string BatchFolderUnreadable = "CLI350";

    /// <summary>
    /// CLI351: the conversion of one file of ncx convert --batch stopped with an exception, a crash, which is a bug of
    /// ncx; the batch reports it on the file, counts it in its report and goes on with the next file, exit code 1
    /// (controllers sample-corpus 3; implementation 13, P3-07).
    /// </summary>
    public const string BatchFileCrashed = "CLI351";

    /// <summary>
    /// CLI352, a WARNING: the controller program is no UTF-8 text, so it is read as Windows-1252, the code page older
    /// controls and editors write their umlauts in; the NCX text is UTF-8 all the same (D229; language 3, Encoding).
    /// </summary>
    public const string ProgramReadAsWindows1252 = "CLI352";
}
