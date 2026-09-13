using System.Globalization;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// A call of a program in a block (controllers fanuc.md 1, 7; controller-mapping 6): M98 P, G65 P and, on a machine of
/// the builder nakamura, M200 P call a program of the file or an external one, M198 P an external program.
/// </summary>
/// <param name="Program">The number of the called program, 100 of M98 P0100.</param>
/// <param name="Repeats">True when the call runs the program more than once, or a number of times the reader does not
/// know.</param>
/// <param name="External">True for M198, the call of a program from external memory.</param>
internal sealed record FanucCall(long Program, bool Repeats, bool External)
{
    /// <summary>
    /// The name of the section the call enters, the number without leading zeros (controller-mapping 6).
    /// </summary>
    public string Name => Program.ToString(CultureInfo.InvariantCulture);
}
