using Ncx.Core.Machine;

namespace Ncx.Config;

/// <summary>
/// The built-in machine that check, trace, annotate and analyze use when no machine file is given (D103, virtual
/// machine 3.8): one work spindle MAIN with the C axis, one tool holder with the tool spindle TOOL, the axes X Y Z A B
/// C without limits and without reference points, the coolant channel STANDARD, no named functions, units default MM.
/// Convert and compile need a machine file and never use it (D77).
/// </summary>
public static class DefaultMachine
{
    /// <summary>
    /// Builds the default machine of D103.
    /// </summary>
    public static MachineConfig Create()
    {
        // One work spindle MAIN with the C axis, one tool holder with the tool spindle TOOL (D103).
        // TODO(question): D103 names both the tool holder and the tool spindle TOOL, and a role resolves to one
        // resource (virtual machine 3.8 rule 1); TOOL is the tool spindle here, and the holder, which has no role, is
        // the default holder that TOOL and PRELOAD without a role address target (rule 2).
        // TODO(question): D103 does not say which spindle SPINDLE and RPM without a role address target;
        // default_spindle is the tool spindle of the default holder, the spindle that turns the tool of a mill program.
        // TODO(question): machine-config names no key for the arc tolerance of D36 ("the tolerance from the machine
        // configuration", virtual machine 3.2); the default machine sets none, so the D36 defaults, 0.01 mm and
        // 0.0005 in, apply.
        return new MachineConfig
        {
            Machine = new MachineIdentity
            {
                Name = "NCXchange default machine (D103)",
                Channels = [1],
                UnitsDefault = "MM",
                DefaultSpindle = "S2",
                DefaultHolder = "H1",
                DefaultWorkpiece = "S1",
            },
            Roles = new Dictionary<string, string> { ["MAIN"] = "S1", ["TOOL"] = "S2" },
            Resources =
            [
                new ResourceDef { Id = "S1", Type = ResourceType.WorkSpindle, Axis = "C" },
                new ResourceDef { Id = "S2", Type = ResourceType.ToolSpindle },
                new ResourceDef { Id = "H1", Type = ResourceType.ToolHolder, Spindle = "S2" },
            ],

            // The axes X Y Z A B C without limits and without reference points (D103); C is the rotary axis of the
            // work spindle, so that it resolves to that spindle while it holds the workpiece (virtual machine 3.8
            // rule 3).
            Axes =
            [
                new AxisDef { Id = "X", NcxName = "X", Letter = "X", Kind = AxisKind.Linear },
                new AxisDef { Id = "Y", NcxName = "Y", Letter = "Y", Kind = AxisKind.Linear },
                new AxisDef { Id = "Z", NcxName = "Z", Letter = "Z", Kind = AxisKind.Linear },
                new AxisDef { Id = "A", NcxName = "A", Letter = "A", Kind = AxisKind.Rotary },
                new AxisDef { Id = "B", NcxName = "B", Letter = "B", Kind = AxisKind.Rotary },
                new AxisDef { Id = "C", NcxName = "C", Letter = "C", Kind = AxisKind.Rotary, Owner = "S1" },
            ],

            // The coolant channel STANDARD, which a bare COOLANT addresses (virtual machine 2.5), and no named
            // functions (D103); the default machine compiles nothing, so its tables hold no templates.
            Coolant = new Dictionary<string, FunctionTable> { ["STANDARD"] = new FunctionTable() },
        };
    }
}
