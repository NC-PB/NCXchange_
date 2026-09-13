namespace Ncx.Core.Machine;

/// <summary>
/// [machine]: which machine, which controller and dialect, and the default resources that NCX words without a role
/// address resolve to (machine-config 1).
/// </summary>
public sealed record MachineIdentity
{
    /// <summary>
    /// name: the machine as the shop calls it, "DMU 50".
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// controller: selects the reader and the compiler (machine-config 1). Null only on the built-in default machine
    /// of D103, which serves no reader and no compiler (D77).
    /// </summary>
    public Controller? Controller { get; init; }

    /// <summary>
    /// dialect: selects the variant tables of reader and compiler, "iTNC530", "31i-B"; null when the file leaves it
    /// out.
    /// </summary>
    public string? Dialect { get; init; }

    /// <summary>
    /// builder: the machine builder id used by RAW:DMG and by the builder function tables, "dmg"; null when the file
    /// leaves it out.
    /// </summary>
    public string? Builder { get; init; }

    /// <summary>
    /// gcode_system: A, B or C, for Fanuc lathes only; null when the file leaves it out.
    /// </summary>
    public GcodeSystem? GcodeSystem { get; init; }

    /// <summary>
    /// s_binds_to_spindle_word: Fanuc has one S word, owned by the spindle of the M code in the same block; false when
    /// the file leaves it out.
    /// </summary>
    public bool SBindsToSpindleWord { get; init; }

    /// <summary>
    /// channels: the channel numbers of the machine, [1, 2]; empty when the file leaves it out.
    /// </summary>
    public IReadOnlyList<int> Channels { get; init; } = [];

    /// <summary>
    /// units_default: MM or INCH, a value of the UNITS word (language 4.2); null when the file leaves it out.
    /// </summary>
    // TODO: UnitsDefault is kept as the word MM or INCH; it takes the units enumeration of the channel state once the
    // virtual machine defines it (P1-01, code-guidelines 7).
    public string? UnitsDefault { get; init; }

    /// <summary>
    /// default_spindle: the resource id that SPINDLE, RPM and the other spindle words without a role address target
    /// (virtual machine 3.8 rule 2); null when the file leaves it out.
    /// </summary>
    public string? DefaultSpindle { get; init; }

    /// <summary>
    /// default_holder: the resource id that TOOL and PRELOAD without a role address target (virtual machine 3.8 rule
    /// 2); null when the file leaves it out.
    /// </summary>
    public string? DefaultHolder { get; init; }

    /// <summary>
    /// default_workpiece: the resource id of the workpiece holder at program start (virtual machine 2.1); null when the
    /// file leaves it out.
    /// </summary>
    public string? DefaultWorkpiece { get; init; }
}
