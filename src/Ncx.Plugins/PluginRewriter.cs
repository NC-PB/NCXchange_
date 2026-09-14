using Ncx.Core.Expander;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Plugins;

/// <summary>
/// Stands in the expander for a program rewriter of a plugin (architecture 5.5, 9): it gives the rewriter the context
/// with the plugin's own settings (D80), lets through only an answer whose every block parses under the option of
/// generated text (D95), says how many blocks the rewriter inserted, and reports and leaves out a plugin that throws,
/// so that the expansion goes on without it (implementation 17, P7-01). The blocks of its answers carry the name of
/// the plugin (architecture 9: trace shows the plugin's name).
/// </summary>
internal sealed class PluginRewriter : INamedRewriter
{
    // The expander parses the text of generated blocks with the pseudo-words of D95, and so the text is checked here.
    private static readonly ParserOptions s_generatedText = new() { AllowPseudoWords = true };

    private readonly Plugin _plugin;
    private readonly PluginDiagnostics _diagnostics;

    public PluginRewriter(Plugin plugin, IProgramRewriter rewriter, PluginDiagnostics diagnostics)
    {
        _plugin = plugin;
        _diagnostics = diagnostics;
        Rewriter = rewriter;
    }

    /// <summary>
    /// The rewriter of the plugin.
    /// </summary>
    public IProgramRewriter Rewriter { get; }

    /// <summary>
    /// The name of the plugin, which the blocks of its answers carry as their source.
    /// </summary>
    public string Name => _plugin.Name;

    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        // A plugin that failed is asked nothing more in the run (implementation 17, P7-01).
        if (_plugin.IsDropped)
        {
            return RewriteResult.Unchanged;
        }

        // The rewriter learns the machine name, the channel, the line and its own settings, and nothing of the virtual
        // machine (D61, D80, D106).
        var pluginContext = new PluginRewriteContext(context.MachineName, context.Channel, context.Line,
            _plugin.Settings);
        RewriteResult? result;
        try
        {
            result = Rewriter.Rewrite(block, pluginContext);
        }
        catch (Exception exception) when (PluginFaults.IsPluginFault(exception))
        {
            return Fail($"{nameof(Rewrite)} threw {PluginFaults.Describe(exception)}", context.Line);
        }

        if (result is null)
        {
            return Fail($"{nameof(Rewrite)} gave no answer", context.Line);
        }

        // Inserted NCX text that does not parse under the option of generated text is rejected here, and the whole
        // answer with it, so that nothing half of it reaches the program (implementation 17, P7-01; D95).
        if (!Parses(TextsOf(result), context.Line))
        {
            return RewriteResult.Unchanged;
        }

        // D98, code-guidelines 11: the blocks a rewriter inserted are an INFO with the plugin's name and the line.
        // TODO(question): D98, code-guidelines 11 and P7-02 print "inserted 2 blocks at line 12" for the coolant clutch
        // rule, whose answer inserts three blocks, @SAVE=SPINDLE:MAIN, SPINDLE:MAIN=OFF and @RESTORE=SPINDLE:MAIN.
        // Every block of the answer before and after the block is counted, three for the clutch rule, until that is
        // answered.
        int inserted = result.Kind == RewriteKind.Surround ? result.Before.Count + result.After.Count : 0;
        if (inserted > 0)
        {
            _diagnostics.Inserted(_plugin.Name, inserted, context.Line);
        }

        return result;
    }

    // The texts of an answer: the new text of the block for Replace, the texts before and after it for Surround; a
    // list a plugin left null holds no text.
    private static List<string?> TextsOf(RewriteResult result)
    {
        var texts = new List<string?>();
        if (result.Kind == RewriteKind.Replace)
        {
            texts.Add(result.Replacement);
        }
        else if (result.Kind == RewriteKind.Surround)
        {
            AddTexts(texts, result.Before);
            AddTexts(texts, result.After);
        }

        return texts;
    }

    // The texts of one side of a Surround; a list the plugin left null stands for one text that is not there.
    private static void AddTexts(List<string?> texts, IReadOnlyList<string>? side)
    {
        if (side is null)
        {
            texts.Add(null);
            return;
        }

        texts.AddRange(side);
    }

    // Every text parses as one block under the option of generated text (D95); each ERROR of the parser is reported
    // with the plugin's name on the line of the block the answer was for. What the expander checks of a block that
    // parses, a blank text or a word of the file frame, it reports itself with the plugin's name (language 4.15).
    private bool Parses(List<string?> texts, int line)
    {
        bool parses = true;
        foreach (string? text in texts)
        {
            if (text is null)
            {
                _diagnostics.TextDoesNotParse(_plugin.Name, "", line, "the answer holds no text for it");
                parses = false;
                continue;
            }

            var parsing = new Diagnostics(_plugin.Name);
            Parser.ParseBlock(text, line, s_generatedText, parsing);
            foreach (Diagnostic diagnostic in parsing.Items)
            {
                if (diagnostic.Severity == Severity.Error)
                {
                    _diagnostics.TextDoesNotParse(_plugin.Name, text, line, diagnostic.Code + " " + diagnostic.Message);
                    parses = false;
                }
            }
        }

        return parses;
    }

    // Implementation 17, P7-01: a plugin whose rewriter fails is reported, left out for the rest of the run, and the
    // block stays as it is.
    private RewriteResult Fail(string what, int line)
    {
        _diagnostics.Failed(_plugin.Name, what, line, null);
        _plugin.Drop();
        return RewriteResult.Unchanged;
    }
}
