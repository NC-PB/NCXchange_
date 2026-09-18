namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// What a label found on the way the text runs into it: whether the control has there the radius compensation and the
/// feed of the program. Where it has, the first L block and the first feed motion after the label write them only where
/// a block from the label on states them; where it has not, they write the values of that way (HeidenhainArrivals).
/// The chain words and SETPOS after the label are written for the frame of that way, with the cycle 7 of SETPOS at the
/// place the control has there (HeidenhainChainArrivals).
/// </summary>
/// <param name="CompensationInStep">True where the control has the compensation of the program.</param>
/// <param name="FeedInStep">True where the control has the feed of the program.</param>
/// <param name="SetposPlace">The place of the cycle 7 of SETPOS the control has (HeidenhainSetpos.PlaceKey); null
/// where the target state does not know it.</param>
internal sealed record HeidenhainLabelWay(bool CompensationInStep, bool FeedInStep, string? SetposPlace);
