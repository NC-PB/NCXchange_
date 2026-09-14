using System.Text;

namespace Ncx.Acceptance.Jobs;

/// <summary>
/// The fixtures of Ncx.Acceptance that are no example of the specification (Fixtures/), embedded in the test assembly
/// under their path from the repository root, read without the working directory (code-guidelines 8, tests/README.md).
/// </summary>
internal static class JobFixture
{
    // Every fixture of this project carries its path from the repository root as its logical name (the project file).
    private const string FixturesFolder = "tests/Ncx.Acceptance/Fixtures/";

    /// <summary>
    /// The job manifest of the Nakamura WY-250L pair.
    /// </summary>
    public const string NakamuraJob = "nakamura-wy250l.ncxjob.toml";

    /// <summary>
    /// Reads a fixture as UTF-8 text.
    /// </summary>
    /// <param name="name">The file name in Fixtures/, "nakamura-wy250l.ncxjob.toml".</param>
    public static string ReadText(string name)
    {
        using Stream stream = typeof(JobFixture).Assembly.GetManifestResourceStream(FixturesFolder + name)
            ?? throw new ArgumentException($"{name} is not a fixture of tests/Ncx.Acceptance/Fixtures.", nameof(name));
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
