using Ncx.Core.Model;

namespace Ncx.Core.Parsing;

/// <summary>
/// What the structure pass finds in the blocks of a file: the programs and subprograms as sections in file order, and
/// the two blocks of the file frame (language 4.1, 4.13; virtual machine 2.1, file.programs and file.subs).
/// </summary>
/// <param name="Sections">The sections in file order.</param>
/// <param name="FileBegin">The FILE=BEGIN block; null when the file has none.</param>
/// <param name="FileEnd">The FILE=END block, the last one when there are several; null when the file has none.</param>
internal sealed record FileStructure(IReadOnlyList<Section> Sections, Block? FileBegin, Block? FileEnd);
