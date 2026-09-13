using Ncx.Config.Templates;
using Ncx.Core.Model;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Serialization;
using Tomlyn.Syntax;

namespace Ncx.Config;

/// <summary>
/// A TOML file parsed for the loaders: its tables with the line of every key and of every [[table]] header, and its
/// syntax errors reported with their line (P2-01). Tomlyn reads the text twice, once into the syntax tree that knows
/// the syntax errors and the headers, once into the tables the loaders walk.
/// </summary>
internal sealed class TomlDocument
{
    private readonly TomlTable _root;
    private readonly TomlMetadataStore _metadata;
    private readonly Dictionary<string, List<int>> _headerLines;

    // The template texts parsed so far, so that each text is parsed once, on the first key that writes it (wave-1
    // question #61).
    private readonly HashSet<string> _checkedTemplates = new(StringComparer.Ordinal);

    private TomlDocument(
        TomlTable root, TomlMetadataStore metadata, Dictionary<string, List<int>> headerLines, Diagnostics diagnostics)
    {
        _root = root;
        _metadata = metadata;
        _headerLines = headerLines;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Where the mistakes of the file are reported.
    /// </summary>
    public Diagnostics Diagnostics { get; }

    /// <summary>
    /// Parses a TOML file; null when it is not valid TOML, which is reported with the line of every error.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <param name="diagnostics">Where the errors are reported; its file names the file.</param>
    public static TomlDocument? Parse(string text, Diagnostics diagnostics)
    {
        // A file that is not TOML reports every syntax error with its line and loads nothing (P2-01); TOML itself
        // forbids a key twice in a table and a leading zero in an integer, ON = 08.
        DocumentSyntax syntax = SyntaxParser.Parse(text, diagnostics.File, true);
        foreach (DiagnosticMessage message in syntax.Diagnostics)
        {
            int line = message.Span.Start.Line + 1;
            string report = $"The file is not valid TOML: {message.Message} (machine-config).";
            if (message.Kind == DiagnosticMessageKind.Error)
            {
                diagnostics.Error(line, DiagnosticCodes.TomlSyntax, report);
            }
            else
            {
                diagnostics.Warning(line, DiagnosticCodes.TomlSyntax, report);
            }
        }

        if (syntax.HasErrors)
        {
            return null;
        }

        var metadata = new TomlMetadataStore();
        var options = new TomlSerializerOptions { MetadataStore = metadata, SourceName = diagnostics.File };
        try
        {
            TomlTable root = TomlSerializer.Deserialize<TomlTable>(text, options) ?? new TomlTable();
            return new TomlDocument(root, metadata, HeaderLines(syntax), diagnostics);
        }
        catch (TomlException exception)
        {
            // What the syntax tree accepts and the tables cannot hold is not valid TOML either.
            diagnostics.Error(exception.Line ?? 1, DiagnosticCodes.TomlSyntax,
                $"The file is not valid TOML: {exception.Message} (machine-config).");
            return null;
        }
    }

    /// <summary>
    /// The number of ERRORs reported so far; a loader gives nothing back when its file added one, because the run
    /// stops on ERROR (virtual machine 2.9).
    /// </summary>
    public static int ErrorCount(Diagnostics diagnostics)
    {
        int errors = 0;
        foreach (Diagnostic diagnostic in diagnostics.Items)
        {
            if (diagnostic.Severity == Severity.Error)
            {
                errors++;
            }
        }

        return errors;
    }

    /// <summary>
    /// The top of the file as a table, without a name where the file is made of tables (the machine file), named after
    /// the file where its keys stand at the top (ncx.toml).
    /// </summary>
    /// <param name="section">The section of the specification that defines the file, cited in the messages.</param>
    /// <param name="name">The name of the top of the file in messages, "ncx.toml"; empty by default.</param>
    public ConfigTable Root(string section, string name = "")
    {
        return new ConfigTable(_root, name, 1, section, this);
    }

    /// <summary>
    /// Parses a template text of the file on the line of the key that writes it, once per text, so that a template
    /// that cannot be parsed is an ERROR on the first line that writes it (machine-config introduction; wave-1
    /// question #61).
    /// </summary>
    /// <param name="template">The template as it is loaded.</param>
    /// <param name="line">The line of its key.</param>
    public void CheckTemplate(string template, int line)
    {
        if (_checkedTemplates.Add(template))
        {
            Template.Check(template, line, Diagnostics);
        }
    }

    /// <summary>
    /// The 1-based line of a key of a table; the fallback when Tomlyn recorded none.
    /// </summary>
    public int LineOf(TomlTable table, string key, int fallback)
    {
        // Tomlyn records the span of every key, dotted keys and keys of inline tables included, and counts lines
        // from 0.
        if (_metadata.TryGetProperties(table, out TomlPropertiesMetadata? properties)
            && properties is not null
            && properties.TryGetProperty(key, out TomlPropertyMetadata? property)
            && property is not null
            && property.Span.Length > 0)
        {
            return property.Span.Start.Line + 1;
        }

        return fallback;
    }

    /// <summary>
    /// The 1-based line of the header of one [[name]] at the top of the file, counted from 0 in file order; the
    /// fallback when there is no such header.
    /// </summary>
    public int HeaderLine(string name, int index, int fallback)
    {
        if (_headerLines.TryGetValue(name, out List<int>? lines) && index < lines.Count)
        {
            return lines[index];
        }

        return fallback;
    }

    // The header line of every [[name]] of the file in order, so that each table of an array reports at its own
    // header (P2-01: a missing required key reports the table).
    private static Dictionary<string, List<int>> HeaderLines(DocumentSyntax syntax)
    {
        var headerLines = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (TableSyntaxBase table in syntax.Tables)
        {
            if (table is not TableArraySyntax || table.Name is null)
            {
                continue;
            }

            string name = table.Name.ToString().Trim();
            if (!headerLines.TryGetValue(name, out List<int>? lines))
            {
                lines = [];
                headerLines[name] = lines;
            }

            lines.Add(table.Span.Start.Line + 1);
        }

        return headerLines;
    }
}
