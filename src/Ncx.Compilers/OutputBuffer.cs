using System.Globalization;
using System.Text;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers;

// TODO(question): machine-config 2 gives the [format] keys without saying what a file that leaves one out writes,
// and what a line longer than max_line_length does. Without line_ending the lines end with LF, as the canonical writer
// ends the lines of a program that was never a file; without block_numbers no line is numbered, and block_numbers
// enabled without start or step counts from 10 in steps of 10, the values of its example; a longer line is kept as it
// is with a WARNING, until that is answered. Without decimal_separator the separator of the controller family applies
// (NumberFormatter, wave-1 question #64).

/// <summary>
/// The lines of one output file and how [format] writes them (machine-config 2): the block numbers of block_numbers,
/// the WARNING of max_line_length, the line ending of line_ending. The lines come in the order of the file; each
/// carries the NCX block it was written for.
/// </summary>
internal sealed class OutputBuffer
{
    private const string CrLf = "\r\n";
    private const string Lf = "\n";

    // block_numbers = { enabled = true, start = 10, step = 10 } (machine-config 2).
    private const int DefaultStart = 10;
    private const int DefaultStep = 10;

    private readonly List<OutputLine> _lines = [];
    private readonly OutputFormat? _format;
    private readonly string _blockNumberPrefix;
    private readonly Func<string, bool> _takesBlockNumber;
    private readonly Func<string, int?>? _ownBlockNumber;

    /// <summary>
    /// An empty output file.
    /// </summary>
    /// <param name="format">[format] of the machine; null when the file has none.</param>
    /// <param name="blockNumberPrefix">What stands before a block number, "N" on Fanuc, nothing on Heidenhain.</param>
    /// <param name="takesBlockNumber">Whether a line gets a block number: not the % and O lines of Fanuc, not the
    /// continuation lines of Klartext.</param>
    /// <param name="ownBlockNumber">The block number a line carries itself, the label N20 of a Fanuc program, which
    /// the numbering gives no other line; null for none.</param>
    public OutputBuffer(OutputFormat? format, string blockNumberPrefix, Func<string, bool> takesBlockNumber,
        Func<string, int?>? ownBlockNumber = null)
    {
        _format = format;
        _blockNumberPrefix = blockNumberPrefix;
        _takesBlockNumber = takesBlockNumber;
        _ownBlockNumber = ownBlockNumber;
    }

    /// <summary>
    /// The number of lines so far.
    /// </summary>
    public int Count => _lines.Count;

    /// <summary>
    /// Adds one line for a block.
    /// </summary>
    /// <param name="text">The line without block number and line ending.</param>
    /// <param name="block">The block it is written for.</param>
    public void Line(string text, Block block)
    {
        _lines.Add(new OutputLine(text, block));
    }

    /// <summary>
    /// Adds lines in their order.
    /// </summary>
    public void Add(IEnumerable<OutputLine> lines)
    {
        _lines.AddRange(lines);
    }

    /// <summary>
    /// The text of the file: every line numbered per block_numbers where it takes a number, checked against
    /// max_line_length, and ended with the line_ending of the machine (machine-config 2).
    /// </summary>
    /// <param name="diagnostics">Where the WARNING about a line that is too long goes.</param>
    public string ToText(Diagnostics diagnostics)
    {
        BlockNumbering? numbering = _format?.BlockNumbers is { Enabled: true } enabled ? enabled : null;
        int number = numbering?.Start ?? DefaultStart;
        int step = numbering?.Step ?? DefaultStep;
        int longest = _format?.MaxLineLength ?? 0;
        string lineEnding = _format?.LineEnding == LineEnding.CrLf ? CrLf : Lf;
        HashSet<int> taken = OwnNumbers(numbering);
        var text = new StringBuilder();
        foreach (OutputLine line in _lines)
        {
            // Block numbers per block_numbers, from start in steps of step, on the lines that take one; a number that
            // a line of the file carries itself is passed over, so that it names one block.
            string written = line.Text;
            if (numbering is not null && _takesBlockNumber(line.Text))
            {
                while (step > 0 && taken.Contains(number))
                {
                    number += step;
                }

                string blockNumber = _blockNumberPrefix + number.ToString(CultureInfo.InvariantCulture);
                written = written.Length == 0 ? blockNumber : blockNumber + " " + written;
                number += step;
            }

            // max_line_length is the longest line the control takes, 0 for unlimited (machine-config 2).
            if (longest > 0 && written.Length > longest)
            {
                diagnostics.Warning(line.Block, DiagnosticCodes.LineLongerThanTheMachineTakes,
                    $"The line \"{written}\" has {written.Length.ToString(CultureInfo.InvariantCulture)} characters, "
                    + $"more than max_line_length = {longest.ToString(CultureInfo.InvariantCulture)} of the machine "
                    + "(machine-config 2).");
            }

            text.Append(written).Append(lineEnding);
        }

        return text.ToString();
    }

    // The block numbers the lines of the file carry themselves, which the numbering gives no other line; none without
    // block_numbers (machine-config 2).
    private HashSet<int> OwnNumbers(BlockNumbering? numbering)
    {
        var taken = new HashSet<int>();
        if (numbering is null || _ownBlockNumber is null)
        {
            return taken;
        }

        foreach (OutputLine line in _lines)
        {
            if (!_takesBlockNumber(line.Text) && _ownBlockNumber(line.Text) is int own)
            {
                taken.Add(own);
            }
        }

        return taken;
    }
}
