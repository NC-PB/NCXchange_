using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// One channel of a job while the job compiler writes it (implementation 16, P6-02): its program in its expanded file,
/// the subprograms its walk calls, the marks it passes, and the blocks the job compiler inserts or the words it takes
/// out, from which the channel program the compiler of the family writes is built (D56; language 4.13, 4.14).
/// </summary>
internal sealed class JobChannel
{
    // The blocks inserted before or after a block of the file, by the index of that block, in their order.
    private readonly Dictionary<int, List<Block>> _before = [];
    private readonly Dictionary<int, List<Block>> _after = [];

    // The words taken out of a block of the file, by the index of that block.
    private readonly Dictionary<int, List<Word>> _removed = [];

    /// <summary>
    /// The channel, the id of the [[channel]] of the manifest (machine-config 8).
    /// </summary>
    public required int Channel { get; init; }

    /// <summary>
    /// The NCX file the program of the channel stands in, parsed and expanded with the rules of the machine and the
    /// rewriters of the plugins (architecture 5.5): the blocks the expansion generated stand in it like the rest.
    /// </summary>
    public required NcxProgram File { get; init; }

    /// <summary>
    /// The program the job runs on the channel: the one the manifest names, the first of the file without one (D48).
    /// </summary>
    public required Section Program { get; init; }

    /// <summary>
    /// The diagnostics of the file of the channel that the program of the channel starts with: the parser's and the
    /// expander's for the first channel of the file, none for a second channel that runs another program of it.
    /// </summary>
    public required Diagnostics FileDiagnostics { get; init; }

    /// <summary>
    /// What the job compiler reports on the blocks of the channel's file (D98).
    /// </summary>
    public required Diagnostics Diagnostics { get; init; }

    /// <summary>
    /// The marks the channel passed on the STATIC run of the job, the mark every block of its program stands behind,
    /// and the subprograms its walk entered (virtual machine 3.7, 3.9).
    /// </summary>
    public required ChannelMarks Marks { get; init; }

    /// <summary>
    /// Inserts a block before a block of the file, after those inserted there before.
    /// </summary>
    public void InsertBefore(int index, Block block)
    {
        Add(_before, index, block);
    }

    /// <summary>
    /// Inserts a block after a block of the file, after those inserted there before.
    /// </summary>
    public void InsertAfter(int index, Block block)
    {
        Add(_after, index, block);
    }

    /// <summary>
    /// Takes words out of a block of the file.
    /// </summary>
    public void Remove(int index, IReadOnlyList<Word> words)
    {
        if (!_removed.TryGetValue(index, out List<Word>? removed))
        {
            removed = [];
            _removed.Add(index, removed);
        }

        removed.AddRange(words);
    }

    /// <summary>
    /// The channel program as the compiler of the family writes it (language 4.13, 4.14): FILE=BEGIN, the program of
    /// the channel with the blocks the job compiler inserted and without the words it took out, the subprograms the
    /// program calls, FILE=END; every section on the channel of the job, expanded already. A block whose words are all
    /// taken out is left out. The program reports on diagnostics of its own, which start with FileDiagnostics.
    /// </summary>
    public NcxProgram Build()
    {
        Block fileBegin = File.FileBegin ?? throw NoFrame();
        Block fileEnd = File.FileEnd ?? throw NoFrame();
        var blocks = new List<Block> { fileBegin };
        var sections = new List<Section> { AddSection(Program, blocks) };
        foreach (Section sub in Marks.Subs)
        {
            sections.Add(AddSection(sub, blocks));
        }

        blocks.Add(fileEnd);
        var diagnostics = new Diagnostics(File.FileName);
        foreach (Diagnostic diagnostic in FileDiagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        return File with { Blocks = blocks, Sections = sections, Diagnostics = diagnostics };
    }

    // The blocks of one section with the insertions, and the section around them on the channel of the job.
    private Section AddSection(Section section, List<Block> blocks)
    {
        int first = blocks.Count;
        int last = first;
        for (int index = section.FirstBlock; index <= section.LastBlock; index++)
        {
            AddAll(blocks, _before, index);
            if (Remaining(index) is Block block)
            {
                last = blocks.Count;
                blocks.Add(block);
            }

            AddAll(blocks, _after, index);
        }

        return section with { FirstBlock = first, LastBlock = last, Channel = Channel };
    }

    // The block without the words taken out of it; null when nothing but SKIP remains.
    private Block? Remaining(int index)
    {
        Block block = File.Blocks[index];
        if (!_removed.TryGetValue(index, out List<Word>? removed))
        {
            return block;
        }

        var words = new List<Word>();
        foreach (Word word in block.Words)
        {
            if (!removed.Contains(word))
            {
                words.Add(word);
            }
        }

        bool empty = words.Count == 0 || (words.Count == 1 && words[0].Key == "SKIP");
        return empty ? null : block with { Words = words };
    }

    private static InvalidOperationException NoFrame()
    {
        return new InvalidOperationException("A file that parses without ERROR has FILE=BEGIN and FILE=END.");
    }

    private static void Add(Dictionary<int, List<Block>> inserted, int index, Block block)
    {
        if (!inserted.TryGetValue(index, out List<Block>? blocks))
        {
            blocks = [];
            inserted.Add(index, blocks);
        }

        blocks.Add(block);
    }

    private static void AddAll(List<Block> blocks, Dictionary<int, List<Block>> inserted, int index)
    {
        if (inserted.TryGetValue(index, out List<Block>? insertedBlocks))
        {
            blocks.AddRange(insertedBlocks);
        }
    }
}
