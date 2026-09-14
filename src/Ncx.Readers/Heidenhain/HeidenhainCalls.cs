namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The calls of the subprograms of a file: the state each CALL LBL enters with, and what each subprogram may change
/// (virtual machine 3.9). The subprograms stand after the M30 of the program (controllers heidenhain.md 1), so the
/// reader has read every call of a subprogram in the program before it reads the subprogram.
/// </summary>
internal sealed class HeidenhainCalls
{
    private readonly Dictionary<string, List<HeidenhainCallerState>> _callers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HeidenhainChanges> _changes = new(StringComparer.Ordinal);

    /// <summary>
    /// Records the state a call of a subprogram enters with.
    /// </summary>
    /// <param name="name">The name of the subprogram.</param>
    /// <param name="caller">The facts of the reader at the call.</param>
    public void Record(string name, HeidenhainCallerState caller)
    {
        if (!_callers.TryGetValue(name, out List<HeidenhainCallerState>? callers))
        {
            callers = [];
            _callers[name] = callers;
        }

        callers.Add(caller);
    }

    // A subprogram runs with the state of its caller at the CALL (virtual machine 3.9), and a reader reads it once.
    // TODO(question): the documents do not say how a reader reads a block of a subprogram whose meaning depends on its
    // caller (the pole of a C, the cycle an M99 calls, the plane of an arc) where the callers leave different states;
    // P3-02 asks the same for Fanuc. The reader takes what every call agrees on, fact by fact; a fact the calls do not
    // agree on is unknown, and a block that depends on it stays RAW.

    /// <summary>
    /// Starts the facts of a subprogram section from what every call of it agrees on.
    /// </summary>
    /// <param name="name">The name of the subprogram.</param>
    /// <param name="state">The facts of the file, which take the entry state.</param>
    public void Enter(string name, HeidenhainState state)
    {
        List<HeidenhainCallerState> callers = _callers.TryGetValue(name, out List<HeidenhainCallerState>? known)
            ? known
            : [];
        state.ForgetPositions();
        state.Pattern = null;
        HeidenhainCallerState? first = callers.Count > 0 ? callers[0] : null;
        bool plane = true;
        bool pole = true;
        bool definition = first is not null && !first.DefinitionUnknown;
        bool cycleOn = true;
        bool chain = first?.ChainText is not null;
        bool tcpm = true;
        foreach (HeidenhainCallerState caller in callers)
        {
            plane &= caller.Plane == first!.Plane;
            pole &= caller.Pole == first.Pole;
            definition &= !caller.DefinitionUnknown && ReferenceEquals(caller.Definition, first.Definition);
            cycleOn &= caller.CycleOn == first.CycleOn;
            chain &= caller.ChainText == first.ChainText;
            tcpm &= caller.Tcpm == first.Tcpm;
        }

        state.Plane = plane ? first?.Plane : null;
        state.Pole = pole ? first?.Pole : null;
        state.PoleDecimals = first?.PoleDecimals ?? 0;
        state.Definition = definition ? first!.Definition : null;
        state.DefinitionUnknown = !definition;
        state.CycleOn = cycleOn ? first?.CycleOn : null;
        state.CycleCalled = true;
        state.Tcpm = tcpm ? first?.Tcpm : null;
        state.DatumShift.Clear();
        if (chain)
        {
            state.Chain.Restore(first!.Chain!);
            foreach (KeyValuePair<string, decimal> axis in first.DatumShift)
            {
                state.DatumShift[axis.Key] = axis.Value;
            }
        }
        else
        {
            state.Chain.Forget();
        }
    }

    /// <summary>
    /// What a subprogram of the file may change, from its blocks and those of the subprograms it calls, worked out once
    /// per subprogram.
    /// </summary>
    /// <param name="name">The name of the subprogram.</param>
    /// <param name="state">The facts of the file with its blocks and labels.</param>
    public HeidenhainChanges ChangesOf(string name, HeidenhainState state)
    {
        if (_changes.TryGetValue(name, out HeidenhainChanges? known))
        {
            return known;
        }

        HeidenhainChanges changes = ChangesOf(name, state, new HashSet<string>(StringComparer.Ordinal));
        _changes[name] = changes;
        return changes;
    }

    private static HeidenhainChanges ChangesOf(string name, HeidenhainState state, HashSet<string> visited)
    {
        var changes = new HeidenhainChanges();
        HeidenhainLabels labels = state.Labels;
        if (!visited.Add(name) || !labels.SubFirst.TryGetValue(name, out int first))
        {
            return changes;
        }

        for (int index = first; index <= labels.SubLast[name]; index++)
        {
            SourceBlock block = state.Blocks[index];
            AddChanges(changes, block);
            if (HeidenhainLabels.IsLabelCall(block) && !HeidenhainLabels.IsRepeat(block)
                && HeidenhainLabels.LabelOf(block) is string called && labels.Subs.Contains(called))
            {
                changes.Add(ChangesOf(called, state, visited));
            }
        }

        return changes;
    }

    // What one block of a subprogram may change: TOOL CALL the plane and the cycle, CC the pole, CYCL DEF the
    // definition or the chain, CYCL CALL and M99 the cycle, PLANE the chain, PATTERN DEF the pattern, M128, M129 and
    // FUNCTION TCPM.
    private static void AddChanges(HeidenhainChanges changes, SourceBlock block)
    {
        string first = block.Words.Count > 0 ? block.Words[0].Address : "";
        string second = block.Words.Count > 1 ? block.Words[1].Address : "";
        changes.Tool |= first == "TOOL" && second == "CALL";
        changes.Pole |= first == "CC";
        changes.Chain |= first == "PLANE";
        changes.Pattern |= first == "PATTERN";
        changes.Tcpm |= first == "FUNCTION";
        changes.CallsCycle |= first == "CYCL" && second == "CALL";
        // Every CYCL DEF but a cycle acting where it stands replaces the definition, as the reader reads it: one the
        // reader keeps as RAW and one whose cycle number it cannot read too (controllers heidenhain.md 5).
        if (first == "CYCL" && second == "DEF")
        {
            int? frame = HeidenhainCycles.FrameCycleOf(block);
            changes.Chain |= frame is 7 or 8 or 10 or 19 or 247;
            changes.DefinesCycle |= frame is null;
        }

        foreach (SourceWord word in block.Words)
        {
            string? code = word.Address == "M" ? NativeCode.Of(word) : null;
            changes.Tcpm |= code is "M128" or "M129";
            changes.CallsCycle |= code == "M99";
        }
    }
}
