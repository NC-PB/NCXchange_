namespace Ncx.Readers.Fanuc;

/// <summary>
/// An open WHILE [ ] DOm loop of custom macro B: its number m and the label of its head (controllers fanuc.md 7).
/// </summary>
/// <param name="Number">The number m of DOm and ENDm, 1 to 3.</param>
/// <param name="Head">The label the reader wrote at the head of the loop, WHILE_12; its end is the head with
/// _END.</param>
internal sealed record FanucLoop(int Number, string Head);
