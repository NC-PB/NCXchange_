namespace Ncx.Readers.Heidenhain;

/// <summary>
/// What a subprogram may change of the facts of the reader, from its blocks and those of the subprograms it calls: the
/// caller continues with the state the subprogram left (virtual machine 3.9), which the reader reads only after the
/// caller, so what the subprogram may change is unknown to the caller after the call.
/// </summary>
internal sealed class HeidenhainChanges
{
    /// <summary>
    /// A TOOL CALL: the working plane and the NCX cycle, which TOOL ends (virtual machine 4).
    /// </summary>
    public bool Tool { get; set; }

    /// <summary>
    /// A CC: the pole.
    /// </summary>
    public bool Pole { get; set; }

    /// <summary>
    /// A CYCL DEF other than the cycles 7, 8, 9, 10, 19, 32 and 247, one the reader keeps as RAW too: the active
    /// definition (controllers heidenhain.md 5).
    /// </summary>
    public bool DefinesCycle { get; set; }

    /// <summary>
    /// A CYCL CALL, CYCL CALL PAT or M99: whether NCX has the cycle on.
    /// </summary>
    public bool CallsCycle { get; set; }

    /// <summary>
    /// A cycle 7, 8, 10, 19 or 247 or a PLANE: the chain of transforms.
    /// </summary>
    public bool Chain { get; set; }

    /// <summary>
    /// M128, M129 or a FUNCTION: TCPM.
    /// </summary>
    public bool Tcpm { get; set; }

    /// <summary>
    /// A PATTERN DEF: the points of the pattern.
    /// </summary>
    public bool Pattern { get; set; }

    /// <summary>
    /// Adds the changes of a subprogram this one calls.
    /// </summary>
    /// <param name="other">The changes of the called subprogram.</param>
    public void Add(HeidenhainChanges other)
    {
        Tool |= other.Tool;
        Pole |= other.Pole;
        DefinesCycle |= other.DefinesCycle;
        CallsCycle |= other.CallsCycle;
        Chain |= other.Chain;
        Tcpm |= other.Tcpm;
        Pattern |= other.Pattern;
    }

    /// <summary>
    /// Makes what the subprogram may change unknown to the caller after the call; where the tool stands is unknown
    /// after every call (virtual machine 3.9).
    /// </summary>
    /// <param name="state">The facts of the caller.</param>
    public void ApplyTo(HeidenhainState state)
    {
        state.ForgetPositions();
        if (Tool)
        {
            state.Plane = null;
        }

        if (Pole)
        {
            state.Pole = null;
        }

        if (DefinesCycle)
        {
            state.Definition = null;
            state.DefinitionUnknown = true;
        }

        if (Tool || DefinesCycle || CallsCycle)
        {
            state.CycleOn = null;
        }

        if (Chain)
        {
            state.Chain.Forget();
            state.DatumShift.Clear();
        }

        if (Tcpm)
        {
            state.Tcpm = null;
        }

        if (Pattern)
        {
            state.Pattern = null;
        }
    }
}
