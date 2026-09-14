namespace Ncx.Analytics;

/// <summary>
/// The analytics by the name ncx analyze --analytic takes, "tools", "runtime", each with the factory that creates it
/// for a run; one registration line per analytic in Program.cs, so that a new analytic is a new class and a new line
/// (code-guidelines 4 and 5, Registry; architecture 9).
/// </summary>
public sealed class AnalyticsRegistry
{
    private readonly Dictionary<string, Func<AnalyticOptions, IAnalytic>> _factories = new(StringComparer.Ordinal);
    private readonly List<string> _names = [];

    /// <summary>
    /// The registered names in the order of registration, the order ncx analyze writes the reports in when
    /// --analytic names none.
    /// </summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>
    /// Registers an analytic under its name.
    /// </summary>
    /// <param name="name">The name on the command line: "tools".</param>
    /// <param name="factory">Creates the analytic for one run.</param>
    public void Register(string name, Func<AnalyticOptions, IAnalytic> factory)
    {
        // A name registered twice is a mistake of the composition root, not of the user (code-guidelines 6).
        if (!_factories.TryAdd(name, factory))
        {
            throw new InvalidOperationException($"An analytic named {name} is registered already.");
        }

        _names.Add(name);
    }

    /// <summary>
    /// Creates the analytic of a name for one run; null for a name nothing is registered under.
    /// </summary>
    /// <param name="name">The name on the command line.</param>
    /// <param name="options">The machine, the names, the range and the format of the run.</param>
    public IAnalytic? Create(string name, AnalyticOptions options)
    {
        return _factories.TryGetValue(name, out Func<AnalyticOptions, IAnalytic>? factory) ? factory(options) : null;
    }
}
