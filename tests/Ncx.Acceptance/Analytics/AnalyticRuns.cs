using Ncx.Analytics;
using Ncx.Config;
using Ncx.Core.Expander;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// Runs a program as ncx analyze runs it, parsed, expanded and executed INTERPRETED with ExpandCycles and an analytic
/// subscribed (implementation 14, P4-02; D37), through the library, so that a test reads the totals of the analytic.
/// </summary>
internal static class AnalyticRuns
{
    /// <summary>
    /// The options of an analytic in a test: the file test.ncx on the machine test.toml.
    /// </summary>
    public static AnalyticOptions Options(MachineConfig machine, BlockRange? range = null,
        ExecutionMode mode = ExecutionMode.Interpreted)
    {
        return new AnalyticOptions
        {
            Machine = machine,
            MachineName = "test.toml",
            FileName = "test.ncx",
            Range = range ?? BlockRange.Whole,
            Mode = mode,
        };
    }

    /// <summary>
    /// Runs a program text with the analytic subscribed and returns its report; the run reports no ERROR.
    /// </summary>
    public static string Run(string text, MachineConfig machine, IAnalytic analytic,
        ExecutionMode mode = ExecutionMode.Interpreted)
    {
        NcxProgram program = Parser.Parse(text, "test.ncx", new ParserOptions());
        NcxProgram expanded = Expander.Expand(program, machine, []);
        VmOptions options = VmOptions.ForMachine(machine) with { ExpandCycles = true };
        var vm = new VirtualMachine(machine, options, expanded.Diagnostics, mode);
        vm.Subscribe(analytic);
        vm.Run(expanded);

        Assert.False(expanded.Diagnostics.HasErrors, expanded.Diagnostics.ToText());
        return analytic.Report();
    }

    /// <summary>
    /// Runs an example of the specification without a machine file, against the built-in default machine of D103,
    /// with the analytic subscribed, and returns its report.
    /// </summary>
    public static string RunExample(string example, IAnalytic analytic, ExecutionMode mode = ExecutionMode.Interpreted)
    {
        return Run(Fixture.ReadText(example), DefaultMachine.Create(), analytic, mode);
    }
}
