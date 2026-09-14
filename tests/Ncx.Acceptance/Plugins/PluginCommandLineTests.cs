using Ncx.Cli;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// ncx plugin on the command line: the four commands of architecture 10, new, build, check and test, under the one
/// command plugin (implementation 17, P7-02).
/// </summary>
public sealed class PluginCommandLineTests
{
    // Architecture 10: ncx plugin names its four commands in its help.
    [Fact]
    public void PluginHelp_ListsNewBuildCheckAndTest()
    {
        (int exitCode, string output, _) = Run("plugin", "--help");

        Assert.Equal(0, exitCode);
        foreach (string command in new[] { "new", "build", "check", "test" })
        {
            Assert.Matches($"(?m)^  {command} ", output);
        }
    }

    // D97: ncx plugin without one of its commands is a usage error, exit code 2.
    [Fact]
    public void Plugin_WithoutACommand_IsAUsageError()
    {
        (int exitCode, _, string error) = Run("plugin");

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI001: ", error, StringComparison.Ordinal);
    }

    // D97: ncx plugin new without the name of the plugin is a usage error, exit code 2.
    [Fact]
    public void PluginNew_WithoutAName_IsAUsageError()
    {
        (int exitCode, _, string error) = Run("plugin", "new");

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI001: ", error, StringComparison.Ordinal);
    }

    private static (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = Program.Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }
}
