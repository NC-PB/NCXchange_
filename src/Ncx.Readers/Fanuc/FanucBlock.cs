using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// One Fanuc source block while it is read: its words, which of them are read, and everything its reading needs, the
/// source-side state, the facts of the file, the machine with its templates, the diagnostics and the draft of the NCX
/// blocks it reads into. Every word a concern reads is marked, and a word no concern reads keeps the block as RAW
/// (D5).
/// </summary>
internal sealed class FanucBlock
{
    private readonly bool[] _read;

    /// <summary>
    /// Starts the reading of a source block.
    /// </summary>
    public FanucBlock(SourceBlock source, SourceState state, FanucState fanuc, MachineConfig machine,
        TemplateSet templates, Diagnostics diagnostics)
    {
        Source = source;
        State = state;
        Fanuc = fanuc;
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
    /// The source-side state after the modal codes of the block (controllers fanuc.md 3).
    /// </summary>
    public SourceState State { get; }

    /// <summary>
    /// The facts of the file.
    /// </summary>
    public FanucState Fanuc { get; }

    /// <summary>
    /// The machine the file was written for.
    /// </summary>
    public MachineConfig Machine { get; }

    /// <summary>
    /// The templates of the machine, matched against the source (architecture 6).
    /// </summary>
    public TemplateSet Templates { get; }

    /// <summary>
    /// The diagnostics of the file.
    /// </summary>
    public Diagnostics Diagnostics { get; }

    /// <summary>
    /// The NCX blocks the block reads into.
    /// </summary>
    public FanucDraft Draft { get; } = new();

    /// <summary>
    /// True when G53 stands in the block: its coordinates are machine coordinates, FRAME=MACHINE (controllers
    /// fanuc.md 4).
    /// </summary>
    public bool MachineFrame { get; set; }

    /// <summary>
    /// The line of the block.
    /// </summary>
    public int Line => Source.Line;

    /// <summary>
    /// The G-code system of a lathe; null on a mill (machine-config 1).
    /// </summary>
    public GcodeSystem? System => Machine.Machine.GcodeSystem;

    /// <summary>
    /// True on a lathe, a machine with a G-code system (machine-config 1).
    /// </summary>
    public bool IsLathe => System is not null;

    /// <summary>
    /// True while G91 makes the axis words incremental (controllers fanuc.md 3, group 03).
    /// </summary>
    public bool Incremental => State.ActiveCode(FanucModalGroups.Distance) == "G91";

    /// <summary>
    /// The first unread word with this address; null when there is none.
    /// </summary>
    /// <param name="address">The address, "T".</param>
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
    /// <param name="address">The address, "T".</param>
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
    /// The first unread G or M word of this code, compared by number, M08 is M8 (D105); null when there is none.
    /// </summary>
    /// <param name="code">The code without leading zeros, "M8", "G5.1".</param>
    public SourceWord? FindCode(string code)
    {
        for (int index = 0; index < _read.Length; index++)
        {
            SourceWord word = Source.Words[index];
            if (!_read[index] && word.Address is "G" or "M" && NativeCode.Of(word) == code)
            {
                return word;
            }
        }

        return null;
    }

    /// <summary>
    /// Tells whether the block holds this G or M code unread.
    /// </summary>
    /// <param name="code">The code without leading zeros.</param>
    public bool HasCode(string code)
    {
        return FindCode(code) is not null;
    }

    /// <summary>
    /// Reads this G or M code; false when the block does not hold it unread.
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
    /// Tells whether a word of the block is read.
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
