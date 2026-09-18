using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Core.Expander;

/// <summary>
/// Turns the NCX text of an expansion rule or a rewriter into a generated block (language 4.15, virtual machine 3.10):
/// parsed with the option of generated text, so that it may carry @SAVE and @RESTORE (D95), marked with its origin,
/// and every diagnostic about it reported on the line of the block it was generated for (D98).
/// </summary>
internal sealed class GeneratedText
{
    // The expander parses the text of generated blocks with the pseudo-words of D95.
    private static readonly ParserOptions s_generatedText = new() { AllowPseudoWords = true };

    // The words that open and close the file, a program and a subprogram, each exactly once (language 4.1, 4.13).
    private static readonly string[] s_frameKeys = ["FILE", "NCX", "PROGRAM", "SUB"];

    private readonly MachineConfig _machine;
    private readonly Diagnostics _diagnostics;

    // An unknown position is an ERROR on the rule and is reported once per rule text (architecture 5.5, D100).
    private readonly HashSet<string> _unknownPositionReported = new(StringComparer.Ordinal);

    public GeneratedText(MachineConfig machine, Diagnostics diagnostics)
    {
        _machine = machine;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// The block of a text of an expansion rule: {position:NAME} is replaced by the axis words of [positions] before
    /// the block is parsed, and an unknown name is an ERROR on the rule that gives no block (architecture 5.5, D100).
    /// </summary>
    /// <param name="text">The text as the rule writes it, or the words the expander writes for requires and restore.
    /// </param>
    /// <param name="ruleKey">The key of the rule the text comes from: pre, post, requires, restore.</param>
    /// <param name="rule">The triggered rule with its table.</param>
    /// <param name="origin">The block of the file the block is generated for.</param>
    /// <param name="placement">Before or after it.</param>
    public Block? FromRule(string text, string ruleKey, TriggeredRule rule, Block origin,
        GeneratedPlacement placement)
    {
        string writer = rule.Source + " " + ruleKey;
        if (PositionPlaceholder.UnknownName(text, _machine.Positions) is string unknownName)
        {
            if (_unknownPositionReported.Add(writer + " " + text))
            {
                Error(origin, DiagnosticCodes.UnknownPosition,
                    $"{writer} \"{text}\": {{position:{unknownName}}} names no entry of [positions] of the machine "
                    + "(machine-config 5a, D100).");
            }

            return null;
        }

        var generated = new GeneratedBlock
        {
            Origin = origin,
            Source = rule.Source,
            Reason = ruleKey,
            Placement = placement,
        };
        return Parse(text, PositionPlaceholder.Expand(text, _machine.Positions), writer, generated);
    }

    /// <summary>
    /// The block of a text a rewriter gives, parsed as it stands.
    /// </summary>
    /// <param name="text">The NCX text of one block.</param>
    /// <param name="rewriter">The name of the rewriter, which every diagnostic about the text names.</param>
    /// <param name="generated">The origin the block carries.</param>
    public Block? FromRewriter(string text, string rewriter, GeneratedBlock generated)
    {
        return Parse(text, text, rewriter, generated);
    }

    /// <summary>
    /// Reports an ERROR about blocks generated for a block of the file: on its line, as the diagnostic of a generated
    /// block (D98).
    /// </summary>
    public void Error(Block origin, string code, string message)
    {
        _diagnostics.Add(new Diagnostic
        {
            Severity = Severity.Error,
            File = _diagnostics.File,
            Line = origin.Line,
            OriginLine = origin.Line,
            Code = code,
            Message = message,
        });
    }

    /// <summary>
    /// A block marked as generated: IsGenerated, the line of its origin as the OriginLine every diagnostic on it
    /// carries (D98), its origin, and the SKIP of its origin.
    /// </summary>
    public static Block Mark(Block block, GeneratedBlock generated)
    {
        // TODO(question): language 4.15 and virtual machine 3.10 do not say whether a generated block is skipped with
        // the SKIP block it was generated for (D53); it carries the SKIP word of its origin, so that skip_blocks skips
        // the stop, the retract or the mode code together with the block that needs them, until D201 is answered.
        IReadOnlyList<Word> words = block.Words;
        if (generated.Origin.Find("SKIP") is Word skip && !block.Has("SKIP"))
        {
            var withSkip = new List<Word> { skip };
            withSkip.AddRange(block.Words);
            words = withSkip;
        }

        return block with
        {
            Words = words,
            IsGenerated = true,
            OriginLine = generated.Origin.Line,
            Generated = generated,
        };
    }

    /// <summary>
    /// The origin of a block that a rewriter or the limits rewrote: a generated block keeps its place and adds the
    /// source and the reason to its own; a block of the file becomes a block in its place, which names it as its
    /// origin (language 4.15).
    /// </summary>
    public static GeneratedBlock Rewritten(Block block, Block origin, string source, string reason)
    {
        if (block.Generated is GeneratedBlock generated)
        {
            return generated with
            {
                Source = generated.Source + ", " + source,
                Reason = generated.Reason + "; " + reason,
            };
        }

        return new GeneratedBlock
        {
            Origin = origin,
            Source = source,
            Reason = reason,
            Placement = GeneratedPlacement.InPlace,
        };
    }

    // A generated block has no line of its own in the file: it takes the line of the block it was generated for, and a
    // diagnostic about its text names that block and the rule or rewriter that wrote it (language 4.15, D98).
    // TODO(question): D98 renders a diagnostic on a generated block as file(line, from 12) without saying which line a
    // generated block has, since it stands on no line of the file; it has the line of its origin, which reads
    // file(12, from 12), until D200 is answered.
    private Block? Parse(string text, string parsedText, string writer, GeneratedBlock generated)
    {
        Block origin = generated.Origin;
        var parsing = new Diagnostics(_diagnostics.File);
        Block? block = Parser.ParseBlock(parsedText, origin.Line, s_generatedText, parsing);
        foreach (Diagnostic diagnostic in parsing.Items)
        {
            _diagnostics.Add(diagnostic with
            {
                OriginLine = origin.Line,
                Message = $"{writer} \"{text}\": {diagnostic.Message}",
            });
        }

        // Generated blocks are ordinary NCX blocks inserted before or after the block (language 4.15); a blank or a
        // comment-only line is none (language 3, Block).
        if (block is null)
        {
            Error(origin, DiagnosticCodes.GeneratedTextWithoutBlock,
                $"{writer} \"{text}\" holds no block; a generated block is one block of NCX words (language 3, Block; "
                + "4.15).");
            return null;
        }

        // The file frame and the frames of its programs and subprograms stand exactly once (language 4.1, 4.13).
        foreach (string frameKey in s_frameKeys)
        {
            if (block.Has(frameKey))
            {
                Error(origin, DiagnosticCodes.GeneratedFrameWord,
                    $"{writer} \"{text}\" writes {frameKey}, which opens or closes the file, a program or a subprogram "
                    + "exactly once; a generated block stands inside them (language 4.1, 4.13).");
                return null;
            }
        }

        return Mark(block, generated);
    }
}
