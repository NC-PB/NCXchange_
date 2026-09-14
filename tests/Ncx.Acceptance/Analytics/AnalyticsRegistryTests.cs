using Ncx.Analytics;
using Ncx.Analytics.Runtime;
using Ncx.Analytics.ToolList;
using Ncx.Cli;
using Ncx.Config;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The analytics register by name at start-up, one registration line each in Program.cs (code-guidelines 5, Registry;
/// architecture 9), and ncx analyze --analytic chooses from them.
/// </summary>
public sealed class AnalyticsRegistryTests
{
    // Implementation 14, P4-02: the tool list and the runtime estimate are the analytics tools and runtime, written in
    // that order when --analytic names none.
    [Fact]
    public void Analytics_Program_RegistersToolsAndRuntimeInThatOrder()
    {
        AnalyticsRegistry analytics = Program.Analytics();

        Assert.Equal(["tools", "runtime"], analytics.Names);
    }

    // A registered name creates its analytic with the options of the run, the block range among them (D67).
    [Fact]
    public void Create_RegisteredName_CreatesItsAnalyticWithTheRange()
    {
        AnalyticsRegistry analytics = Program.Analytics();
        var range = new BlockRange { From = 380, To = 600 };

        IAnalytic? tools = analytics.Create("tools", AnalyticRuns.Options(DefaultMachine.Create(), range));
        IAnalytic? runtime = analytics.Create("runtime", AnalyticRuns.Options(DefaultMachine.Create(), range));

        Assert.IsType<ToolListAnalytic>(tools);
        Assert.IsType<RuntimeAnalytic>(runtime);
        Assert.Equal(range, runtime.Range);
    }

    // A name nothing is registered under creates nothing; the command line reports it as a usage error.
    [Fact]
    public void Create_UnknownName_IsNull()
    {
        Assert.Null(Program.Analytics().Create("segments", AnalyticRuns.Options(DefaultMachine.Create())));
    }

    // A name registered twice is a mistake of the composition root (code-guidelines 6).
    [Fact]
    public void Register_NameTwice_Throws()
    {
        var analytics = new AnalyticsRegistry();
        analytics.Register("tools", options => new ToolListAnalytic(options));

        Assert.Throws<InvalidOperationException>(
            () => analytics.Register("tools", options => new ToolListAnalytic(options)));
    }
}
