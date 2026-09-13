using System.Text;
using System.Text.RegularExpressions;
using Ncx.Core.Model;

namespace Ncx.Config.Templates;

/// <summary>
/// A template of the machine file: literal text with {placeholders}, parsed once, rendered by the compilers and matched
/// by the readers, so that one string describes both directions (machine-config introduction, architecture 6).
/// </summary>
public sealed class Template
{
    /// <summary>
    /// The decimal point, which numbers are written with unless the machine writes the comma (machine-config 2).
    /// </summary>
    internal const string DecimalPoint = ".";

    /// <summary>
    /// The comma, the decimal separator of Klartext files (machine-config 2, controllers heidenhain.md 1).
    /// </summary>
    internal const string DecimalComma = ",";

    // The literal text before, between and after the placeholders, one piece more than there are placeholders; a line
    // break of the template stays in its piece (machine-config 3).
    private readonly List<string> _literals = [];
    private readonly List<Placeholder> _placeholders = [];
    private readonly Regex _pattern;

    // The decimal separator the machine writes numbers with, from its [format] (machine-config 2); what a number reads
    // with is the pattern's.
    private readonly string _decimalSeparator;

    /// <summary>
    /// Parses a template once (code-guidelines 7). Braces that are not a placeholder are an ERROR of the machine file
    /// and stay in the template as literal text. Numbers are written and read with the decimal point; the templates of
    /// a machine come from its <see cref="TemplateSet"/>, with the decimal separator its [format] writes and the comma
    /// its controller reads.
    /// </summary>
    /// <param name="text">The template as the machine file gives it, with a line break where the file writes \n:
    /// "T{tool} M6".</param>
    /// <param name="line">The line of the template in the machine file, which a diagnostic cites.</param>
    /// <param name="diagnostics">The diagnostics of the machine file.</param>
    public Template(string text, int line, Diagnostics diagnostics)
        : this(text, line, DecimalPoint, readsComma: false, diagnostics)
    {
    }

    /// <summary>
    /// Parses a template of a machine that writes numbers with the decimal separator of its [format] and may read the
    /// comma as well as the point, the comma of Klartext (machine-config 2, controllers heidenhain.md 7 rule 8).
    /// </summary>
    /// <param name="text">The template as the machine file gives it.</param>
    /// <param name="line">The line of the template in the machine file, which a diagnostic cites.</param>
    /// <param name="decimalSeparator">The decimal separator the machine writes, "." or ",".</param>
    /// <param name="readsComma">True when a number reads with the comma as well as with the point.</param>
    /// <param name="diagnostics">The diagnostics of the machine file.</param>
    internal Template(string text, int line, string decimalSeparator, bool readsComma, Diagnostics diagnostics)
    {
        Text = text;
        _decimalSeparator = decimalSeparator;
        Parse(line, diagnostics);
        _pattern = TemplatePattern.Build(_literals, _placeholders, readsComma);
    }

    /// <summary>
    /// The template as written: "G340 T{tool:02}{offset:02}. A{next:02}.".
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The placeholders in the order they stand, those of every line of the template.
    /// </summary>
    public IReadOnlyList<Placeholder> Placeholders => _placeholders;

    /// <summary>
    /// Writes the template with the values of its placeholders, numbers padded by their format suffix and written with
    /// the decimal separator of the machine; a line break of the template is a line break of the text (machine-config
    /// introduction, 2 and 3).
    /// </summary>
    /// <param name="values">The values by placeholder name.</param>
    /// <param name="block">The block the template is written for, which a diagnostic cites (D98).</param>
    /// <param name="diagnostics">The diagnostics of the program being written.</param>
    /// <returns>The text; null when a placeholder has no value, which leaves the template unusable and is a CFG ERROR
    /// on the block for each placeholder without one.</returns>
    public string? Render(TemplateValues values, Block block, Diagnostics diagnostics)
    {
        var text = new StringBuilder(_literals[0]);
        var missing = new List<string>();
        for (int index = 0; index < _placeholders.Count; index++)
        {
            Placeholder placeholder = _placeholders[index];
            string? value = placeholder.Write(values, _decimalSeparator);
            if (value is not null)
            {
                text.Append(value);
            }
            else if (!missing.Contains(placeholder.Name))
            {
                // A missing placeholder value leaves the template unusable and the compiler reports it, naming the
                // placeholder and the template, once for a placeholder that stands twice (machine-config
                // introduction).
                missing.Add(placeholder.Name);
                diagnostics.Error(block, DiagnosticCodes.TemplateValueMissing,
                    $"The template {Quoted(Text)} has no value for {placeholder}; a missing placeholder value leaves "
                    + "the template unusable (machine-config introduction).");
            }

            text.Append(_literals[index + 1]);
        }

        if (missing.Count > 0)
        {
            return null;
        }

        return text.ToString();
    }

