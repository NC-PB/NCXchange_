using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// A way into a label that the text does not run: a JUMP or a REPEAT reaches the label with what the control and the
/// program have at the jump (language 4.9; virtual machine 1, 3.6), and HeidenhainArrivals follows it to the first L
/// block and the first feed motion after the label.
/// </summary>
internal sealed record HeidenhainArrival
{
    /// <summary>
    /// The block of the JUMP or REPEAT, where a way that Klartext cannot write is reported.
    /// </summary>
    public required Block Jump { get; init; }

    /// <summary>
    /// The JUMP or REPEAT word, as the message names it.
    /// </summary>
    public required Word Word { get; init; }

    /// <summary>
    /// The radius compensation the control has on the way, R0, RL or RR, which only an L block switches (controllers
    /// heidenhain.md 2); null once the way has reached its first L block or a motion that runs with another one.
    /// </summary>
    public string? ControlCompensation { get; init; }

    /// <summary>
    /// The radius compensation the program has on the way: the one at the jump, or the COMP a block after the label
    /// states (language 2 rule 2, 4.4).
    /// </summary>
    public required string ProgramCompensation { get; init; }

    /// <summary>
    /// Whether the control has on the way the feed the program has; null once the way has reached its first feed
    /// motion.
    /// </summary>
    public bool? FeedInStep { get; init; }

    /// <summary>
    /// The step after which the program has the feed of the way: the jump, or a block after the label that states F
    /// (language 2 rule 2, 4.3).
    /// </summary>
    public required int FeedStep { get; init; }
}
