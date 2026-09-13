namespace Ncx.Acceptance.Examples;

/// <summary>
/// A fact that needs the maintainer's corpus, which stays outside the repository: it runs when the environment variable
/// NCX_CORPUS names the corpus folder and is skipped with a message otherwise (implementation 00-method 4; controllers
/// sample-corpus 2).
/// </summary>
internal sealed class CorpusFactAttribute : FactAttribute
{
    /// <summary>
    /// The environment variable that names the corpus folder.
    /// </summary>
    public const string Variable = "NCX_CORPUS";

    /// <summary>
    /// Skips the fact when the corpus is not there.
    /// </summary>
    public CorpusFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable)))
        {
            Skip = $"{Variable} is not set: the corpus of the maintainer stays outside the repository (implementation "
                + "00-method 4).";
        }
    }
}
