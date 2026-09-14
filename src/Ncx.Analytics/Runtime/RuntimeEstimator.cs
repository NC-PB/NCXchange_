using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The runtime estimate of virtual machine 8 and D64 as a stream: it reads the events of a run one by one and hands
/// every time it charges, of a motion, a dwell, a cycle dwell or a spindle start or stop, to the analytic that owns it.
/// It keeps nothing but the one motion whose exit speed waits for the next motion, so that it runs over a point list of
/// millions of blocks (implementation 14, risks).
/// </summary>
// TODO(question): virtual machine 8 lets path_mode = "continuous" carry the speed through corners up to corner_speed
// without saying where the path comes to rest. It comes to rest at a dwell, a spindle start or stop, a tool change, a
// STOP, the end of a program and a motion that is not timed; between two timed motions every junction is a corner, a
// block without a motion between them and a reversal of the direction included, until that is answered.
internal sealed class RuntimeEstimator
{
    private const double SecondsPerMinute = 60d;
    private const string SpindlePrefix = "SPINDLE:";
    private const string CycleDwellKey = "CYCLE_DWELL";

    private readonly MachineConfig _machine;
    private readonly RangeFilter _range;
    private readonly Action<TimedStep> _timed;

    private PendingMotion? _pending;
    private CycleCallEvent? _openCycle;
    private bool _openCycleMoved;
    private string? _section;

    /// <summary>
    /// An estimate over one run.
    /// </summary>
    /// <param name="machine">The machine of the run.</param>
    /// <param name="range">The block range of the analytic: the estimate runs over the whole run, so that the motions
    /// at the ends of the range enter and leave with their true speeds, and counts what it cannot time inside the range
    /// only.</param>
    /// <param name="timed">Receives every time charged, in the order the estimate charges it.</param>
    public RuntimeEstimator(MachineConfig machine, RangeFilter range, Action<TimedStep> timed)
    {
        _machine = machine;
        _range = range;
        _timed = timed;
        Dynamics = new MachineDynamics(machine);
    }

    /// <summary>
    /// The values of the machine file the estimate uses.
    /// </summary>
    public MachineDynamics Dynamics { get; }

    /// <summary>
    /// The motions not timed because their length is unknown: an axis not known at both ends in one frame.
    /// </summary>
    public int UnknownLength { get; private set; }

    /// <summary>
    /// The motions not timed because they have no known feed, a feed per revolution while the spindle stands, or a
    /// RAPID, HOME or RETRACT without a rapid rate in the machine file.
    /// </summary>
    public int WithoutSpeed { get; private set; }

    /// <summary>
    /// The cycle calls not timed because the virtual machine does not know their motions: a catalog cycle or a
    /// CYCLE:controller=n (virtual machine 3.3, D94).
    /// </summary>
    public int UnknownCycles { get; private set; }

    /// <summary>
    /// The dwells not timed because their seconds come from an expression STATIC mode does not evaluate.
    /// </summary>
    public int UnknownDwells { get; private set; }

    /// <summary>
    /// The motions that turned a rotary axis, whose travel adds no time (virtual machine 9).
    /// </summary>
    public int RotaryMoves { get; private set; }

    /// <summary>
    /// Reads one event of the run.
    /// </summary>
    public void On(VmEvent vmEvent)
    {
        CloseCycleCall(vmEvent);
        switch (vmEvent)
        {
            case MotionEvent motion:
                AddMotion(motion);
                break;
            case CycleCallEvent cycleCall:
                _openCycle = cycleCall;
                _openCycleMoved = false;
                break;
            case DwellEvent dwell:
                AddDwell(dwell);
                break;
            case StateChangeEvent change:
                AddSpindleChange(change);
                break;
            case SectionEvent section:
                _section = section.Text;
                break;
            case ProgramEvent program:
                Stop();
                _section = program.Phase == EventPhase.Begin ? null : _section;
                break;
            case ToolEvent or StopEvent or FileEvent:
                Stop();
                break;
        }
    }

    /// <summary>
    /// Ends the estimate after the last event: the motion that waits comes to rest.
    /// </summary>
    public void Finish()
    {
        CloseCycleCall(null);
        Stop();
    }

