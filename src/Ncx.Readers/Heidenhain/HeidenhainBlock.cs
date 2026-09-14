using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// One Klartext block while it is read: its words, which of them are read, and everything its reading needs, the
/// source-side state, the facts of the file, the machine with its templates, the diagnostics and the draft of the NCX
/// blocks it reads into. Every word a concern reads is marked, and a word no concern reads keeps the block as RAW (D5).
/// </summary>
internal sealed class HeidenhainBlock
{
    private readonly bool[] _read;

    /// <summary>
    /// Starts the reading of a source block.
    /// </summary>
    public HeidenhainBlock(SourceBlock source, string content, SourceState state, HeidenhainState heidenhain,
        MachineConfig machine, TemplateSet templates, Diagnostics diagnostics)
    {
        Source = source;
        Content = content;
        State = state;
        Heidenhain = heidenhain;
        Machine = machine;
        Templates = templates;
        Diagnostics = diagnostics;
        _read = new bool[source.Words.Count];
    }

    /// <summary>
    /// The source block.
    /// </summary>
    public SourceBlock Source { get; }

    /// <summary>
    /// The words of the block as text, without the block number, the skip and the comments.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// The source-side state (architecture 7).
    /// </summary>
    public SourceState State { get; }

    /// <summary>
    /// The facts of the file.
    /// </summary>
    public HeidenhainState Heidenhain { get; }

    /// <summary>
    /// The machine the file was written for.
    /// </summary>
    public MachineConfig Machine { get; }

    /// <summary>
    /// The templates of the machine, which its function tables are matched through (architecture 6).
    /// </summary>
    public TemplateSet Templates { get; }

    /// <summary>
    /// The diagnostics of the file.
    /// </summary>
    public Diagnostics Diagnostics { get; }

    /// <summary>
    /// The NCX blocks the block reads into.
    /// </summary>
    public HeidenhainDraft Draft { get; } = new();

    /// <summary>
    /// The line of the block.
    /// </summary>
    public int Line => Source.Line;

    /// <summary>
    /// The address of a word by its place, "" past the last word: the first words say what the block is, L, CYCL DEF,
    /// TOOL CALL (controllers heidenhain.md 7 rule 1).
    /// </summary>
    /// <param name="index">The place of the word, 0 for the first.</param>
    public string Keyword(int index)
    {
        return index < Source.Words.Count ? Source.Words[index].Address : "";
    }

    /// <summary>
    /// The first unread word with this address; null when there is none.
    /// </summary>
    /// <param name="address">The address, "X".</param>
    public SourceWord? Find(string address)
    {
        for (int index = 0; index < _read.Length; index++)
        {
            if (!_read[index] && Source.Words[index].Address == address)
            {
                return Source.Words[index];
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the first unread word with this address; null when there is none.
    /// </summary>
    /// <param name="address">The address, "F".</param>
    public SourceWord? Take(string address)
    {
        SourceWord? word = Find(address);
        if (word is not null)
        {
            MarkRead(word);
        }

        return word;
    }

    /// <summary>
    /// The first unread M word of this code, compared by number, M08 is M8 (D105); null when there is none.
    /// </summary>
    /// <param name="code">The code without leading zeros, "M91".</param>
    public SourceWord? FindCode(string code)
    {
        for (int index = 0; index < _read.Length; index++)
        {
            SourceWord word = Source.Words[index];
            if (!_read[index] && word.Address == "M" && NativeCode.Of(word) == code)
            {
                return word;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads this M code; false when the block does not hold it unread.
    /// </summary>
    /// <param name="code">The code without leading zeros.</param>
    public bool TakeCode(string code)
    {
        SourceWord? word = FindCode(code);
        if (word is null)
        {
            return false;
        }

        MarkRead(word);
        return true;
    }

    /// <summary>
    /// Marks a word of the block as read.
    /// </summary>
    /// <param name="word">A word of the block.</param>
    public void MarkRead(SourceWord word)
    {
        int index = IndexOf(word);
        if (index >= 0)
        {
            _read[index] = true;
        }
    }

    /// <summary>
    /// Marks the first words of the block as read, the words that say what it is: TOOL CALL, CYCL DEF.
    /// </summary>
    /// <param name="count">How many words.</param>
    public void MarkLeading(int count)
    {
        for (int index = 0; index < count && index < _read.Length; index++)
        {
            _read[index] = true;
        }
    }

    /// <summary>
    /// Marks every word of the block as read, for a block whose whole content a concern has read.
    /// </summary>
    public void MarkAllRead()
    {
        MarkLeading(_read.Length);
    }

    /// <summary>
    /// The words no concern has read yet, in source order.
    /// </summary>
    public List<SourceWord> Unread()
    {
        var unread = new List<SourceWord>();
        for (int index = 0; index < _read.Length; index++)
        {
            if (!_read[index])
            {
                unread.Add(Source.Words[index]);
            }
        }

        return unread;
    }

    /// <summary>
    /// The place of a word among the words of the block; -1 for a word of another block.
    /// </summary>
    /// <param name="word">A word.</param>
    public int IndexOf(SourceWord word)
    {
        for (int index = 0; index < Source.Words.Count; index++)
        {
            if (ReferenceEquals(Source.Words[index], word))
            {
                return index;
            }
        }

        return -1;
    }
}
