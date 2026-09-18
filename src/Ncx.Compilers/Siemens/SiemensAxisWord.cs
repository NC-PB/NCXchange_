using Ncx.Core.Model;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// An axis word of a block: the NCX name of its axis, X for IX, whether it is incremental, and the word.
/// </summary>
internal sealed record SiemensAxisWord(string Axis, bool Incremental, Word Word);
