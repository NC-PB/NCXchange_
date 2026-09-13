namespace Ncx.Core.Machine;

/// <summary>
/// [machine] controller: the controller family that selects the reader and the compiler (machine-config 1).
/// </summary>
public enum Controller
{
    /// <summary>
    /// controller = "fanuc".
    /// </summary>
    Fanuc,

    /// <summary>
    /// controller = "heidenhain".
    /// </summary>
    Heidenhain,

    /// <summary>
    /// controller = "siemens".
    /// </summary>
    Siemens,
}
