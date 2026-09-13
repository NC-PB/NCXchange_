using Ncx.Config.Templates;
using Ncx.Core.Model;

namespace Ncx.Config.Tests.Templates;

/// <summary>
/// The round trip of a template: rendered from a sample value for each placeholder and matched back to the same
/// values, because the same string describes both directions (architecture 6).
/// </summary>
internal static class TemplateSamples
{
    // The program the templates are rendered for, as the diagnostics name it.
    private const string ProgramFile = "PART.ncx";

    /// <summary>
    /// Renders the template from <see cref="Values"/>, asserts that nothing is reported, matches the text back and
    /// asserts that every placeholder captured the value it was rendered from.
    /// </summary>
    public static void AssertRoundTrip(Template template)
    {
        TemplateValues values = Values(template);
        var diagnostics = new Diagnostics(ProgramFile);
        var block = new Block { Line = 1, Words = [new Word { Key = "TOOL", Value = new IntegerValue(4, "4") }] };

        string? rendered = template.Render(values, block, diagnostics);

        Assert.Empty(diagnostics.Items);
        Assert.NotNull(rendered);
        bool matches = template.Matches(rendered, out TemplateValues captured);
        Assert.True(matches, $"{rendered} does not match its template.");
        foreach (Placeholder placeholder in template.Placeholders)
        {
            if (placeholder.IsText)
            {
                Assert.Equal(Text(values, placeholder.Name), Text(captured, placeholder.Name));
            }
            else
            {
                Assert.Equal(Number(values, placeholder.Name), Number(captured, placeholder.Name));
            }
        }
    }

    /// <summary>
    /// A value for every placeholder: words for the placeholders whose values are words, numbers otherwise, small
    /// enough for the width of a padded placeholder and with decimals where there is no suffix.
    /// </summary>
    public static TemplateValues Values(Template template)
    {
        var values = new TemplateValues();
        int count = 0;
        foreach (Placeholder placeholder in template.Placeholders)
        {
            count++;
            if (placeholder.IsText)
            {
                values.Set(placeholder.Name, SampleText(placeholder.Name));
            }
            else if (placeholder.Width > 0)
            {
                values.Set(placeholder.Name, count);
            }
            else
            {
                values.Set(placeholder.Name, (count * 10) + 0.5m);
            }
        }

        return values;
    }

    // Words as the machine writes them: a tool name, an axis letter, an axis list, the words of MOVE=TURN on a
    // Heidenhain, a channel list, the axis words of a position.
    private static string SampleText(string name)
    {
        return name switch
        {
            "name" => "MILL_D10",
            "axis" => "Z",
            "axes" => "X0 Z0",
            "move" => "TURN FMAX",
            "channels" => "1,2",
            _ => "X0 Z-120",
        };
    }

    private static decimal Number(TemplateValues values, string name)
    {
        Assert.True(values.TryGetNumber(name, out decimal number), $"No number for {name}.");
        return number;
    }

    private static string Text(TemplateValues values, string name)
    {
        Assert.True(values.TryGetText(name, out string? text), $"No text for {name}.");
        return text;
    }
}
