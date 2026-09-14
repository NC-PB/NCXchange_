using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The reports that are the reference of the analytics for later versions, in tests/Ncx.Acceptance/Expected/analytics
/// (implementation 14, P4-02 and P4-03), compared as whole files (code-guidelines 8).
/// </summary>
internal static class AnalyticsReference
{
    /// <summary>
    /// Compares a report with its reference file, both with LF line endings; on a difference, and when there is no
    /// reference yet, the report is written to the temporary folder ncx-acceptance for a diff or a review, as
    /// CliHarness.AssertExpectedFile does.
    /// </summary>
    /// <param name="referenceName">The name of the file in Expected/analytics: 2.5D_FRAESEN.tools.txt.</param>
    /// <param name="actual">The report.</param>
    public static void AssertEquals(string referenceName, string actual)
    {
        string referencePath = Path.Combine(
            Fixture.RepositoryRoot(), "tests", "Ncx.Acceptance", "Expected", "analytics", referenceName);
        string? expected = File.Exists(referencePath) ? File.ReadAllText(referencePath).ReplaceLineEndings("\n") : null;
        string normalized = actual.ReplaceLineEndings("\n");
        if (normalized != expected)
        {
            string folder = Path.Combine(Path.GetTempPath(), "ncx-acceptance");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, referenceName), normalized);
        }

        // A report of the corpus has no reference until the maintainer has reviewed its first run (implementation 14,
        // P4-03).
        Assert.True(expected is not null,
            $"There is no reference {referencePath} yet: the report is in the temporary folder ncx-acceptance, to be "
            + "reviewed and committed as the reference (implementation 14, P4-03).");
        Assert.True(normalized == expected,
            $"The report differs from {referencePath} (actual in the temporary folder ncx-acceptance).\n"
            + $"Expected:\n{expected}\nActual:\n{normalized}");
    }
}