    // Per motion a trapezoidal velocity profile from the machine configuration (virtual machine 8, D64); without
    // [dynamics] distance over feed. A motion whose length is unknown is not timed, and the path comes to rest around
    // it.
    private void AddMotion(MotionEvent motion)
    {
        if (motion.Length is not decimal length)
        {
            UnknownLength += InRange(motion);
            Stop();
            return;
        }

        double millimetres = (double)length * CommandedFeed.MillimetresPerUnit(motion);
        MovingAxes axes = MovingAxes.Of(motion, Dynamics);
        RotaryMoves += axes.TurnsRotary ? InRange(motion) : 0;
        double? speed = SpeedOf(motion, axes);
        if (millimetres > 0 && speed is null)
        {
            WithoutSpeed += InRange(motion);
            Stop();
            return;
        }

        double velocity = speed ?? 0;

        // Without dynamics in the configuration the estimate falls back to distance over feed (virtual machine 8).
        if (!Dynamics.HasProfile)
        {
            Charge(motion, millimetres > 0 ? millimetres / velocity : 0, _section);
            return;
        }

        double entry = 0;
        if (_pending is not null)
        {
            entry = CornerSpeed(_pending, velocity);
            Complete(_pending, entry);
        }

        _pending = new PendingMotion
        {
            Motion = motion,
            Length = millimetres,
            Speed = velocity,
            Entry = entry,
            Acceleration = axes.Acceleration,
            Section = _section,
        };
    }

    // The speed of a motion in mm/s: RAPID and HOME use the rapid rate, the smallest of the moving axes (virtual
    // machine 8; HOME is a MOTION, wave-2 question #25); every other motion the commanded feed, which the profile
    // limits by the max_feed of every axis that moves. The fallback is distance over the feed as commanded.
    // TODO(question): virtual machine 8 names only RAPID and HOME for the rapid rate, and a RETRACT takes no feed of
    // its own (virtual machine 3.1a: "A RETRACT with a feed ... ERROR"), so the F of its MOTION is the active feed of
    // the program. The estimate times RETRACT at the rapid rate of its moving axes with the same profile, as the tool
    // list counts its distance as rapid, until that is answered.
    private double? SpeedOf(MotionEvent motion, MovingAxes axes)
    {
        if (motion.Verb is Verb.Rapid or Verb.Home or Verb.Retract)
        {
            return axes.Rapid / SecondsPerMinute;
        }

        if (CommandedFeed.MillimetresPerMinute(motion, _machine) is not double perMinute || perMinute <= 0)
        {
            return null;
        }

        if (Dynamics.HasProfile && axes.MaxFeed is double maxFeed)
        {
            perMinute = Math.Min(perMinute, maxFeed);
        }

        return perMinute / SecondsPerMinute;
    }

    // path_mode = "exact_stop" brakes to zero in every block, "continuous" carries the speed through corners up to the
    // configured corner_speed (virtual machine 8), and never faster than either motion travels, nor faster than the
    // motion before the corner can reach by its end.
    private double CornerSpeed(PendingMotion before, double nextSpeed)
    {
        if (!Dynamics.Continuous)
        {
            return 0;
        }

        double reachable = before.Acceleration is double acceleration
            ? Math.Sqrt((before.Entry * before.Entry) + (2 * acceleration * before.Length))
            : double.PositiveInfinity;
        return Math.Min(Math.Min(before.Speed, nextSpeed), Math.Min(Dynamics.CornerSpeed, reachable));
    }

    // The time of a motion under its profile; a block can never be shorter than the control's block_time, which is what
    // makes thousands of short segments slow (virtual machine 8).
    private void Complete(PendingMotion motion, double exit)
    {
        double seconds = MotionProfile.Seconds(motion.Length, motion.Speed, motion.Entry, exit, motion.Acceleration);
        if (Dynamics.BlockTime is double blockTime)
        {
            seconds = Math.Max(seconds, blockTime);
        }

        Charge(motion.Motion, seconds, motion.Section);
    }

