using System.Diagnostics.CodeAnalysis;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The calls of the sections of the file that a block enters (virtual machine 3.9; controller-mapping 6; language
/// 4.7.1): how many blocks call each subprogram and name each contour section, the state of the caller at every call
/// the reader has read, the O line of every program and subprogram, the first block of every contour section, and what
/// each subprogram may change.
/// </summary>
internal sealed class FanucCalls
{
    private readonly Dictionary<string, int> _expected = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<FanucCallerState>> _callers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FanucChanges> _changes = new(StringComparer.Ordinal);

    /// <summary>
    /// The O line of every program and subprogram of the file, by its number without leading zeros, "100" of O0100.
    /// </summary>
    public Dictionary<string, SourceBlock> Begins { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The NAME of every contour section by the line of its first block (language 4.7.1, D65).
    /// </summary>
    public Dictionary<int, string> ContourBegins { get; } = [];

    /// <summary>
    /// A block of the file enters the section: a call of the subprogram, or a cycle whose contour it is.
    /// </summary>
    /// <param name="name">The name of the section, "100" or "CONTOUR_10_20".</param>
    public void Expect(string name)
    {
        _expected[name] = _expected.GetValueOrDefault(name) + 1;
    }

    /// <summary>
    /// The state of the caller at a call the reader has read.
    /// </summary>
    /// <param name="name">The name of the section the call enters.</param>
    /// <param name="caller">The state the call enters with.</param>
    public void Record(string name, FanucCallerState caller)
    {
        if (!_callers.TryGetValue(name, out List<FanucCallerState>? callers))
        {
            callers = [];
            _callers[name] = callers;
        }

        callers.Add(caller);
    }

    /// <summary>
    /// The state a section runs with (virtual machine 3.9): what every call agrees on where the reader has read every
    /// call of the file before the section, else the state of a caller the reader does not know.
    /// </summary>
    /// <param name="name">The name of the section; null for one the reader cannot name.</param>
    /// <param name="groups">The modal groups the reading of a block consults.</param>
    public FanucCallerState EntryOf(string? name, IReadOnlyCollection<int> groups)
    {
        return name is not null && _expected.TryGetValue(name, out int count)
            && _callers.TryGetValue(name, out List<FanucCallerState>? callers) && callers.Count == count
            ? FanucCallerState.Agreed(callers, groups)
            : FanucCallerState.Unknown(groups);
    }

    /// <summary>
    /// What a subprogram may change, once the reader has worked it out.
    /// </summary>
    /// <param name="name">The name of the subprogram.</param>
    /// <param name="changes">Its changes.</param>
    public bool TryGetChanges(string name, [NotNullWhen(true)] out FanucChanges? changes)
    {
        return _changes.TryGetValue(name, out changes);
    }

    /// <summary>
    /// Keeps what a subprogram may change.
    /// </summary>
    /// <param name="name">The name of the subprogram.</param>
    /// <param name="changes">Its changes.</param>
    public void KeepChanges(string name, FanucChanges changes)
    {
        _changes[name] = changes;
    }
}
