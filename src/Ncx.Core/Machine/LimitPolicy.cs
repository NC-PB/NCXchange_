namespace Ncx.Core.Machine;

/// <summary>
/// [machine] limits: what the expander does with RPM, F and targets beyond the machine limits (machine-config 1, D64).
/// </summary>
public enum LimitPolicy
{
    /// <summary>
    /// limits = "warn": a WARNING, the value stays; the default (D64).
    /// </summary>
    Warn,

    /// <summary>
    /// limits = "clamp": the expander rewrites the value to the limit and the WARNING says so.
    /// </summary>
    Clamp,
}
