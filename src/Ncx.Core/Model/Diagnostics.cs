using System.Text;

namespace Ncx.Core.Model;

/// <summary>
/// The diagnostics a stage collects while it reads, checks or writes one file. Anything the user can cause is
/// reported here and never thrown, and the stage continues where the specification allows (code-guidelines 6, D98).
/// </summary>
public sealed class Diagnostics
{
    private readonly List<Diagnostic> _items = [];

    /// <summary>
    /// Starts an empty list for one file.
    /// </summary>
    /// <param name="file">The file that Error, Warning and Info report on; every diagnostic carries it (D98).</param>
    public Diagnostics(string file)
    {
        File = file;
    }

    /// <summary>
    /// The file that Error, Warning and Info report on.
    /// </summary>
    public string File { get; }

    /// <summary>
    /// Every diagnostic in the order it was reported.
    /// </summary>
    public IReadOnlyList<Diagnostic> Items => _items;

    /// <summary>
    /// True when at least one diagnostic is an ERROR, which stops the run (virtual machine 2.9).
    /// </summary>
    public bool HasErrors
    {
        get
        {
            foreach (Diagnostic diagnostic in _items)
            {
                if (diagnostic.Severity == Severity.Error)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Reports an ERROR on a line of the file: the run stops (virtual machine 2.9).
    /// </summary>
    /// <param name="line">The 1-based line of the block.</param>
    /// <param name="code">The code of the rule, a constant of DiagnosticCodes.</param>
    /// <param name="message">The message, naming the rule and the block.</param>
    public void Error(int line, string code, string message)
    {
        Report(Severity.Error, line, null, code, message);
    }

    /// <summary>
    /// Reports a WARNING on a line of the file: the run continues (virtual machine 2.9).
    /// </summary>
    /// <param name="line">The 1-based line of the block.</param>
    /// <param name="code">The code of the rule, a constant of DiagnosticCodes.</param>
    /// <param name="message">The message, naming the rule and the block.</param>
    public void Warning(int line, string code, string message)
    {
        Report(Severity.Warning, line, null, code, message);
    }

    /// <summary>
    /// Reports an INFO on a line of the file, a note that changes nothing (virtual machine 2.9).
    /// </summary>
    /// <param name="line">The 1-based line of the block.</param>
    /// <param name="code">The code of the note, a constant of DiagnosticCodes.</param>
    /// <param name="message">The note.</param>
    public void Info(int line, string code, string message)
    {
        Report(Severity.Info, line, null, code, message);
    }

    /// <summary>
    /// Reports an ERROR on a block; on a generated block the diagnostic carries the line of the block it was
    /// generated for (D98).
    /// </summary>
    /// <param name="block">The block the rule is about.</param>
    /// <param name="code">The code of the rule, a constant of DiagnosticCodes.</param>
    /// <param name="message">The message, naming the rule and the block.</param>
    public void Error(Block block, string code, string message)
    {
        Report(Severity.Error, block.Line, block.OriginLine, code, message);
    }

    /// <summary>
    /// Reports a WARNING on a block; on a generated block the diagnostic carries the line of the block it was
    /// generated for (D98).
    /// </summary>
    /// <param name="block">The block the rule is about.</param>
    /// <param name="code">The code of the rule, a constant of DiagnosticCodes.</param>
    /// <param name="message">The message, naming the rule and the block.</param>
    public void Warning(Block block, string code, string message)
    {
        Report(Severity.Warning, block.Line, block.OriginLine, code, message);
    }

    /// <summary>
    /// Reports an INFO on a block; on a generated block the diagnostic carries the line of the block it was
    /// generated for (D98).
    /// </summary>
    /// <param name="block">The block the note is about.</param>
    /// <param name="code">The code of the note, a constant of DiagnosticCodes.</param>
    /// <param name="message">The note.</param>
    public void Info(Block block, string code, string message)
    {
        Report(Severity.Info, block.Line, block.OriginLine, code, message);
    }

    /// <summary>
    /// Adds a diagnostic as it is, such as one about another file: a called program (virtual machine 2.9).
    /// </summary>
    /// <param name="diagnostic">The diagnostic, with its own file.</param>
    public void Add(Diagnostic diagnostic)
    {
        _items.Add(diagnostic);
    }

    /// <summary>
    /// The diagnostics as the user reads them, one line each in the order they were reported, every line ended with
    /// LF: file(line): ERROR VM042: message (D98).
    /// </summary>
    public string ToText()
    {
        var text = new StringBuilder();
        foreach (Diagnostic diagnostic in _items)
        {
            text.Append(diagnostic.ToText()).Append('\n');
        }

        return text.ToString();
    }

    // Every diagnostic of this list is about its file; a diagnostic on a generated block also names the line of the
    // block it was generated for (D98).
    private void Report(Severity severity, int line, int? originLine, string code, string message)
    {
        _items.Add(new Diagnostic
        {
            Severity = severity,
            File = File,
            Line = line,
            OriginLine = originLine,
            Code = code,
            Message = message,
        });
    }
}
