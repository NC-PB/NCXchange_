using System.Globalization;
using System.Text;
using Ncx.Core.Model;

namespace Ncx.Cli.Commands;

/// <summary>
/// The summary that ncx convert --batch writes into the file of --report (implementation 13, P3-07): the machine, the
/// number of files by what became of them, then per file its result, its blocks, its RAW blocks per word and its
/// diagnostics per code, and the totals. Counts, codes and the names of the files only, never a line of a program, so
/// that the maintainer can commit the report of a corpus that is customer property (implementation 13, risks;
/// tests/corpus-reports/README.md). The text is the same for the same files, so that two reports compare with a diff.
/// </summary>
internal static class BatchReport
{
    // The blanks between two columns.
    private const string Gap = "  ";

    // A column without a value.
    private const string None = "-";

    // The heads of the two tables, and which of their columns hold numbers, aligned to the right.
    private static readonly string[] s_fileHead = ["file", "result", "blocks", "RAW", "diagnostics"];
    private static readonly bool[] s_fileNumbers = [false, false, true, false, false];
    private static readonly string[] s_totalHead = ["total", "count", "files"];
    private static readonly bool[] s_totalNumbers = [false, true, true];

    /// <summary>
    /// The text of the report.
    /// </summary>
    /// <param name="machine">The machine the files were read for, as the head of the report names it.</param>
    /// <param name="files">The files, in the order they were converted.</param>
    public static string Write(string machine, IReadOnlyList<BatchFile> files)
    {
        // TODO(question): implementation 13 asks for "a summary per file with blocks, RAW blocks per word, diagnostics
        // per code, and totals" and names neither its form nor the word a RAW block is counted by. The report is
        // aligned text, the files in the order of their paths, the words and codes in ordinal order, and a RAW block
        // counted by its RAW word, RAW:FANUC or RAW:NAKAMURA, since the report holds no source text; the totals give
        // the files each word and code stands in. Until that is answered.
        var text = new StringBuilder();
        text.Append("ncx convert --batch (implementation 13, P3-07)\n");
        text.Append("machine: ").Append(machine).Append('\n');
        text.Append("files: ").Append(Number(files.Count))
            .Append(", converted ").Append(Number(Count(files, BatchOutcome.Converted)))
            .Append(", unreadable ").Append(Number(Count(files, BatchOutcome.Unreadable)))
            .Append(", crashed ").Append(Number(Count(files, BatchOutcome.Crashed))).Append('\n');

        text.Append('\n');
        AppendTable(text, FileRows(files), s_fileNumbers);
        text.Append('\n');
        AppendTable(text, TotalRows(files), s_totalNumbers);
        return text.ToString();
    }

    // One row per file: its name, its result, its blocks, its RAW blocks per word, its diagnostics per code.
    private static List<string[]> FileRows(IReadOnlyList<BatchFile> files)
    {
        var rows = new List<string[]> { s_fileHead };
        foreach (BatchFile file in files)
        {
            rows.Add(
            [
                file.Name,
                ResultOf(file),
                file.Blocks.ToString(CultureInfo.InvariantCulture),
                Counts(file.RawBlocks),
                Counts(file.Codes),
            ]);
        }

        return rows;
    }

    // The totals: the blocks, every RAW word and every code with its severity, each with its count and the number of
    // files it stands in.
    private static List<string[]> TotalRows(IReadOnlyList<BatchFile> files)
    {
        var rows = new List<string[]> { s_totalHead };
        int blocks = 0;
        int filesWithBlocks = 0;
        var rawBlocks = new Dictionary<string, int>(StringComparer.Ordinal);
        var rawFiles = new Dictionary<string, int>(StringComparer.Ordinal);
        var codes = new Dictionary<string, int>(StringComparer.Ordinal);
        var codeFiles = new Dictionary<string, int>(StringComparer.Ordinal);
        var severities = new Dictionary<string, Severity>(StringComparer.Ordinal);
        foreach (BatchFile file in files)
        {
            blocks += file.Blocks;
            filesWithBlocks += file.Blocks > 0 ? 1 : 0;
            AddUp(file.RawBlocks, rawBlocks, rawFiles);
            AddUp(file.Codes, codes, codeFiles);
            foreach (KeyValuePair<string, Severity> severity in file.Severities)
            {
                severities.TryAdd(severity.Key, severity.Value);
            }
        }

        rows.Add(["blocks", Number(blocks), Number(filesWithBlocks)]);
        foreach (string word in Sorted(rawBlocks))
        {
            rows.Add([word, Number(rawBlocks[word]), Number(rawFiles[word])]);
        }

        foreach (string code in Sorted(codes))
        {
            string severity = severities[code].ToString().ToUpperInvariant();
            rows.Add([code + " " + severity, Number(codes[code]), Number(codeFiles[code])]);
        }

        return rows;
    }

    // The result column: converted, unreadable, or crashed with the name of the exception.
    private static string ResultOf(BatchFile file)
    {
        return file.Outcome switch
        {
            BatchOutcome.Converted => "converted",
            BatchOutcome.Unreadable => "unreadable",
            _ => "crashed " + file.Crash,
        };
    }

    // The counts of one file, "RAW:FANUC 2, RAW:NAKAMURA 5", in ordinal order; - for none.
    private static string Counts(IReadOnlyDictionary<string, int> counts)
    {
        var parts = new List<string>();
        foreach (string key in Sorted(counts))
        {
            parts.Add(key + " " + Number(counts[key]));
        }

        return parts.Count == 0 ? None : string.Join(", ", parts);
    }

    // The counts of one file added to the totals, and the file counted once for every key it has.
    private static void AddUp(IReadOnlyDictionary<string, int> counts, Dictionary<string, int> totals,
        Dictionary<string, int> files)
    {
        foreach (KeyValuePair<string, int> count in counts)
        {
            totals[count.Key] = totals.GetValueOrDefault(count.Key) + count.Value;
            files[count.Key] = files.GetValueOrDefault(count.Key) + 1;
        }
    }

    private static List<string> Sorted(IReadOnlyDictionary<string, int> counts)
    {
        var keys = new List<string>(counts.Keys);
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }

    private static int Count(IReadOnlyList<BatchFile> files, BatchOutcome outcome)
    {
        int count = 0;
        foreach (BatchFile file in files)
        {
            count += file.Outcome == outcome ? 1 : 0;
        }

        return count;
    }

    private static string Number(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    // The rows as columns two blanks apart, the numbers aligned to the right, without blanks at the end of a line.
    private static void AppendTable(StringBuilder text, List<string[]> rows, bool[] numeric)
    {
        int[] widths = new int[numeric.Length];
        foreach (string[] row in rows)
        {
            for (int column = 0; column < row.Length; column++)
            {
                widths[column] = Math.Max(widths[column], row[column].Length);
            }
        }

        foreach (string[] row in rows)
        {
            var line = new StringBuilder();
            for (int column = 0; column < row.Length; column++)
            {
                line.Append(column == 0 ? "" : Gap);
                string cell = row[column];
                line.Append(numeric[column] ? cell.PadLeft(widths[column]) : cell.PadRight(widths[column]));
            }

            text.Append(line.ToString().TrimEnd()).Append('\n');
        }
    }
}
