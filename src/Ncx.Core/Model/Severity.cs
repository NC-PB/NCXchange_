namespace Ncx.Core.Model;

/// <summary>
/// The weight of a diagnostic (D98): an ERROR stops the run, a WARNING is reported and the run continues, an INFO
/// is a note that is neither and never changes the outcome or the exit code (virtual machine 2.9).
/// </summary>
public enum Severity
{
    /// <summary>
    /// ERROR: the run stops.
    /// </summary>
    Error,

    /// <summary>
    /// WARNING: reported, and the run continues.
    /// </summary>
    Warning,

    /// <summary>
    /// INFO: a note that is neither, such as the blocks a plugin inserted.
    /// </summary>
    Info,
}
