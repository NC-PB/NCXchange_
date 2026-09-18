using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// An axis word of an NCX block: the axis by its NCX name, whether the word is incremental, and the word (language
/// 4.3, X and IX).
/// </summary>
/// <param name="Axis">The NCX name of the axis, X, Z2.</param>
/// <param name="Incremental">True for IX, IZ2.</param>
/// <param name="Word">The word of the block.</param>
internal sealed record FanucAxisWord(string Axis, bool Incremental, Word Word);
