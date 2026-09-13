namespace Ncx.Core.Model;

/// <summary>
/// A parsed NCX file: its blocks in file order, the programs and subprograms they form, the comment-only and blank
/// lines kept in place, and what reading it found (architecture 4, language 4.13).
/// </summary>
public sealed record NcxProgram
{
    /// <summary>
    /// The name of the file, which every diagnostic about it carries (D98).
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The line ending of the file, LF or CRLF (language 3, Encoding); null for a program that was never a file, such
    /// as one a reader built, for which the canonical writer takes LF.
    /// </summary>
    public LineEnding? LineEnding { get; init; }

    /// <summary>
    /// Every block of the file in file order. Comment-only and blank lines are not blocks but trivia (language 3,
    /// Block; D92).
    /// </summary>
    public required IReadOnlyList<Block> Blocks { get; init; }

    /// <summary>
    /// The programs and subprograms of the file in file order; between the file frame they stand in any order
    /// (language 4.13).
    /// </summary>
    public IReadOnlyList<Section> Sections { get; init; } = [];

    /// <summary>
    /// The FILE=BEGIN NCX=1 block, the first block of every file; null when the file has none, which the parser
    /// reports (language 4.1).
    /// </summary>
    public Block? FileBegin { get; init; }

    /// <summary>
    /// The FILE=END block, the last block of every file; null when the file has none, which the parser reports
    /// (language 4.1).
    /// </summary>
    public Block? FileEnd { get; init; }

    /// <summary>
    /// The comment-only and blank lines with their line numbers, kept in place and written back as read; the writer
    /// interleaves them with the blocks by line number (D92).
    /// </summary>
    public IReadOnlyList<Trivia> Trivia { get; init; } = [];

    /// <summary>
    /// What reading the file found; a problem the user can cause is a diagnostic, never an exception
    /// (code-guidelines 6, D98).
    /// </summary>
    public required Diagnostics Diagnostics { get; init; }

    /// <summary>
    /// The programs of the file in file order; the first one runs unless the job manifest or the command line names
    /// another (language 4.13).
    /// </summary>
    public IReadOnlyList<Section> Programs => SectionsOf(SectionKind.Program);

    /// <summary>
    /// The subprograms of the file in file order; every program of the file may call them (language 4.13).
    /// </summary>
    public IReadOnlyList<Section> Subs => SectionsOf(SectionKind.Sub);

    private List<Section> SectionsOf(SectionKind kind)
    {
        var sections = new List<Section>();
        foreach (Section section in Sections)
        {
            if (section.Kind == kind)
            {
                sections.Add(section);
            }
        }

        return sections;
    }
}
