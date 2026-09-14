namespace Ncx.Compilers;

/// <summary>
/// What the target control has active (phase 3, P3-03): the modal G of every group, the last feed, the active tool,
/// the last spindle M, each under a key the compiler of the family names ("G01" for the motion group, "F", "T",
/// "M:S1"), so that a modal word is written only on change. A key never written is unknown: at the start of every
/// program, and at the start of every walk of a subprogram, which is written from an unknown target state so that
/// every modal word stands at its first use inside it (virtual machine 3.9, D99).
/// </summary>
public sealed class TargetState
{
    private readonly Dictionary<string, string> _active = new(StringComparer.Ordinal);

    /// <summary>
    /// What the control has active under each key, as written.
    /// </summary>
    public IReadOnlyDictionary<string, string> Active => _active;

    /// <summary>
    /// The value active under a key; null while it is unknown.
    /// </summary>
    /// <param name="key">The key: "G01", "F".</param>
    public string? ActiveOf(string key)
    {
        return _active.TryGetValue(key, out string? value) ? value : null;
    }

    /// <summary>
    /// Tells whether a value must be written, and records that the control has it active from now on: true when the
    /// key was unknown or held another value, false when the control has this value active already.
    /// </summary>
    /// <param name="key">The key: "G01".</param>
    /// <param name="value">The value as written: "G1".</param>
    public bool Changes(string key, string value)
    {
        // A modal word is written only on change (phase 3, P3-03; controllers fanuc.md 10 rule 1).
        if (_active.TryGetValue(key, out string? active) && active == value)
        {
            return false;
        }

        _active[key] = value;
        return true;
    }

    /// <summary>
    /// Records a value the control has active, written by the compiler or set by the control itself.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value as written.</param>
    public void Set(string key, string value)
    {
        _active[key] = value;
    }

    /// <summary>
    /// Makes a key unknown again, so that its next value is written whatever it is.
    /// </summary>
    /// <param name="key">The key.</param>
    public void Forget(string key)
    {
        _active.Remove(key);
    }

    /// <summary>
    /// Takes over what a later part of the program wrote: after a subprogram returns, what its walk wrote is active on
    /// the control, and what it left unknown is as the caller left it (virtual machine 3.9, D99).
    /// </summary>
    /// <param name="later">The target state at the end of the walk.</param>
    public void TakeOver(TargetState later)
    {
        foreach (KeyValuePair<string, string> active in later._active)
        {
            _active[active.Key] = active.Value;
        }
    }
}
