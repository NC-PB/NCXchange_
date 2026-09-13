namespace Ncx.Core.Machine;

/// <summary>
/// [sync] paths: how the Fanuc wait code names the waiting paths in {paths} (machine-config 5).
/// </summary>
public enum SyncPaths
{
    /// <summary>
    /// paths = "list": P12, P13, P123 (parameter 8103#1 = 1).
    /// </summary>
    List,

    /// <summary>
    /// paths = "bitmask": P3, P5, P6, P7 (parameter 8103#1 = 0).
    /// </summary>
    Bitmask,
}
