using Ncx.Core.Model;

namespace Ncx.Cli.Commands;

/// <summary>
/// One file of ncx convert --batch as its report counts it (implementation 13, P3-07): what became of it, its blocks,
/// its RAW blocks per word and its diagnostics per code. It holds counts and codes only, never a line of the program,
/// which may be the property of a customer (implementation 13, risks).
/// </summary>
internal sealed record BatchFile
{
    /// <summary>
    /// The file by its path from the folder of --batch, with forward slashes: "fanuc/O1000.nc".
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What became of the file.
    /// </summary>
    public required BatchOutcome Outcome { get; init; }

    /// <summary>
    /// The name of the exception a crashed conversion stopped with, "IndexOutOfRangeException"; null otherwise.
    /// </summary>
    public string? Crash { get; init; }

    /// <summary>
    /// The blocks of the NCX program the reader produced; 0 when it produced none.
    /// </summary>
    public int Blocks { get; init; }

    /// <summary>
    /// The RAW blocks of the program by their RAW word: "RAW:FANUC" and "RAW:NAKAMURA" (D5; language 4.1, RAW).
    /// </summary>
    public required IReadOnlyDictionary<string, int> RawBlocks { get; init; }

    /// <summary>
    /// The diagnostics of the file by their code, those of the reader and of the check and those of the batch.
    /// </summary>
    public required IReadOnlyDictionary<string, int> Codes { get; init; }

    /// <summary>
    /// The severity of each code of <see cref="Codes"/>.
    /// </summary>
    public required IReadOnlyDictionary<string, Severity> Severities { get; init; }

    /// <summary>
    /// Counts one file.
    /// </summary>
    /// <param name="name">The file by its path from the folder of --batch.</param>
    /// <param name="outcome">What became of it.</param>
    /// <param name="crash">The name of the exception of a crash; null otherwise.</param>
    /// <param name="program">The program the reader produced; null when there is none.</param>
    /// <param name="diagnostics">What was reported on the file.</param>
    public static BatchFile Of(string name, BatchOutcome outcome, string? crash, NcxProgram? program,
        Diagnostics diagnostics)
    {
        // A RAW block holds one RAW word, RAW:<CONTROLLER> or RAW:<BUILDER>, and the report counts the blocks by it
        // (D5; language 4.1, RAW).
        var rawBlocks = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Block block in program?.Blocks ?? [])
        {
            if (block.Find("RAW") is Word raw)
            {
                string word = raw.Addr is null ? raw.Key : raw.Key + ":" + raw.Addr;
                rawBlocks[word] = rawBlocks.GetValueOrDefault(word) + 1;
            }
        }

        var codes = new Dictionary<string, int>(StringComparer.Ordinal);
        var severities = new Dictionary<string, Severity>(StringComparer.Ordinal);
        foreach (Diagnostic diagnostic in diagnostics.Items)
        {
            codes[diagnostic.Code] = codes.GetValueOrDefault(diagnostic.Code) + 1;
            severities.TryAdd(diagnostic.Code, diagnostic.Severity);
        }

        return new BatchFile
        {
            Name = name,
            Outcome = outcome,
            Crash = crash,
            Blocks = program?.Blocks.Count ?? 0,
            RawBlocks = rawBlocks,
            Codes = codes,
            Severities = severities,
        };
    }
}
