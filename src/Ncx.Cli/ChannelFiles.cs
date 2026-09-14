namespace Ncx.Cli;

/// <summary>
/// The files a job reads for its channels (JobPipeline): the text of every file by its path, and the vars file of an
/// INTERPRETED run next to a file, with its text, by the path of that file (virtual machine 3.6; machine-config 8, 10).
/// </summary>
internal sealed class ChannelFiles
{
    /// <summary>
    /// The text of every file of a channel, by its path from the working directory.
    /// </summary>
    public Dictionary<string, string> Texts { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The vars file of a file, by the path of the file.
    /// </summary>
    public Dictionary<string, string> VarsFiles { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The text of the vars file of a file, by the path of the file.
    /// </summary>
    public Dictionary<string, string> VarsTexts { get; } = new(StringComparer.Ordinal);
}
