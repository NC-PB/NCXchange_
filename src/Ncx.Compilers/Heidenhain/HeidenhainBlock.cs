using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The block being written as Klartext and what the concerns of the Heidenhain compiler write it with: the step with
/// the state before and after it, the machine with its number format and templates, what the control has active, the
/// look-ahead of the STATIC pass, and the words of the block that a concern has written, so that no word of the program
/// is dropped without a diagnostic (language 2 rule 8; architecture 8).
/// </summary>
internal sealed class HeidenhainBlock
{
    // The optional block skip is a / at the start of the block (controllers heidenhain.md 1; controller-mapping 1,
    // SKIP).
    private const string SkipMark = "/";

    private readonly HashSet<Word> _written = [];

    /// <summary>
    /// The block with its state before and after it and its events (architecture 8).
    /// </summary>
    public required BlockStep Step { get; init; }

    /// <summary>
    /// The machine of the compile.
    /// </summary>
    public required MachineConfig Machine { get; init; }

    /// <summary>
    /// The numbers as [format] writes them, the comma of Klartext among them (machine-config 2).
    /// </summary>
    public required NumberFormatter Numbers { get; init; }

    /// <summary>
    /// What the control has active in the program or walk being written, so that a modal word is written on change.
    /// </summary>
    public required TargetState Target { get; init; }

    /// <summary>
    /// The diagnostics of the compile (D98).
    /// </summary>
    public required Diagnostics Diagnostics { get; init; }

    /// <summary>
    /// Every step of the run, for what a block needs from the blocks around it (architecture 8, D52).
    /// </summary>
    public required LookAhead LookAhead { get; init; }

    /// <summary>
    /// The templates of the machine, each parsed once (architecture 6).
    /// </summary>
    public required TemplateSet Templates { get; init; }

    /// <summary>
    /// Writes one line of the block (CompilerBase.Line).
    /// </summary>
    public required Action<string> WriteLine { get; init; }

    /// <summary>
    /// Writes the tool change of the block per [tool_change] with the values the compiler sets itself
    /// (CompilerBase.WriteToolChange).
    /// </summary>
    public required Action<TemplateValues> WriteToolChange { get; init; }

    /// <summary>
    /// Writes the PRELOAD of the block with the preload template (CompilerBase.WritePreload).
    /// </summary>
    public required Action WritePreload { get; init; }

    /// <summary>
    /// A comment text in the charset of the machine (CompilerBase.CommentText).
    /// </summary>
    public required Func<string, string> CommentText { get; init; }

    /// <summary>
    /// The block.
    /// </summary>
    public Block Block => Step.Block;

    /// <summary>
    /// The state of the channel before the block.
    /// </summary>
    public ChannelSnapshot Before => Step.Before;

    /// <summary>
    /// The state of the channel after the block.
    /// </summary>
    public ChannelSnapshot After => Step.After;

    /// <summary>
    /// Writes a line of the block, or several where the text holds line breaks. Every line of a block with SKIP starts
    /// with the block skip, except the continuation lines of a cycle definition, which belong to its first line
    /// (controllers heidenhain.md 1).
    /// </summary>
    /// <param name="text">The Klartext without block number.</param>
    public void Line(string text)
    {
        foreach (string line in text.Split('\n'))
        {
            bool skipped = Block.Skip && !HeidenhainCycles.IsContinuation(line);
            WriteLine(skipped ? SkipMark + line : line);
        }
    }

    /// <summary>
    /// The word of the block with this key and without an address, marked as written; null when the block has none.
    /// </summary>
    /// <param name="key">The key: "FEED_MODE".</param>
    public Word? Take(string key)
    {
        return Take(key, null);
    }

    /// <summary>
    /// The word of the block with this key and address, marked as written; null when the block has none.
    /// </summary>
    /// <param name="key">The key: "TOLERANCE".</param>
    /// <param name="addr">The address: "ROTARY"; null for the word without one.</param>
    public Word? Take(string key, string? addr)
    {
        Word? word = Block.Find(key, addr);
        if (word is not null)
        {
            _written.Add(word);
        }

        return word;
    }

    /// <summary>
    /// Every word of the block with this key, whatever its address, marked as written.
    /// </summary>
    /// <param name="key">The key: "OFFSET".</param>
    public List<Word> TakeAll(string key)
    {
        var words = new List<Word>();
        foreach (Word word in Block.Words)
        {
            if (word.Key == key)
            {
                _written.Add(word);
                words.Add(word);
            }
        }

        return words;
    }

    /// <summary>
    /// Marks a word of the block as written.
    /// </summary>
    public void MarkWritten(Word word)
    {
        _written.Add(word);
    }

    /// <summary>
    /// The words of the block that no concern has written, in the order of the block.
    /// </summary>
    public List<Word> Unwritten()
    {
        var words = new List<Word>();
        foreach (Word word in Block.Words)
        {
            if (!_written.Contains(word))
            {
                words.Add(word);
            }
        }

        return words;
    }

    /// <summary>
    /// Writes a template of the machine for the block: rendered with the values, each of its lines a line of the block
    /// (machine-config introduction, 3). A template the machine file does not give is an ERROR, as in the framework
    /// (CMP010); an empty template writes nothing.
    /// </summary>
    /// <param name="text">The template text as the record of the machine keeps it; null when the file has none.</param>
    /// <param name="what">The template as a message names it: "[spindle.TOOL] CW (machine-config 5)".</param>
    /// <param name="values">The values of its placeholders.</param>
    /// <returns>True when it was written.</returns>
    public bool Template(string? text, string what, TemplateValues values)
    {
        // A word whose template the machine does not give cannot be written (machine-config introduction).
        if (text is null)
        {
            Error(DiagnosticCodes.TemplateMissing,
                $"The machine \"{Machine.Machine.Name}\" has no {what}, which {WordsText(Block.Words)} needs, so "
                + "nothing is written for it.");
            return false;
        }

        Template template = Templates.For(text) ?? new Template(text, 1, new Diagnostics(Machine.Machine.Name));
        string? rendered = template.Render(values, Block, Diagnostics);
        if (rendered is null)
        {
            return false;
        }

        if (rendered.Length > 0)
        {
            Line(rendered);
        }

        return true;
    }

    /// <summary>
    /// Reports an ERROR on the block (D98).
    /// </summary>
    public void Error(string code, string message)
    {
        Diagnostics.Error(Block, code, message);
    }

    /// <summary>
    /// Reports a WARNING on the block (D98).
    /// </summary>
    public void Warning(string code, string message)
    {
        Diagnostics.Warning(Block, code, message);
    }

    /// <summary>
    /// Words as canonical NCX writes them, for a message.
    /// </summary>
    public static string WordsText(IEnumerable<Word> words)
    {
        var texts = new List<string>();
        foreach (Word word in words)
        {
            texts.Add(word.ToCanonical());
        }

        return string.Join(" ", texts);
    }
}
