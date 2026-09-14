using Ncx.Config.Templates;
using Ncx.Core.Model;

namespace Ncx.Compilers;

// The templates of the machine, rendered for the block being written (machine-config introduction, 2, 3).
public abstract partial class CompilerBase
{
    /// <summary>
    /// Writes a template of the machine for the block being written: rendered with the values, each line of it a line
    /// (machine-config introduction, 3). A template the machine file does not give, or a placeholder without a value,
    /// is an ERROR on the block; an empty template writes nothing.
    /// </summary>
    /// <param name="text">The template text as the record of the machine keeps it; null when the file has none.</param>
    /// <param name="what">The template as a message names it: "[tool_change] change (machine-config 3)".</param>
    /// <param name="values">The values of its placeholders.</param>
    /// <returns>True when it was written.</returns>
    protected bool WriteTemplate(string? text, string what, TemplateValues values)
    {
        // A word whose template the machine does not give cannot be written, as a template without a value for a
        // placeholder cannot (machine-config introduction).
        Block block = Step.Block;
        if (TemplateOf(text) is not Template template)
        {
            Diagnostics.Error(block, DiagnosticCodes.TemplateMissing,
                $"The machine \"{Machine.Machine.Name}\" has no {what}, which {WordsOf(block)} needs, so nothing is "
                + "written for it.");
            return false;
        }

        string? rendered = template.Render(values, block, Diagnostics);
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
    /// Writes PROGRAM=END as program_end says, M30 or M2 (D48, D49; machine-config 2).
    /// </summary>
    protected void WriteProgramEnd()
    {
        WriteTemplate(Machine.Format?.ProgramEnd, "[format] program_end (machine-config 2, D49)", new TemplateValues());
    }

    /// <summary>
    /// Writes SUB=END and RETURN as sub_end says, M99, LBL 0, RET (machine-config 2).
    /// </summary>
    protected void WriteSubEnd()
    {
        WriteTemplate(Machine.Format?.SubEnd, "[format] sub_end (machine-config 2)", new TemplateValues());
    }

    // The parsed template of a text: the machine's own from its template set, any other parsed here.
    private Template? TemplateOf(string? text)
    {
        if (text is null)
        {
            return null;
        }

        return Templates.For(text) ?? new Template(text, 1, new Diagnostics(Machine.Machine.Name));
    }

    // Whether a template has a placeholder of this name.
    private bool HasPlaceholder(string? text, string name)
    {
        if (TemplateOf(text) is not Template template)
        {
            return false;
        }

        foreach (Placeholder placeholder in template.Placeholders)
        {
            if (placeholder.Name == name)
            {
                return true;
            }
        }

        return false;
    }

    // The words of a block as canonical NCX writes them, for a message.
    private static string WordsOf(Block block)
    {
        var words = new List<string>();
        foreach (Word word in block.Words)
        {
            words.Add(word.ToCanonical());
        }

        return string.Join(" ", words);
    }
}
