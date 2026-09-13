using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The reading state a subprogram runs with, the state of its caller at the CALL (virtual machine 3.9): the facts the
/// reader consults to read a block, each known or unknown, taken at every call the reader reads (FanucCalls). They are
/// the modal groups, the active cycle, the modal F of the control and the F written last, the chain of transforms with
/// the G52 shift, the spindle of a bare S, G96 and the speeds, the preloaded tool, polar interpolation and tool center
/// point control.
/// </summary>
internal sealed class FanucCallerState
{
    private readonly Dictionary<int, string> _groups = [];
    private readonly HashSet<int> _unknownGroups = [];
    private readonly FanucChain _chain = new();
    private readonly Dictionary<string, decimal> _rpm = new(StringComparer.Ordinal);
    private FanucCycle? _cycle;
    private bool _cycleUnknown;
    private string? _spindle;
    private bool _spindleUnknown;
    private bool _css;
    private bool _cssUnknown;
    private bool _speedsUnknown;
    private ToolRef? _preloaded;
    private bool _preloadUnknown;
    private Value? _controlFeed;
    private Value? _writtenFeed;
    private bool _polar;
    private bool _polarUnknown;
    private bool _tcpm;
    private bool _vectorTcpm;
    private bool _tcpmUnknown;

    /// <summary>
    /// The state where the reader stands, every fact as it holds it: at a call, the state the call enters with.
    /// </summary>
    /// <param name="state">The source-side state.</param>
    /// <param name="fanuc">The facts of the file.</param>
    public static FanucCallerState Of(SourceState state, FanucState fanuc)
    {
        var caller = new FanucCallerState
        {
            _cycle = fanuc.Cycle?.Copy(),
            _cycleUnknown = fanuc.Unknowns.Cycle,
            _spindle = state.LastSpindle,
            _spindleUnknown = fanuc.Unknowns.Spindle,
            _css = fanuc.CssOn,
            _cssUnknown = fanuc.Unknowns.Css,
            _speedsUnknown = fanuc.Unknowns.Speeds,
            _preloaded = state.Preloaded,
            _preloadUnknown = fanuc.Unknowns.Preload,
            _controlFeed = fanuc.ControlFeed,
            _writtenFeed = fanuc.WrittenFeed,
            _polar = fanuc.Polar,
            _polarUnknown = fanuc.Unknowns.Polar,
            _tcpm = fanuc.Tcpm,
            _vectorTcpm = fanuc.VectorTcpm,
            _tcpmUnknown = fanuc.Unknowns.Tcpm,
        };
        foreach (KeyValuePair<int, string> group in state.ModalGroups)
        {
            caller._groups[group.Key] = group.Value;
        }

        caller._unknownGroups.UnionWith(fanuc.Unknowns.Groups);
        caller._chain.CopyFrom(fanuc.Chain);
        foreach (KeyValuePair<string, decimal> speed in fanuc.Rpm)
        {
            caller._rpm[speed.Key] = speed.Value;
        }

        return caller;
    }

    /// <summary>
    /// The state of a caller the reader does not know: every fact unknown, of the modal groups those the reading of a
    /// block consults.
    /// </summary>
    /// <param name="groups">The modal groups the reading of a block consults.</param>
    public static FanucCallerState Unknown(IEnumerable<int> groups)
    {
        var caller = new FanucCallerState
        {
            _cycleUnknown = true,
            _spindleUnknown = true,
            _cssUnknown = true,
            _speedsUnknown = true,
            _preloadUnknown = true,
            _polarUnknown = true,
            _tcpmUnknown = true,
        };
        caller._unknownGroups.UnionWith(groups);
        caller._chain.Forget();
        return caller;
    }

