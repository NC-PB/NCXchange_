using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The reports that are the reference of the analytics for later versions, in tests/Ncx.Acceptance/Expected/analytics
/// (implementation 14, P4-02 and P4-03), compared as whole files (code-guidelines 8).
/// </summary>
internal static class AnalyticsReference
{
    /// <summary>
    /// Compares a report with its reference file, both with LF line endings; on a difference the report is written to
    /// the temporary folder ncx-acceptance for a diff, as CliHarness.AssertExpectedFile does.
    /// </summary>
    /// <param name="referenceName">The name of the file in Expected/analytics: 2.5D_FRAESEN.tools.txt.</param>
    /// <param name="actual">The report.</param>
    public static void AssertEquals(string referenceName, string actual)
    {
        string referencePath = Path.Combine(
            Fixture.RepositoryRoot(), "tests", "Ncx.Acceptance", "Expected", "analytics", referenceName);
        string expected = File.ReadAllText(referencePath).ReplaceLineEndings("\n");
        string normalized = actual.ReplaceLineEndings("\n");
        if (normalized != expected)
        {
            string folder = Path.Combine(Path.GetTempPath(), "ncx-acceptance");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, referenceName), normalized);
        }

        Assert.True(normalized == expected,
            $"The report differs from {referencePath} (actual in the temporary folder ncx-acceptance).\n"
            + $"Expected:\n{expected}\nActual:\n{normalized}");
    }
}
