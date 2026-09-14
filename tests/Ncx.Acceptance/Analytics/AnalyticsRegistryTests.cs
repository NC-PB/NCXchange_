using Ncx.Analytics;
using Ncx.Analytics.Runtime;
using Ncx.Analytics.Segments;
using Ncx.Analytics.ToolList;
using Ncx.Analytics.ToolVectors;
using Ncx.Cli;
using Ncx.Config;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The analytics register by name at start-up, one registration line each in Program.cs (code-guidelines 5, Registry;
/// architecture 9), and ncx analyze --analytic chooses from them.
/// </summary>
public sealed class AnalyticsRegistryTests
{
    // Implementation 14, P4-02 and P4-03: the tool list, the runtime estimate, the segment length and the tool vector
    // change are the analytics tools, runtime, segments and vectors, written in that order when --analytic names none.
    [Fact]
    public void Analytics_Program_RegistersToolsRuntimeSegmentsAndVectorsInThatOrder()
    {
        AnalyticsRegistry analytics = Program.Analytics();

        Assert.Equal(["tools", "runtime", "segments", "vectors"], analytics.Names);
    }

    // A registered name creates its analytic with the options of the run, the block range among them (D67).
    [Fact]
    public void Create_RegisteredName_CreatesItsAnalyticWithTheRange()
    {
        AnalyticsRegistry analytics = Program.Analytics();
        var range = new BlockRange { From = 380, To = 600 };

        IAnalytic? tools = analytics.Create("tools", AnalyticRuns.Options(DefaultMachine.Create(), range));
        IAnalytic? runtime = analytics.Create("runtime", AnalyticRuns.Options(DefaultMachine.Create(), range));
        IAnalytic? segments = analytics.Create("segments", AnalyticRuns.Options(DefaultMachine.Create(), range));
        IAnalytic? vectors = analytics.Create("vectors", AnalyticRuns.Options(DefaultMachine.Create(), range));

        Assert.IsType<ToolListAnalytic>(tools);
        Assert.IsType<RuntimeAnalytic>(runtime);
        Assert.IsType<SegmentAnalytic>(segments);
        Assert.IsType<ToolVectorAnalytic>(vectors);
        Assert.Equal(range, runtime.Range);
        Assert.Equal(range, segments.Range);
        Assert.Equal(range, vectors.Range);
    }

    // A name nothing is registered under creates nothing; the command line reports it as a usage error. The loop
    // statistics of architecture 9 come with M9 (D67), so "loops" is no analytic yet.
    [Fact]
    public void Create_UnknownName_IsNull()
    {
        Assert.Null(Program.Analytics().Create("loops", AnalyticRuns.Options(DefaultMachine.Create())));
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