    /// <summary>
    /// What every call agrees on, fact by fact: a fact the calls hold differently is unknown, and a modal F they hold
    /// differently is none, since the virtual machine walks the subprogram with the F of each caller (virtual machine
    /// 3.9).
    /// </summary>
    /// <param name="calls">The states of the calls; none gives the unknown state.</param>
    /// <param name="groups">The modal groups the reading of a block consults.</param>
    public static FanucCallerState Agreed(IReadOnlyList<FanucCallerState> calls, IEnumerable<int> groups)
    {
        if (calls.Count == 0)
        {
            return Unknown(groups);
        }

        FanucCallerState first = calls[0];
        var agreed = new FanucCallerState();
        var named = new HashSet<int>();
        foreach (FanucCallerState call in calls)
        {
            named.UnionWith(call._groups.Keys);
            named.UnionWith(call._unknownGroups);
        }

        foreach (int group in named)
        {
            if (!Agree(calls, call => call.GroupKey(group)) || first._unknownGroups.Contains(group))
            {
                agreed._unknownGroups.Add(group);
            }
            else if (first._groups.TryGetValue(group, out string? code))
            {
                agreed._groups[group] = code;
            }
        }

        bool cycle = Agree(calls, call => call.CycleKey());
        agreed._cycle = cycle ? first._cycle?.Copy() : null;
        agreed._cycleUnknown = !cycle || first._cycleUnknown;
        if (Agree(calls, call => call._chain.ToKey()))
        {
            agreed._chain.CopyFrom(first._chain);
        }
        else
        {
            agreed._chain.Forget();
        }

        bool spindle = Agree(calls, call => call.SpindleKey());
        agreed._spindle = spindle ? first._spindle : null;
        agreed._spindleUnknown = !spindle || first._spindleUnknown;
        bool css = Agree(calls, call => call.CssKey());
        agreed._css = css && first._css;
        agreed._cssUnknown = !css || first._cssUnknown;
        bool speeds = Agree(calls, call => call.SpeedsKey());
        if (speeds)
        {
            foreach (KeyValuePair<string, decimal> speed in first._rpm)
            {
                agreed._rpm[speed.Key] = speed.Value;
            }
        }

        agreed._speedsUnknown = !speeds || first._speedsUnknown;
        bool preload = Agree(calls, call => call.PreloadKey());
        agreed._preloaded = preload ? first._preloaded : null;
        agreed._preloadUnknown = !preload || first._preloadUnknown;
        if (Agree(calls, call => call.FeedKey()))
        {
            agreed._controlFeed = first._controlFeed;
            agreed._writtenFeed = first._writtenFeed;
        }

        bool polar = Agree(calls, call => call.PolarKey());
        agreed._polar = polar && first._polar;
        agreed._polarUnknown = !polar || first._polarUnknown;
        bool tcpm = Agree(calls, call => call.TcpmKey());
        agreed._tcpm = tcpm && first._tcpm;
        agreed._vectorTcpm = tcpm && first._vectorTcpm;
        agreed._tcpmUnknown = !tcpm || first._tcpmUnknown;
        return agreed;
    }

    /// <summary>
    /// The state after a run of a subprogram that may change these facts: they are unknown, the others stay (virtual
    /// machine 3.9, a CALL with TIMES runs each pass from the state the pass before left).
    /// </summary>
    /// <param name="changes">What the subprogram may change.</param>
    public FanucCallerState After(FanucChanges changes)
    {
        FanucCallerState next = Agreed([this], _unknownGroups);
        foreach (int group in changes.Groups)
        {
            next._groups.Remove(group);
            next._unknownGroups.Add(group);
        }

        if (changes.ChangesCycle(next._cycle))
        {
            next._cycle = null;
            next._cycleUnknown = true;
        }

        if (changes.Chain)
        {
            next._chain.Forget();
        }

        if (changes.Spindle)
        {
            next._spindle = null;
            next._spindleUnknown = true;
            next._css = false;
            next._cssUnknown = true;
            next._rpm.Clear();
            next._speedsUnknown = true;
        }

        if (changes.Tool)
        {
            next._preloaded = null;
            next._preloadUnknown = true;
        }

        if (changes.Feed)
        {
            next._controlFeed = null;
            next._writtenFeed = null;
        }

        if (changes.Polar)
        {
            next._polar = false;
            next._polarUnknown = true;
        }

        if (changes.Tcpm)
        {
            next._tcpm = false;
            next._vectorTcpm = false;
            next._tcpmUnknown = true;
        }

        return next;
    }

