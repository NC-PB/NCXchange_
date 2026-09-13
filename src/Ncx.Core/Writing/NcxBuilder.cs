using System.Globalization;
using Ncx.Core.Catalog;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Core.Writing;

/// <summary>
/// Assembles a program block by block while a reader walks its source (architecture 7; code-guidelines 5, Builder):
/// Begin(sourceLine).Verb(...).Word(key, addr, value)...End() for each block, Trivia(text) for a comment-only or blank
/// line, Build() for the program. A reader discovers the words of a block in source order and wants them in canonical
/// order, so the builder sorts them at End() (language 5 rule 6); the canonical writer writes the result (architecture
/// 4.1).
/// </summary>
public sealed class NcxBuilder
{
    // The word that keeps the source text NCX cannot express, with the controller as its address (language 4.1).
    private const string RawKey = "RAW";

    private readonly Diagnostics _diagnostics;
    private readonly List<Block> _blocks = [];
    private readonly List<Trivia> _trivia = [];
    private readonly List<Word> _openWords = [];

    // The block being built: its line and its comment; no line while no block is open.
    private int? _openLine;
    private string? _openComment;

    // The line of the block begun last, 0 before the first; a trivia line takes it (see Trivia).
    private int _lastLine;

    private bool _built;

    /// <summary>
    /// Starts an empty program.
    /// </summary>
    /// <param name="diagnostics">The diagnostics of the reader; the program carries them and their file name
    /// (D98).</param>
    public NcxBuilder(Diagnostics diagnostics)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Begins a block.
    /// </summary>
    /// <param name="sourceLine">The 1-based line of the source block the block is read from, which its diagnostics
    /// cite (architecture 7, D98).</param>
    public NcxBuilder Begin(int sourceLine)
    {
        EnsureNotBuilt();
        if (_openLine is int openLine)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"The block of line {openLine} is still open; End() it before the next Begin()."));
        }

        _openLine = sourceLine;
        _lastLine = sourceLine;
        return this;
    }

    /// <summary>
    /// Adds the verb of the block, a verb without a value: RAPID, LINE, HOME (language 5 rule 1).
    /// </summary>
    /// <param name="key">The verb, uppercase: "LINE".</param>
    public NcxBuilder Verb(string key)
    {
        return Verb(key, NoValue.Instance);
    }

    /// <summary>
    /// Adds the verb of the block with its value: ARC=CW (language 4.3, 5 rule 1).
    /// </summary>
    /// <param name="key">The verb, uppercase: "ARC".</param>
    /// <param name="value">Its value: CW.</param>
    public NcxBuilder Verb(string key, Value value)
    {
        // A verb makes the block a motion or frame block; the =RESET form of SHIFT, TILT and TILT_AXIS is no verb but a
        // frame word, added with Word (language 4.2, 5 rule 1, D90).
        var verb = new Word { Key = key, Value = value, Definition = WordCatalog.Lookup(key) };
        if (!WordCatalog.IsVerb(verb))
        {
            throw new ArgumentException(
                $"{verb.ToCanonical()} is no verb (language 5 rule 1); add it with Word().", nameof(key));
        }

        return Add(verb);
    }

    /// <summary>
    /// Adds a word to the block, in any order; End() sorts the words. A key the catalog does not list is a machine
    /// axis word (D93) or a native cycle parameter (D94) and keeps no definition.
    /// </summary>
    /// <param name="key">The key, uppercase: "OFFSET".</param>
    /// <param name="addr">The address, uppercase: "LEN"; null for a word without one.</param>
    /// <param name="value">The value; NoValue for a bare word.</param>
    public NcxBuilder Word(string key, string? addr, Value value)
    {
        return Add(new Word { Key = key, Addr = addr, Value = value, Definition = WordCatalog.Lookup(key) });
    }

    /// <summary>
    /// Adds the source text that NCX cannot express to the block, verbatim, as the word RAW:controller="text"
    /// (language 4.1; D5).
    /// </summary>
    /// <param name="controller">The controller or builder dialect: "FANUC", "NAKAMURA".</param>
    /// <param name="text">The source text as read.</param>
    public NcxBuilder Raw(string controller, string text)
    {
        // TODO(question): architecture 7 draws Raw(controller, text) as a call of its own that returns nothing, which
        // reads as a whole RAW block, and gives it no line; the phase plan lists it among the calls of a block. It adds
        // the word RAW:controller="text" to the block being built, so that a whole source block is
        // Begin(line).Raw(controller, text).End(), until that is answered.
        return Word(RawKey, controller, new StringValue(text));
    }

    /// <summary>
    /// Gives the block its trailing comment, written in the comment column (language 5 rule 7).
    /// </summary>
    /// <param name="text">The comment as it stands in the NCX file, from the semicolon to the end of the line:
    /// "; O0001 (BUILT)".</param>
    public NcxBuilder Comment(string text)
    {
        EnsureOpen();

        // A block keeps its trailing comment as language 3 defines a comment, from the semicolon to the end of the
        // line, and has one (D92).
        if (!text.StartsWith(';') || HasLineBreak(text))
        {
            throw new ArgumentException(
                "A comment starts with its semicolon and ends with its line (language 3, Comment).", nameof(text));
        }

        if (_openComment is not null)
        {
            throw new InvalidOperationException("The block has its comment already; a block has one (D92).");
        }

        _openComment = text;
        return this;
    }

    /// <summary>
    /// Ends the block: its words sorted into canonical order, its verb marked (language 5 rules 1 and 6).
    /// </summary>
    public void End()
    {
        int line = EnsureOpen();

        // A line that holds no word is no block but trivia (language 3, Block; D92).
        if (_openWords.Count == 0)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"The block of line {line} holds no word; a comment-only line is trivia (language 3, Block)."));
        }

        var block = new Block { Line = line, Words = [.. _openWords], Comment = _openComment };
        _blocks.Add(block with { Words = CanonicalOrder.Sort(block), Verb = VerbOf(block) });
        _openWords.Clear();
        _openLine = null;
        _openComment = null;
    }

    /// <summary>
    /// Adds a comment-only or blank line where it is called: after the block ended last and before the block begun
    /// next (D92; controller-mapping 1: a comment-only source line becomes trivia).
    /// </summary>
    /// <param name="text">The line as it stands in the NCX file: "; a comment", or empty.</param>
    public void Trivia(string text)
    {
        EnsureNotBuilt();
        if (_openLine is int openLine)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"The block of line {openLine} is still open; a trivia line is a line of its own (D92)."));
        }

        // Blank, whitespace-only and comment-only lines are trivia (language 3, Block).
        string content = text.TrimStart();
        if ((content.Length > 0 && !content.StartsWith(';')) || HasLineBreak(text))
        {
            throw new ArgumentException(
                "A trivia line is one blank, whitespace-only or comment-only line (language 3, Block).", nameof(text));
        }

        // The writer places trivia among the blocks by line number, each before the first block of a greater line
        // (D92). A trivia line takes the line of the block begun last, 0 before the first, so that it is written after
        // that block and before the next block of a greater line.
        // TODO(question): a program places its trivia among its blocks by line number alone (architecture 4,
        // NcxProgram.Trivia), and a reader begins each block with the line of its source block (architecture 7). Where
        // the lines of a reader's blocks decrease, as when it moves a section kept below M30 in front of PROGRAM=END
        // (language 4.13), or where two blocks of one source line stand around a trivia line, the writer may place the
        // trivia line away from its call. The builder keeps the line of the block begun last until that is answered.
        _trivia.Add(new Trivia(_lastLine, text));
    }

    /// <summary>
    /// Builds the program: the blocks and trivia in the order they were added, the programs, subprograms and the file
    /// frame found by the structure pass of the parser, whose ERRORs join the diagnostics (language 4.1, 4.13). The
    /// program has no line ending of its own, so the writer ends its lines with LF.
    /// </summary>
    public NcxProgram Build()
    {
        EnsureNotBuilt();
        if (_openLine is int openLine)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"The block of line {openLine} is still open; End() it before Build()."));
        }

        _built = true;
        FileStructure structure = StructurePass.Run(_blocks, _diagnostics);
        return new NcxProgram
        {
            FileName = _diagnostics.File,
            Blocks = [.. _blocks],
            Sections = structure.Sections,
            FileBegin = structure.FileBegin,
            FileEnd = structure.FileEnd,
            Trivia = [.. _trivia],
            Diagnostics = _diagnostics,
        };
    }

    private NcxBuilder Add(Word word)
    {
        EnsureOpen();
        _openWords.Add(word);
        return this;
    }

    // A word, a comment and End() belong to a block that has begun.
    private int EnsureOpen()
    {
        if (_openLine is not int line)
        {
            throw new InvalidOperationException("No block is open; Begin() one first.");
        }

        return line;
    }

    // A builder builds one program.
    private void EnsureNotBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("The program is built already; a builder builds one program.");
        }
    }

    // A block has at most one verb; the =RESET form of SHIFT, TILT and TILT_AXIS is none (language 5 rule 1, D90).
    private static Word? VerbOf(Block block)
    {
        Word? verb = null;
        foreach (Word word in block.Words)
        {
            if (!WordCatalog.IsVerb(word))
            {
                continue;
            }

            if (verb is not null)
            {
                throw new InvalidOperationException(
                    $"{word.ToCanonical()} is a second verb beside {verb.ToCanonical()}; a block has at most one verb "
                    + "(language 5 rule 1).");
            }

            verb = word;
        }

        return verb;
    }

    // One line of the file holds no line break.
    private static bool HasLineBreak(string text)
    {
        return text.Contains('\n') || text.Contains('\r');
    }
}
