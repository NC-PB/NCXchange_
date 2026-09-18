using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// An axis word of a block: the word, the NCX name of its axis, and whether it is the incremental form, IX of X
/// (language 4.3).
/// </summary>
/// <param name="Word">The word of the block.</param>
/// <param name="Axis">The NCX name of the axis: X, Z2.</param>
/// <param name="Incremental">True for IX, IZ2.</param>
internal sealed record HeidenhainAxisWord(Word Word, string Axis, bool Incremental);
