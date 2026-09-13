namespace Ncx.Readers.Fanuc;

/// <summary>
/// The facts of the reading state that a subprogram of the file may change, from the codes and words it holds and the
/// subprograms it calls (FanucReader.ChangesOf): the caller continues with the state the subprogram left (virtual
/// machine 3.9), which the reader, reading the subprogram after its caller, knows only as far as the subprogram leaves
/// it alone.
/// </summary>
internal sealed class FanucChanges
{
    // The modal words of the groups the reader writes where the source changes them (FanucMotion.ReadModalWords).
    private static readonly Dictionary<int, string> s_modalWords = new()
    {
        [FanucModalGroups.Plane] = "WORKPLANE",
        [FanucModalGroups.FeedMode] = "FEED_MODE",
        [FanucModalGroups.Units] = "UNITS",
        [FanucModalGroups.Compensation] = "COMP",
    };

    /// <summary>
    /// The modal groups whose code the subprogram sets (controllers fanuc.md 3).
    /// </summary>
    public HashSet<int> Groups { get; } = [];

    /// <summary>
    /// True when it may start a cycle or change the values of one, a cycle code (controllers fanuc.md 6).
    /// </summary>
    public bool StartsCycle { get; set; }

    /// <summary>
    /// True when it may end a cycle or change its return level: G80, G98, G99, a code of group 01 (FanucCycles).
    /// </summary>
    public bool EndsCycle { get; set; }

    /// <summary>
    /// True when it may change the chain of transforms or the G52 shift (language 4.2).
    /// </summary>
    public bool Chain { get; set; }

    /// <summary>
    /// True when it may select a spindle, switch G96 or give a speed (controller-mapping 4).
    /// </summary>
    public bool Spindle { get; set; }

    /// <summary>
    /// True when it may preload or change a tool (controllers fanuc.md 5).
    /// </summary>
    public bool Tool { get; set; }

    /// <summary>
    /// True when it may give an F (controllers fanuc.md 2).
    /// </summary>
    public bool Feed { get; set; }

    /// <summary>
    /// True when it may switch polar interpolation.
    /// </summary>
    public bool Polar { get; set; }

    /// <summary>
    /// True when it may switch tool center point control.
    /// </summary>
    public bool Tcpm { get; set; }

    /// <summary>
    /// Adds what another subprogram may change, one the subprogram calls.
    /// </summary>
    /// <param name="other">The changes of the called subprogram.</param>
    public void Add(FanucChanges other)
    {
        Groups.UnionWith(other.Groups);
        StartsCycle |= other.StartsCycle;
        EndsCycle |= other.EndsCycle;
        Chain |= other.Chain;
        Spindle |= other.Spindle;
        Tool |= other.Tool;
        Feed |= other.Feed;
        Polar |= other.Polar;
        Tcpm |= other.Tcpm;
    }

    /// <summary>
    /// The caller after the call: what the subprogram may change is unknown to the reader (virtual machine 3.9), and
    /// the modal words of those groups are written again where the caller's source sets them, since the subprogram may
    /// have written others.
    /// </summary>
    /// <param name="state">The source-side state of the caller.</param>
    /// <param name="fanuc">The facts of the file.</param>
    public void ApplyTo(SourceState state, FanucState fanuc)
    {
        bool cycle = ChangesCycle(fanuc.Cycle);
        FanucCallerState.Of(state, fanuc).After(this).Restore(state, fanuc);
        foreach (int group in Groups)
        {
            if (s_modalWords.TryGetValue(group, out string? key))
            {
                fanuc.ForgetWritten(key);
            }
        }

        if (cycle)
        {
            fanuc.ForgetWritten("CYCLE");
        }
    }

    /// <summary>
    /// Tells whether the subprogram may change the cycle a run of it starts with: start one, or end the active one.
    /// </summary>
    /// <param name="active">The active cycle; null for none.</param>
    public bool ChangesCycle(FanucCycle? active)
    {
        return StartsCycle || (EndsCycle && active is not null);
    }
}