    /// <summary>
    /// Makes this the state where the reader stands.
    /// </summary>
    /// <param name="state">The source-side state.</param>
    /// <param name="fanuc">The facts of the file.</param>
    public void Restore(SourceState state, FanucState fanuc)
    {
        state.SetModalGroups(_groups);
        fanuc.Unknowns.Groups.Clear();
        fanuc.Unknowns.Groups.UnionWith(_unknownGroups);
        fanuc.Cycle = _cycle?.Copy();
        fanuc.Unknowns.Cycle = _cycleUnknown;
        fanuc.Chain.CopyFrom(_chain);
        state.LastSpindle = _spindle;
        fanuc.Unknowns.Spindle = _spindleUnknown;
        fanuc.CssOn = _css;
        fanuc.Unknowns.Css = _cssUnknown;
        fanuc.Rpm.Clear();
        foreach (KeyValuePair<string, decimal> speed in _rpm)
        {
            fanuc.Rpm[speed.Key] = speed.Value;
        }

        fanuc.Unknowns.Speeds = _speedsUnknown;
        state.Preloaded = _preloaded;
        fanuc.Unknowns.Preload = _preloadUnknown;
        fanuc.ControlFeed = _controlFeed;
        fanuc.WrittenFeed = _writtenFeed;
        fanuc.Polar = _polar;
        fanuc.Unknowns.Polar = _polarUnknown;
        fanuc.Tcpm = _tcpm;
        fanuc.VectorTcpm = _vectorTcpm;
        fanuc.Unknowns.Tcpm = _tcpmUnknown;
    }

    private static bool Agree(IReadOnlyList<FanucCallerState> calls, Func<FanucCallerState, string> key)
    {
        string first = key(calls[0]);
        foreach (FanucCallerState call in calls)
        {
            if (key(call) != first)
            {
                return false;
            }
        }

        return true;
    }

    private string GroupKey(int group)
    {
        return _unknownGroups.Contains(group) ? "?" : _groups.TryGetValue(group, out string? code) ? code : "";
    }

    private string CycleKey()
    {
        return _cycleUnknown ? "?" : _cycle?.ToKey() ?? "";
    }

    private string SpindleKey()
    {
        return _spindleUnknown ? "?" : _spindle ?? "";
    }

    private string CssKey()
    {
        return _cssUnknown ? "?" : _css ? "G96" : "G97";
    }

    private string SpeedsKey()
    {
        if (_speedsUnknown)
        {
            return "?";
        }

        var roles = new List<string>(_rpm.Keys);
        roles.Sort(StringComparer.Ordinal);
        var parts = new List<string>(roles.Count);
        foreach (string role in roles)
        {
            parts.Add(role + "=" + _rpm[role].ToString(CultureInfo.InvariantCulture));
        }

        return string.Join("|", parts);
    }

    private string PreloadKey()
    {
        if (_preloadUnknown)
        {
            return "?";
        }

        return _preloaded is ToolRef tool
            ? tool.Number?.ToString(CultureInfo.InvariantCulture) ?? "\"" + tool.Name + "\""
            : "";
    }

    private string FeedKey()
    {
        return (_controlFeed?.ToCanonical() ?? "") + "|" + (_writtenFeed?.ToCanonical() ?? "");
    }

    private string PolarKey()
    {
        return _polarUnknown ? "?" : _polar ? "on" : "off";
    }

    private string TcpmKey()
    {
        return _tcpmUnknown ? "?" : (_tcpm ? "on" : "off") + (_vectorTcpm ? " vector" : "");
    }
}
