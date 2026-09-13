using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Config.Templates;

// TODO(question): architecture 6 draws Placeholder with Name, Format and Literal, while the phase file (P2-02) parses
// a template into literal text and placeholders with a name and a format suffix. Literal is left out and the template
// keeps its literal text itself, until the meaning of Literal is settled.
//
// TODO(question): the introduction of machine-config lists the placeholders, while its sections 3 and 5 and the
// example machine files also write {point}, {order}, {channel}, {radius}, {pair} and {ratio}. Any name is accepted as a
// placeholder; whether a name the introduction does not list deserves a diagnostic is open.

/// <summary>
/// One placeholder of a template, a name in braces with an optional format suffix: {tool}, {tool:02},
/// {position:tool_change} (machine-config introduction).
/// </summary>
public sealed record Placeholder
{
    // In expansion rules {position:NAME} stands for the axis words of that entry of [positions] (machine-config
    // introduction and 5a, D100).
    private const string PositionName = "position";
    private const string PositionPrefix = PositionName + ":";

    /// <summary>
    /// The name the value is found by in <see cref="TemplateValues"/>: "tool", "offset"; for a position the entry
    /// belongs to the name, "position:tool_change", since one rule may name two positions (machine-config
    /// introduction).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The format suffix as written, "02" in {tool:02}; empty without one (machine-config introduction).
    /// </summary>
    public string Format { get; init; } = "";

    /// <summary>
    /// The width the format suffix pads numbers to with zeros, 2 for {tool:02}; 0 without a suffix (machine-config
    /// introduction).
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// True when the value is words, not a number: the tool name, the axis letter, the axis list, the words of the
    /// MOVE option, the channel list of a wait, the axis words of a position (machine-config introduction and 5).
    /// </summary>
    public bool IsText
    {
        get
        {
            // TODO(question): the documents give no value kind per placeholder, and the phase file reads every
            // placeholder without a suffix as a signed decimal, which cannot capture the words of {name}
            // (T="{name}"), {axes} (G28 U0 W0), {move} (TURN FMAX) or {channels} (1,2) that the specification's own
            // templates carry. The words are taken from the descriptions of the introduction and of machine-config 5;
            // every other placeholder is a number, {kind} included, which both example machines map to numbers.
            if (Name.StartsWith(PositionPrefix, StringComparison.Ordinal))
            {
                return true;
            }

            return Name is "name" or "axis" or "axes" or "move" or "channels";
        }
    }

    /// <summary>
    /// The placeholder as the template writes it: {tool}, {tool:02}.
    /// </summary>
    public override string ToString()
    {
        if (Format.Length == 0)
        {
            return "{" + Name + "}";
        }

        return "{" + Name + ":" + Format + "}";
    }

    // A placeholder is a name in braces, letters, digits and underscores, with an optional format suffix after a colon
    // that pads numbers with zeros to its width: {tool}, {tool:02}. In {position:NAME} the part after the colon names
    // an entry of [positions] instead (machine-config introduction and 5a). Braces around anything else leave the
    // template unusable and are reported; the caller keeps them as literal text.
    internal static Placeholder? Read(string braces, string quotedTemplate, int line, Diagnostics diagnostics)
    {
        string inside = braces.Substring(1, braces.Length - 2);
        int colon = inside.IndexOf(':');
        string name = colon < 0 ? inside : inside.Substring(0, colon);
        string suffix = colon < 0 ? "" : inside.Substring(colon + 1);
        if (!IsName(name) || (colon >= 0 && suffix.Length == 0))
        {
            return NotAPlaceholder(braces, quotedTemplate, line, diagnostics);
        }

        if (colon < 0)
        {
            return new Placeholder { Name = name };
        }

        // {position:NAME}: the entry of [positions] is a name as well.
        if (name == PositionName)
        {
            if (!IsName(suffix))
            {
                return NotAPlaceholder(braces, quotedTemplate, line, diagnostics);
            }

            return new Placeholder { Name = PositionPrefix + suffix };
        }

        int width = PadWidth(suffix);
        if (width == 0)
        {
            diagnostics.Error(line, DiagnosticCodes.TemplateFormatUnknown,
                $"The template {quotedTemplate} has the format suffix {suffix} in {braces}; a format suffix is a width "
                + "padded with zeros, such as 02 (machine-config introduction).");
            return null;
        }

        return new Placeholder { Name = name, Format = suffix, Width = width };
    }

