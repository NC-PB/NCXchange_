namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The value of one modal quantity of NCX in a state of the STATIC walk, as the Fanuc compiler writes it under a key of
/// the target state: F100., G94, G17, the D register, S1000. None where NCX has no value, the feed before the first
/// F; not known where an expression set it, which the STATIC walk does not evaluate (virtual machine 1).
/// </summary>
/// <param name="Text">The value as written; null for none and for a value that is not known.</param>
/// <param name="IsKnown">False for a value set from an expression.</param>
internal readonly record struct FanucModalValue(string? Text, bool IsKnown)
{
    /// <summary>
    /// No value in NCX: nothing the control must hold.
    /// </summary>
    public static FanucModalValue None => new(null, true);

    /// <summary>
    /// A value set from an expression, which the STATIC walk does not evaluate (virtual machine 1).
    /// </summary>
    public static FanucModalValue NotKnown => new(null, false);

    /// <summary>
    /// A value as written.
    /// </summary>
    public static FanucModalValue Of(string text)
    {
        return new FanucModalValue(text, true);
    }
}
