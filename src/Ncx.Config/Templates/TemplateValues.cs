using System.Diagnostics.CodeAnalysis;

namespace Ncx.Config.Templates;

/// <summary>
/// The values of the placeholders of a template, by placeholder name: numbers (a tool, an offset, a mark, a speed, a
/// coordinate) and words (a tool name, an axis letter, an axis list). A compiler fills it to render a template, and a
/// match fills it with what a reader found (machine-config introduction, architecture 6).
/// </summary>
public sealed class TemplateValues
{
    private readonly Dictionary<string, decimal> _numbers = new();
    private readonly Dictionary<string, string> _texts = new();

    /// <summary>
    /// Sets a number, replacing any value of the name; an int is taken as it is: <c>values.Set("tool", 4)</c>.
    /// </summary>
    /// <param name="name">The placeholder name, "tool" for {tool} and {tool:02}.</param>
    /// <param name="number">The number.</param>
    public void Set(string name, decimal number)
    {
        _texts.Remove(name);
        _numbers[name] = number;
    }

    /// <summary>
    /// Sets words, replacing any value of the name: <c>values.Set("axes", "U0 W0")</c>.
    /// </summary>
    /// <param name="name">The placeholder name, "axes" for {axes}.</param>
    /// <param name="text">The words as the machine writes them.</param>
    public void Set(string name, string text)
    {
        _numbers.Remove(name);
        _texts[name] = text;
    }

    /// <summary>
    /// True when a number or words are set for the name.
    /// </summary>
    /// <param name="name">The placeholder name.</param>
    public bool Has(string name)
    {
        return _numbers.ContainsKey(name) || _texts.ContainsKey(name);
    }

    /// <summary>
    /// Gets the number set for the name.
    /// </summary>
    /// <param name="name">The placeholder name.</param>
    /// <param name="number">The number; 0 when there is none.</param>
    /// <returns>False when no number is set for the name, also when its value is words.</returns>
    public bool TryGetNumber(string name, out decimal number)
    {
        return _numbers.TryGetValue(name, out number);
    }

    /// <summary>
    /// Gets the words set for the name.
    /// </summary>
    /// <param name="name">The placeholder name.</param>
    /// <param name="text">The words; null when there are none.</param>
    /// <returns>False when no words are set for the name, also when its value is a number.</returns>
    public bool TryGetText(string name, [NotNullWhen(true)] out string? text)
    {
        return _texts.TryGetValue(name, out text);
    }
}
