namespace Ncx.Readers.Fanuc;

/// <summary>
/// The facts of the reading state that the reader does not know: in a subprogram those its callers leave open, in a
/// caller those the subprogram it called may have changed (virtual machine 3.9; FanucCallerState). A block that
/// depends on one of them stays RAW (D5), a block that sets one makes it known again. The chain of transforms keeps its
/// own (FanucChain.Known).
/// </summary>
internal sealed class FanucUnknowns
{
    /// <summary>
    /// The modal groups whose code is not known (controllers fanuc.md 3).
    /// </summary>
    public HashSet<int> Groups { get; } = [];

    /// <summary>
    /// True while the active cycle is not known (controllers fanuc.md 6).
    /// </summary>
    public bool Cycle { get; set; }

    /// <summary>
    /// True while the spindle a bare S belongs to is not known (controller-mapping 4, the S binding rule).
    /// </summary>
    public bool Spindle { get; set; }

    /// <summary>
    /// True while it is not known whether S is a speed or a cutting speed, G97 or G96 (controllers fanuc.md 4).
    /// </summary>
    public bool Css { get; set; }

    /// <summary>
    /// True while the speeds of the spindles are not known, the S of the pitch of a tapping cycle (controllers
    /// fanuc.md 6).
    /// </summary>
    public bool Speeds { get; set; }

    /// <summary>
    /// True while the preloaded tool is not known (controllers fanuc.md 5).
    /// </summary>
    public bool Preload { get; set; }

    /// <summary>
    /// True while it is not known whether polar interpolation is on, G12.1 (controllers fanuc.md 4).
    /// </summary>
    public bool Polar { get; set; }

    /// <summary>
    /// True while it is not known whether tool center point control is on, G43.4 or G43.5 (controllers fanuc.md 4).
    /// </summary>
    public bool Tcpm { get; set; }

    /// <summary>
    /// Why a block that depends on a fact the reader does not know stays RAW (D5).
    /// </summary>
    /// <param name="fact">The fact, "the code of group 01".</param>
    public static string Reason(string fact)
    {
        return $"the block depends on {fact}, which the reader does not know here: a subprogram runs with the state of "
            + "its caller, and a caller continues with the state its subprogram left (virtual machine 3.9)";
    }

    /// <summary>
    /// Every fact is known: the start of a program.
    /// </summary>
    public void Clear()
    {
        Groups.Clear();
        Cycle = false;
        Spindle = false;
        Css = false;
        Speeds = false;
        Preload = false;
        Polar = false;
        Tcpm = false;
    }
}
