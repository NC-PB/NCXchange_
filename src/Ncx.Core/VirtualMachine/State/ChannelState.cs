using System.Collections.ObjectModel;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The state of one channel, every table of virtual machine 2 as one class each, with the start values the tables
/// give. Mutable; only the virtual machine changes it, and <see cref="Snapshot"/> gives the immutable Before and After
/// of every event (code-guidelines 7, architecture 5).
/// </summary>
internal sealed class ChannelState
{
    // The coolant channel that a bare COOLANT addresses (virtual machine 2.5).
    private const string DefaultCoolantChannel = "STANDARD";

    /// <summary>
    /// The state of a channel at the start of a run, from the machine file, or from the built-in default machine of
    /// D103 that the caller passes when no machine file is given.
    /// </summary>
    /// <param name="machine">The machine configuration the state starts from.</param>
    /// <param name="channelId">1, or the CHANNEL of the program the channel runs (virtual machine 2.8).</param>
    /// <param name="startValues">The start values of the variables from &lt;file&gt;.vars.toml; null without
    /// one.</param>
    public ChannelState(MachineConfig machine, int channelId = 1,
        IReadOnlyDictionary<string, Value>? startValues = null)
    {
        // channel.id is 1 or CHANNEL; waitingAt starts none and finished false (virtual machine 2.8).
        ChannelId = channelId;
        WaitingAt = null;
        Finished = false;

        Program = new ProgramState();
        Frame = new FrameState(machine);
        Motion = new MotionState(machine);
        Cycle = new CycleState(Frame.Workplane);
        Flow = new FlowState();

        // vars start empty or from <file>.vars.toml (virtual machine 2.7, 3.6).
        Vars = new VariableStore(this, machine, startValues ?? new Dictionary<string, Value>());

        // The resources come from TOML (virtual machine 2.8): every work and tool spindle gets the spindle rows of 2.4,
        // every tool holder the tool rows of 2.3, by resource id.
        foreach (ResourceDef resource in machine.Resources)
        {
            if (resource.IsSpindle)
            {
                Spindles[resource.Id] = new SpindleState();
            }
            else if (resource.Type == ResourceType.ToolHolder)
            {
                Holders[resource.Id] = new HolderState();
            }
        }

        // lastHolder starts at default_holder from TOML (virtual machine 2.3), or at the only holder of a machine that
        // has one and names no default (3.8 rule 2).
        LastHolder = machine.ResolveDefaultHolder()?.Id;

        // Every coolant channel starts OFF, the channel STANDARD among them, the default channel a bare COOLANT
        // addresses (virtual machine 2.5).
        Coolant[DefaultCoolantChannel] = false;
        foreach (string channel in machine.Coolant.Keys)
        {
            Coolant[channel] = false;
        }

        // The named functions come from TOML (virtual machine 2.5).
        // TODO(question): virtual machine 2.5 gives function[name] the start value "from TOML", and machine-config 5
        // names the states of a function but no start state; every function starts without a state until FUNC sets
        // one.
        foreach (string function in machine.Functions.Keys)
        {
            Functions[function] = null;
        }
    }

    /// <summary>
    /// channel.id: 1 or CHANNEL (virtual machine 2.8).
    /// </summary>
    public int ChannelId { get; set; }

    public ProgramState Program { get; }

    public FrameState Frame { get; }

    public MotionState Motion { get; }

    public CycleState Cycle { get; }

    public FlowState Flow { get; }

    public VariableStore Vars { get; }

    /// <summary>
    /// The state of each spindle by its resource id (virtual machine 2.4).
    /// </summary>
    public Dictionary<string, SpindleState> Spindles { get; } = new();

    /// <summary>
    /// The state of each tool holder by its resource id (virtual machine 2.3).
    /// </summary>
    public Dictionary<string, HolderState> Holders { get; } = new();

    /// <summary>
    /// lastHolder: the resource id of the holder of the last TOOL (virtual machine 2.3); null on a machine without a
    /// holder.
    /// </summary>
    public string? LastHolder { get; set; }

    /// <summary>
    /// coolant: on or off for each coolant channel by its name (virtual machine 2.5).
    /// </summary>
    public Dictionary<string, bool> Coolant { get; } = new();

    /// <summary>
    /// function: the state of each named function by its name; null while the program has set none (virtual machine
    /// 2.5).
    /// </summary>
    public Dictionary<string, string?> Functions { get; } = new();

    /// <summary>
    /// channel.waitingAt: the SYNC mark the channel waits at; null for none (virtual machine 2.8).
    /// </summary>
    public int? WaitingAt { get; set; }

    /// <summary>
    /// channel.finished (virtual machine 2.8).
    /// </summary>
    public bool Finished { get; set; }

    /// <summary>
    /// The state variables that are UNKNOWN although their row holds a value: set from an expression in STATIC mode
    /// (virtual machine 1), or the setpos shift of an axis that SETPOS declared directly after a HOME without a
    /// reference point (D101). Each is named by the key that sets it and, for a variable per resource or per axis, the
    /// resource id or the axis name as its address, after the state keys of virtual machine 3.10: F, RPM:S1, SETPOS:C.
    /// </summary>
    public HashSet<string> Unknown { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// An immutable deep copy of the channel as it is now: the Before or the After of an event (virtual machine 7,
    /// architecture 5.3).
    /// </summary>
    public ChannelSnapshot Snapshot()
    {
        // Every table is copied as it is now, so that the snapshot stays as it was while the virtual machine goes on
        // (code-guidelines 7).
        var spindles = new Dictionary<string, SpindleSnapshot>();
        foreach (KeyValuePair<string, SpindleState> spindle in Spindles)
        {
            spindles[spindle.Key] = spindle.Value.Snapshot();
        }

        var holders = new Dictionary<string, HolderSnapshot>();
        foreach (KeyValuePair<string, HolderState> holder in Holders)
        {
            holders[holder.Key] = holder.Value.Snapshot();
        }

        return new ChannelSnapshot
        {
            ChannelId = ChannelId,
            Program = Program.Snapshot(),
            Frame = Frame.Snapshot(),
            Motion = Motion.Snapshot(),
            Cycle = Cycle.Snapshot(),
            Flow = Flow.Snapshot(),
            Vars = Vars.Snapshot(),
            Spindles = spindles.AsReadOnly(),
            Holders = holders.AsReadOnly(),
            LastHolder = LastHolder,
            Coolant = new Dictionary<string, bool>(Coolant).AsReadOnly(),
            Functions = new Dictionary<string, string?>(Functions).AsReadOnly(),
            WaitingAt = WaitingAt,
            Finished = Finished,
            Unknown = new ReadOnlySet<string>(new HashSet<string>(Unknown, StringComparer.Ordinal)),
        };
    }
}
