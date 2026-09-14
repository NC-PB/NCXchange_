using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// One SINUMERIK block while it is read: its words, which of them are read, and everything its reading needs, the
/// source-side state, the facts of the file, the machine with its templates, the diagnostics and the draft of the NCX
/// blocks it reads into. Every word a concern reads is marked, and a word no concern reads keeps the block as RAW (D5).
/// </summary>
internal sealed class SiemensBlock
{
    private readonly bool[] _read;
    private readonly SiemensTokenizer _tokenizer;

    /// <summary>
    /// Starts the reading of a source block.
    /// </summary>
    public SiemensBlock(SourceBlock source, SiemensTokenizer tokenizer, SiemensState siemens, MachineConfig machine,
        TemplateSet templates, Diagnostics diagnostics)
    {
        Source = source;
        _tokenizer = tokenizer;
        Siemens = siemens;
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
    /// The words of the block as text, without its skip, its number, its label and its comment.
    /// </summary>
    public string Content => _tokenizer.ContentOf(Source);

    /// <summary>
    /// The facts of the file.
    /// </summary>
    public SiemensState Siemens { get; }

    /// <summary>
    /// The facts of the modal state where the block stands.
    /// </summary>
    public SiemensFacts Facts => Siemens.Facts;

    /// <summary>
    /// The source-side state (architecture 7).
    /// </summary>
    public SourceState State => Siemens.Source;

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
    public SiemensDraft Draft { get; } = new();

    /// <summary>
    /// The line of the block.
    /// </summary>
    public int Line => Source.Line;

    /// <summary>
    /// The unit the block stands in.
    /// </summary>
    public SiemensUnit? Unit => Siemens.Unit;

    /// <summary>
    /// The G53, G153 or SUPA of the block, whose coordinates then refer to the machine datum for this block
    /// (controllers siemens.md 3, group 9); null for a block without one.
    /// </summary>
    public SourceWord? MachineFrame { get; set; }

    /// <summary>
    /// The subprograms of the file the block calls, by name, with the passes of each call (virtual machine 3.9).
    /// </summary>
    public List<KeyValuePair<string, int>> Calls { get; } = [];

    /// <summary>
    /// True when the block calls a program outside the file, which the reader does not follow (virtual machine 3.9).
    /// </summary>
    public bool CallsOutside { get; set; }

    /// <summary>
    /// The NAME of the SUB section the structure pass made of the block range a REPEAT of the block names, which the
    /// REPEAT calls (controller-mapping 6, REPEAT + TIMES); null for a block without one.
    /// </summary>
    public string? RepeatSection { get; init; }

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
    /// The first unread code with its number, G1, M8, compared by number, G01 is G1 (D105); the address of one letter,
    /// so that M2=3 is no M23. Null when there is none.
    /// </summary>
    /// <param name="code">The code without leading zeros, "G90".</param>
    public SourceWord? FindCode(string code)
    {
        for (int index = 0; index < _read.Length; index++)
        {
            SourceWord word = Source.Words[index];
            if (!_read[index] && word.Address.Length == 1 && NativeCode.Of(word) == code)
            {
                return word;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads this code; false when the block does not hold it unread.
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
    /// The code of a word of one letter with its number, "G1" of G01; null for another word.
    /// </summary>
    /// <param name="word">A word of the block.</param>
    public static string? CodeOf(SourceWord word)
    {
        return word.Address.Length == 1 ? NativeCode.Of(word) : null;
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
    /// Marks every word of the block as read, for a block whose whole content a concern has read.
    /// </summary>
    public void MarkAllRead()
    {
        for (int index = 0; index < _read.Length; index++)
        {
            _read[index] = true;
        }
    }

    /// <summary>
    /// True when the word is read.
    /// </summary>
    /// <param name="word">A word of the block.</param>
    public bool IsRead(SourceWord word)
    {
        int index = IndexOf(word);
        return index >= 0 && _read[index];
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
    /// The word as the source writes it, "X=AC(10)", "G641".
    /// </summary>
    /// <param name="word">A word of the block.</param>
    public string SpanOf(SourceWord word)
    {
        return _tokenizer.SpanOf(Source, IndexOf(word));
    }

    /// <summary>
    /// Keeps a word that NCX has no meaning for as part of the RAW:SIEMENS word of the block, verbatim (controllers
    /// siemens.md 2, 11 rule 8; D5).
    /// </summary>
    /// <param name="word">A word of the block.</param>
    public void KeepAsRawWord(SourceWord word)
    {
        MarkRead(word);
        Draft.RawWords.Add(SpanOf(word));
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
