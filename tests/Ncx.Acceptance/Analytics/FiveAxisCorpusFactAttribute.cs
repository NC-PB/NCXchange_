using Ncx.Acceptance.Examples;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// A fact on the 5-axis A/C pair of the maintainer's corpus, which stays outside the repository (sources README;
/// implementation 14, P4-03): it runs when the environment variable NCX_CORPUS names the corpus folder and that folder
/// holds the folder 5X with the pair and its machine files, and is skipped with a message otherwise (implementation
/// 00-method 4).
/// </summary>
internal sealed class FiveAxisCorpusFactAttribute : FactAttribute
{
    /// <summary>
    /// The folder of the corpus that holds the 5-axis A/C pair: the Klartext program (.h) with the machine file
    /// heidenhain.toml, the Fanuc program (.nc) with fanuc.toml.
    /// </summary>
    public const string FolderName = "5X";

    /// <summary>
    /// Skips the fact while the pair is not there.
    /// </summary>
    public FiveAxisCorpusFactAttribute()
    {
        string? corpus = Environment.GetEnvironmentVariable(CorpusFactAttribute.Variable);
        if (string.IsNullOrEmpty(corpus))
        {
            Skip = $"{CorpusFactAttribute.Variable} is not set: the corpus of the maintainer stays outside the "
                + "repository, and the reference of the 5-axis program waits for it (implementation 00-method 4; "
                + "implementation 14, P4-03).";
        }
        else if (!Directory.Exists(Path.Combine(corpus, FolderName)))
        {
            Skip = $"The corpus has no folder {FolderName} with the 5-axis A/C pair and its machine files "
                + "heidenhain.toml and fanuc.toml: the reference of the 5-axis program waits for it (implementation "
                + "14, P4-03).";
        }
    }

    /// <summary>
    /// The folder of the pair; only for a fact that is not skipped.
    /// </summary>
    public static string Folder()
    {
        return Path.Combine(Environment.GetEnvironmentVariable(CorpusFactAttribute.Variable) ?? "", FolderName);
    }
}