    /// <summary>
    /// Recognizes a native text as this template and captures the values of its placeholders. Blanks between words may
    /// be left out or doubled, blanks at the ends do not count, an M or G code of the literal text compares by number
    /// (M8 matches M08), a padded placeholder reads the digits it pads to, and a number reads with the point and, on a
    /// machine that reads the comma, with the comma (architecture 6, D105, controllers heidenhain.md 7 rule 8).
    /// </summary>
    /// <param name="text">The native text, as many lines as the template has: "G340 T0101. A02.".</param>
    /// <param name="captured">The values found, numbers by their number and words as written; empty when the text is
    /// not this template.</param>
    /// <returns>True when the whole text is this template.</returns>
    public bool Matches(string text, out TemplateValues captured)
    {
        captured = new TemplateValues();
        Match match = _pattern.Match(text);
        if (!match.Success)
        {
            return false;
        }

        // Every placeholder takes the value its group found; a placeholder that stands twice must find one value.
        var values = new TemplateValues();
        for (int index = 0; index < _placeholders.Count; index++)
        {
            string found = match.Groups[TemplatePattern.GroupName(index)].Value;
            if (!_placeholders[index].Capture(found, values))
            {
                return false;
            }
        }

        captured = values;
        return true;
    }

    // The template as the machine file writes it in a TOML string, \" for a quote, \\ for a backslash, \n for a line
    // break, so that a message names the template in a form the user finds in the file (code-guidelines 2).
    internal static string Quoted(string text)
    {
        var quoted = new StringBuilder();
        quoted.Append('"');
        foreach (char character in text)
        {
            if (character == '\n')
            {
                quoted.Append("""\n""");
                continue;
            }

            if (character is '"' or '\\')
            {
                quoted.Append('\\');
            }

            quoted.Append(character);
        }

        quoted.Append('"');
        return quoted.ToString();
    }

    // Templates are strings with {placeholders} (machine-config introduction): the text between the placeholders is
    // literal and kept as written, a line break included, since a template may span lines with \n (machine-config 3).
    // A brace that opens no placeholder or closes none leaves the template unusable and is reported; it stays in the
    // text as a literal brace.
    private void Parse(int line, Diagnostics diagnostics)
    {
        string quoted = Quoted(Text);
        var literal = new StringBuilder();
        int position = 0;
        while (position < Text.Length)
        {
            char character = Text[position];
            if (character == '}')
            {
                diagnostics.Error(line, DiagnosticCodes.TemplatePlaceholderMalformed,
                    $"The template {quoted} closes a placeholder that was never opened (machine-config introduction).");
                literal.Append(character);
                position++;
                continue;
            }

            if (character != '{')
            {
                literal.Append(character);
                position++;
                continue;
            }

            // A placeholder ends at its closing brace; an opening brace before that means this one is never closed.
            int close = Text.IndexOf('}', position + 1);
            int nextOpen = Text.IndexOf('{', position + 1);
            if (close < 0 || (nextOpen >= 0 && nextOpen < close))
            {
                diagnostics.Error(line, DiagnosticCodes.TemplatePlaceholderMalformed,
                    $"The template {quoted} opens a placeholder that is never closed (machine-config introduction).");
                literal.Append(character);
                position++;
                continue;
            }

            string braces = Text.Substring(position, close - position + 1);
            Placeholder? placeholder = Placeholder.Read(braces, quoted, line, diagnostics);
            if (placeholder is null)
            {
                literal.Append(braces);
            }
            else
            {
                _literals.Add(literal.ToString());
                literal.Clear();
                _placeholders.Add(placeholder);
            }

            position = close + 1;
        }

        _literals.Add(literal.ToString());
    }
}
