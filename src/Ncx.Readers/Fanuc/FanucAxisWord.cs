namespace Ncx.Readers.Fanuc;

/// <summary>
/// A source word that names an axis: the word, the NCX axis it moves and whether its value is incremental.
/// </summary>
/// <param name="Word">The source word, X50.4 or U5.</param>
/// <param name="Axis">The NCX axis name, "X".</param>
/// <param name="Incremental">True for an incremental value, under G91 or by an incremental address of system A.</param>
internal sealed record FanucAxisWord(SourceWord Word, string Axis, bool Incremental)
{
    /// <summary>
    /// The NCX key of the word, X or IX (language 4.3).
    /// </summary>
    public string Key => Incremental ? "I" + Axis : Axis;
}
