namespace Ncx.Core.Machine;

// TODO(question): the documents say the family is "defined in the same way inside NCXchange" (machine-config 6) and
// "defined in code" (phase 2, P2-03), not where. It lives with the records of the machine model in Ncx.Core, so that
// the virtual machine, which runs the built-in family itself (virtual machine 3.3), and the loader in Ncx.Config both
// reach it.

/// <summary>
/// The built-in drilling family of language 4.7, DRILL, DRILL_DWELL, PECK, CHIP_BREAK, TAP, REAM and BORE, defined in
/// code in the same way as a catalog entry, per controller family as controller-mapping 5 maps it. The catalog file of
/// the family and the [[cycle]] entries of a machine file override it (machine-config 6). One part per family:
/// DrillingFamily.Fanuc.cs, DrillingFamily.Heidenhain.cs, DrillingFamily.Siemens.cs.
/// </summary>
public static partial class DrillingFamily
{
    /// <summary>
    /// The built-in names in the order language 4.7 lists them.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } =
        ["DRILL", "DRILL_DWELL", "PECK", "CHIP_BREAK", "TAP", "REAM", "BORE"];

    /// <summary>
    /// The built-in family of a controller family as a catalog, one entry per built-in name in the order of Names
    /// (machine-config 6, controller-mapping 5).
    /// </summary>
    /// <param name="controller">The controller family of the machine.</param>
    public static CycleCatalog Catalog(Controller controller)
    {
        return controller switch
        {
            Controller.Fanuc => FanucFamily(),
            Controller.Heidenhain => HeidenhainFamily(),
            Controller.Siemens => SiemensFamily(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(controller), controller, "Not a controller family of machine-config 1."),
        };
    }
}
