using System.Globalization;
using Ncx.Core.Catalog;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Core.Parsing;

/// <summary>
/// Reads NCX text into an NcxProgram (architecture 4.1, virtual machine 3 step 1): the lexer cuts every line into its
/// words and its comment, the parser builds the words against the word catalog, converts their values and applies the
/// block rules of language 5, and the structure pass finds the file frame, the programs and the subprograms. The
/// parser never throws for user input; every problem is a diagnostic of the program (code-guidelines 6).
/// </summary>
public static class Parser
{
    /// <summary>
    /// Parses the text of a file.
    /// </summary>
    /// <param name="text">The whole text, UTF-8 decoded, LF or CRLF line endings (language 3, Encoding).</param>
    /// <param name="fileName">The name of the file, which every diagnostic carries (D98).</param>
    /// <param name="options">A user file, or the generated text of the expander (D95).</param>
    /// <returns>The program with its blocks, trivia, sections and diagnostics.</returns>
    public static NcxProgram Parse(string text, string fileName, ParserOptions options)
    {
        var diagnostics = new Diagnostics(fileName);
        LineEnding? lineEnding = Lexer.DetectLineEnding(text, diagnostics);
        var blocks = new List<Block>();
        var trivia = new List<Trivia>();
        List<string> lines = Lexer.SplitLines(text);
        for (int index = 0; index < lines.Count; index++)
        {
            // One line is one block; blank, whitespace-only and comment-only lines are not blocks but trivia, kept
            // with their line number and their text as read (language 3, Block; D92).
            int line = index + 1;
            Block? block = ParseBlock(lines[index], line, options, diagnostics);
            if (block is null)
            {
                trivia.Add(new Trivia(line, lines[index]));
            }
            else
            {
                blocks.Add(block);
            }
        }

        FileStructure structure = StructurePass.Run(blocks, diagnostics);
        return new NcxProgram
        {
            FileName = fileName,
            LineEnding = lineEnding,
            Blocks = blocks,
            Sections = structure.Sections,
            FileBegin = structure.FileBegin,
            FileEnd = structure.FileEnd,
            Trivia = trivia,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>
    /// Parses one line into a block, with the lexical rules of language 3 and the block rules of language 5, and
    /// without the file structure: what the expander needs for the NCX text of a generated block (language 4.15).
    /// </summary>
    /// <param name="text">The line without its line ending.</param>
    /// <param name="line">The 1-based line of the block, which every diagnostic carries.</param>
    /// <param name="options">A user file, or the generated text of the expander (D95).</param>
    /// <param name="diagnostics">Where the diagnostics go.</param>
    /// <returns>The block; null for a blank, whitespace-only or comment-only line, which is trivia (D92).</returns>
    public static Block? ParseBlock(string text, int line, ParserOptions options, Diagnostics diagnostics)
    {
        int reportedBefore = diagnostics.Items.Count;
        LexedLine lexed = Lexer.LexLine(text, line, options, diagnostics);
        if (lexed.IsTrivia)
        {
            return null;
        }

        List<Word> words = BuildWords(lexed, diagnostics);
        var block = new Block
        {
            Line = line,
            Words = words,
            Verb = BlockRules.FindVerb(words, line, diagnostics),
            Comment = lexed.Comment,
        };
        CheckWords(block, diagnostics);
        BlockRules.Check(block, diagnostics);

        // A block with an ERROR is kept with its source text, so nothing the user wrote is lost (architecture 4.1);
        // any other block keeps it under KeepSourceText.
        bool keepSourceText = options.KeepSourceText || HasErrorSince(diagnostics, reportedBefore);
        return keepSourceText ? block with { SourceText = text } : block;
    }

    // Every word with the catalog entry of its key, if the key has one, and its value converted by its form
    // (language 3, Word, value types; architecture 4). A value that cannot be kept is reported and its word left out.
    private static List<Word> BuildWords(LexedLine lexed, Diagnostics diagnostics)
    {
        var words = new List<Word>();
        foreach (LexedWord lexedWord in lexed.Words)
        {
            Value? value = ValueOf(lexedWord, lexed.Line, diagnostics);
            if (value is null)
            {
                continue;
            }

            words.Add(new Word
            {
                Key = lexedWord.Key,
                Addr = lexedWord.Addr,
                Value = value,
                Definition = WordCatalog.Lookup(lexedWord.Key),
            });
        }

        return words;
    }

    // The value types of language 3 are the value records of the model, one to one: an integer and a decimal keep
    // their text, a list its items, a string its content, an expression the tree of the expression parser, a state
    // key its key and address (language 2 rule 5; language 3; D95). Whether the entry of the word takes the value is
    // the catalog check that follows.
    private static Value? ValueOf(LexedWord word, int line, Diagnostics diagnostics)
    {
        string text = word.ValueText;
        switch (word.Form)
        {
            case ValueKinds.Integer:
                if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer))
                {
                    return new IntegerValue(integer, text);
                }

                ReportOutOfRange(word, "integer", line, diagnostics);
                return null;
            case ValueKinds.Decimal:
                if (decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out decimal number))
                {
                    return new DecimalValue(number, text);
                }

                ReportOutOfRange(word, "decimal", line, diagnostics);
                return null;
            case ValueKinds.Ident:
                return new IdentValue(text);
            case ValueKinds.List:
                return new ListValue(text.Split(','));
            case ValueKinds.String:
                return new StringValue(text);
            case ValueKinds.Expr:
                return ExpressionOf(text, line, diagnostics);
            case ValueKinds.StateKey:
                return StateKeyOf(text);
            default:
                return NoValue.Instance;
        }
    }

    // The text between the braces goes to the expression parser; the value keeps the canonical text of its tree,
    // which ncx format writes, and an expression the grammar does not take keeps its text as written after the ERROR
    // of the expression parser (language 4.12; P0-05).
    private static ExprValue ExpressionOf(string text, int line, Diagnostics diagnostics)
    {
        ExprNode? tree = ExprParser.Parse(text, line, diagnostics);
        return tree is null ? new ExprValue(text, null) : new ExprValue(tree.ToCanonical(), tree);
    }

    // KEY or KEY:ADDR, the value of @SAVE and @RESTORE (language 3, state key; D95).
    private static StateKeyValue StateKeyOf(string text)
    {
        int colon = text.IndexOf(':');
        return colon < 0
            ? new StateKeyValue(text, null)
            : new StateKeyValue(text.Substring(0, colon), text.Substring(colon + 1));
    }

    // Every word is checked against its catalog entry. A key the catalog does not list is one of two provisional
    // words, decided on the whole block because the condition is a property of the block: in a block that carries
    // CYCLE:controller=n it is a native parameter, one of the machine-axis form included (D94); elsewhere a key of the
    // machine-axis form is a machine axis word, which rule 2 places under its verb (D93). Any other key is unknown
    // and an ERROR (virtual machine 3 step 1; architecture 4.1). The parameter names of a cycle catalog entry
    // (CYCLE=RECT_POCKET LENGTH=60, language 4.7.1) are unknown keys as well, by the open question of
    // WordCatalog.IsNativeParameterAllowed.
    private static void CheckWords(Block block, Diagnostics diagnostics)
    {
        bool nativeBlock = WordCatalog.IsNativeParameterAllowed(block);
        foreach (Word word in block.Words)
        {
            if (word.Definition is WordDefinition definition)
            {
                WordCheck.Accepts(word, definition, block, diagnostics);
            }
            else if (nativeBlock)
            {
                CheckProvisionalWord(word, block, "a native parameter of a CYCLE:controller=n block",
                    "language 4.7.1, D94", bareAllowed: false, diagnostics);
            }
            else if (WordCatalog.TryMachineAxis(word.Key, out _))
            {
                CheckProvisionalWord(word, block, "a machine axis word", "language 4.3, D93", bareAllowed: true,
                    diagnostics);
            }
            else
            {
                diagnostics.Error(block, DiagnosticCodes.UnknownKey,
                    $"{word.Key} is no word of the catalog, no machine axis word (D93) and no native cycle parameter "
                    + "(D94); the block is kept as written (virtual machine 3 step 1, architecture 4.1).");
            }
        }
    }

    // A machine axis word and a native parameter take a number or an expression and no address (language 3, ADDR;
    // D93, D94). A bare machine axis word is the question of rule 2: bare under HOME only.
    private static void CheckProvisionalWord(Word word, Block block, string kind, string source, bool bareAllowed,
        Diagnostics diagnostics)
    {
        if (word.Addr is not null)
        {
            diagnostics.Error(block, DiagnosticCodes.WordTakesNoAddress,
                $"{word.Key} is {kind} and takes no address, not {word.Key}:{word.Addr} ({source}).");
            return;
        }

        bool isNumberOrExpression = word.Value is IntegerValue or DecimalValue or ExprValue;
        if (isNumberOrExpression || (bareAllowed && word.Value is NoValue))
        {
            return;
        }

        string given = word.Value is NoValue ? "no value" : word.Value.ToCanonical();
        diagnostics.Error(block, DiagnosticCodes.WordValueNotAccepted,
            $"{word.Key} is {kind} and takes a number or an expression, not {given} ({source}).");
    }

    // True when an ERROR was reported after the first so many diagnostics.
    private static bool HasErrorSince(Diagnostics diagnostics, int reportedBefore)
    {
        for (int index = reportedBefore; index < diagnostics.Items.Count; index++)
        {
            if (diagnostics.Items[index].Severity == Severity.Error)
            {
                return true;
            }
        }

        return false;
    }

    // A number beyond what the model keeps: an integer beyond a long, a decimal beyond a decimal (language 3).
    private static void ReportOutOfRange(LexedWord word, string valueType, int line, Diagnostics diagnostics)
    {
        diagnostics.Error(line, DiagnosticCodes.NumberOutOfRange, string.Create(CultureInfo.InvariantCulture,
            $"The {valueType} {word.ValueText} of {word.Key} at column {word.Column} is too large to be kept as a "
            + $"number (language 3, {valueType})."));
    }
}
