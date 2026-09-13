namespace Ncx.Core.Geometry;

/// <summary>
/// What resolving an ARC gives: the arc, or why it does not resolve. A problem is a value here, never a diagnostic and
/// never an exception; the virtual machine turns it into the diagnostic with its VM code (code-guidelines 6).
/// </summary>
internal sealed record ArcResult
{
    private ArcResult(Arc? arc, ArcError? error)
    {
        Arc = arc;
        Error = error;
    }

    /// <summary>
    /// The resolved arc; null when the arc does not resolve.
    /// </summary>
    public Arc? Arc { get; }

    /// <summary>
    /// Why the arc does not resolve; null when it resolves.
    /// </summary>
    public ArcError? Error { get; }

    public static ArcResult Resolved(Arc arc)
    {
        return new ArcResult(arc, null);
    }

    public static ArcResult Failed(ArcError error)
    {
        return new ArcResult(null, error);
    }
}