    // The value as the template writes it: words as given; a number with the decimal point whatever the culture
    // (code-guidelines 3.4), padded with zeros in front to the width of the format suffix, so that {tool:02} writes 04
    // and {tool:02}. writes 04. with the point as literal text (machine-config introduction), and with the decimal
    // separator of the machine, the comma of a Heidenhain file (machine-config 2, controllers heidenhain.md 1). Null
    // when the values have none for this placeholder.
    internal string? Write(TemplateValues values, string decimalSeparator)
    {
        if (values.TryGetText(Name, out string? text))
        {
            return text;
        }

        if (!values.TryGetNumber(Name, out decimal number))
        {
            return null;
        }

        string digits = Math.Abs(number).ToString(CultureInfo.InvariantCulture);
        int point = digits.IndexOf('.');
        int integerDigits = point < 0 ? digits.Length : point;
        if (integerDigits < Width)
        {
            digits = new string('0', Width - integerDigits) + digits;
        }

        string written = number < 0 ? "-" + digits : digits;
        return written.Replace(Template.DecimalPoint, decimalSeparator, StringComparison.Ordinal);
    }

    // Takes the value the pattern found for this placeholder into the values: words as found, a number by its number,
    // so that M0106 gives the mark 106 and T01 the tool 1 (D105), and a number read with the comma of Klartext as the
    // same number with the point (controllers heidenhain.md 7 rule 8); the pattern lets a comma into a number only on a
    // machine that reads it. A placeholder that stands twice stands for one value; a second, different value means the
    // text is not this template's. False when the found text does not fit.
    internal bool Capture(string found, TemplateValues values)
    {
        if (IsText)
        {
            if (values.TryGetText(Name, out string? earlierText) && earlierText != found)
            {
                return false;
            }

            values.Set(Name, found);
            return true;
        }

        string withPoint = found.Replace(Template.DecimalComma, Template.DecimalPoint, StringComparison.Ordinal);
        NumberStyles style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (!decimal.TryParse(withPoint, style, CultureInfo.InvariantCulture, out decimal number))
        {
            return false;
        }

        if (values.TryGetNumber(Name, out decimal earlierNumber) && earlierNumber != number)
        {
            return false;
        }

        values.Set(Name, number);
        return true;
    }

    // Braces around something that is not a name leave the template unusable (machine-config introduction).
    private static Placeholder? NotAPlaceholder(string braces, string quotedTemplate, int line, Diagnostics diagnostics)
    {
        diagnostics.Error(line, DiagnosticCodes.TemplatePlaceholderMalformed,
            $"The template {quotedTemplate} has {braces}, which is not a placeholder: a placeholder is a name in "
            + "braces (machine-config introduction).");
        return null;
    }

    // A name of letters, digits and underscores that does not start with a digit, as every placeholder of the
    // specification is written.
    private static bool IsName(string text)
    {
        if (text.Length == 0 || char.IsAsciiDigit(text[0]))
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    // The width of a format suffix, a zero followed by the width: 02 pads to two digits (machine-config introduction);
    // 0 for any other suffix.
    private static int PadWidth(string suffix)
    {
        if (suffix.Length < 2 || suffix[0] != '0')
        {
            return 0;
        }

        foreach (char character in suffix)
        {
            if (!char.IsAsciiDigit(character))
            {
                return 0;
            }
        }

        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int width) ? width : 0;
    }
}