    // The path comes to rest: the motion that waits ends at speed zero.
    private void Stop()
    {
        if (_pending is not null)
        {
            Complete(_pending, 0);
            _pending = null;
        }
    }

    // Dwells add their times (virtual machine 8; DWELL in seconds, language 4.1).
    private void AddDwell(DwellEvent dwell)
    {
        Stop();
        if (dwell.Seconds is not decimal seconds)
        {
            UnknownDwells += InRange(dwell);
            return;
        }

        Charge(dwell, (double)seconds, _section);
    }

    // A spindle start or stop adds accel_time of that spindle (virtual machine 8; machine-config 5), read from the
    // STATE_CHANGE of SPINDLE (architecture 9); the spindles of generated blocks count as well (virtual machine 3.10,
    // machine-config 5a).
    // TODO(question): machine-config 5 gives accel_time as "seconds from stop to rpm_max" and virtual machine 8 says a
    // start or stop "adds accel_time"; a start to less than rpm_max adds the whole accel_time, and a change from CW to
    // CCW counts as a stop and a start, until that is answered.
    private void AddSpindleChange(StateChangeEvent change)
    {
        if (!change.Variable.StartsWith(SpindlePrefix, StringComparison.Ordinal))
        {
            return;
        }

        int startsAndStops = StartsAndStops(change.OldValue, change.NewValue);
        if (startsAndStops == 0)
        {
            return;
        }

        Stop();
        string spindle = change.Variable.Substring(SpindlePrefix.Length);
        if (Dynamics.AccelTime(spindle) is double accelTime)
        {
            Charge(change, startsAndStops * accelTime, _section);
        }
    }

    // OFF to CW or CCW is a start, CW or CCW to OFF a stop, CW to CCW a stop and a start (language 4.5); a value
    // that is unknown, written empty, is neither.
    private static int StartsAndStops(string oldValue, string newValue)
    {
        bool turned = oldValue is "CW" or "CCW";
        bool turns = newValue is "CW" or "CCW";
        if (oldValue == "OFF" && turns)
        {
            return 1;
        }

        if (turned && newValue == "OFF")
        {
            return 1;
        }

        return turned && turns && oldValue != newValue ? 2 : 0;
    }

    // The motions of an expanded CYCLE_CALL follow it in its block (virtual machine 3.3, D37; wave-2 question #27) and
    // are timed as motions; cycle plunges and dwells add their times (virtual machine 8). The dwell of CYCLE_DWELL is
    // charged once per call, at the depth, and moves nothing (virtual machine 3.3; wave-2 question #11). A call without
    // motions is a cycle whose sequence the virtual machine does not know (virtual machine 3.3, D94): not timed.
    private void CloseCycleCall(VmEvent? next)
    {
        if (_openCycle is null)
        {
            return;
        }

        if (next is MotionEvent && ReferenceEquals(next.After, _openCycle.After))
        {
            _openCycleMoved = true;
            return;
        }

        if (!_openCycleMoved)
        {
            UnknownCycles += InRange(_openCycle);
        }
        else if (_openCycle.Parameters.Count > 0)
        {
            ChargeCycleDwell(_openCycle);
        }

        _openCycle = null;
    }

    private void ChargeCycleDwell(CycleCallEvent cycleCall)
    {
        foreach (Word word in cycleCall.Parameters)
        {
            if (word.Key != CycleDwellKey)
            {
                continue;
            }

            switch (word.Value)
            {
                case DecimalValue seconds:
                    Charge(cycleCall, (double)seconds.Number, _section);
                    break;
                case IntegerValue seconds:
                    Charge(cycleCall, seconds.Number, _section);
                    break;
                default:
                    UnknownDwells += InRange(cycleCall);
                    break;
            }
        }
    }

    private void Charge(VmEvent vmEvent, double seconds, string? section)
    {
        _timed(new TimedStep { Seconds = seconds, Event = vmEvent, Section = section });
    }

    // What the estimate cannot time counts where the block stands in the range of the analytic (virtual machine 8,
    // D67): 1 inside it, 0 outside.
    private int InRange(VmEvent vmEvent)
    {
        return _range.Contains(vmEvent) ? 1 : 0;
    }
}
