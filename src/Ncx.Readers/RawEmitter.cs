using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Writing;

namespace Ncx.Readers;

/// <summary>
/// Keeps a source block that NCX cannot express as RAW:controller or RAW:builder with its source text verbatim and a
/// WARNING, so that nothing is dropped (D5; language 4.1, RAW; controller-mapping 9).
/// </summary>
internal static class RawEmitter
{
    /// <summary>
    /// The dialect the RAW word of a block is written with: the builder of the machine when the block holds a code
    /// that the [raw] table names, RAW:NAKAMURA, and the controller family otherwise, RAW:FANUC (machine-config 1 and
    /// 5, language 4.1).
    /// </summary>
    /// <param name="block">The source block.</param>
    /// <param name="machine">The machine, its builder and its [raw] table.</param>
    /// <param name="controller">The controller family of the reader.</param>
    public static string DialectOf(SourceBlock block, MachineConfig machine, Controller controller)
    {
        // The builder codes the reader keeps as RAW carry the builder's name, codes compared by number (machine-config
        // 5, [raw]; D105); NCX writes the address in capitals (language 3).
        string? builder = machine.Machine.Builder;
        if (!string.IsNullOrEmpty(builder) && HoldsBuilderCode(block, machine.Raw))
        {
            return builder.ToUpperInvariant();
        }

        return controller.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// Tells whether a word is a code that the [raw] table of the machine names, compared by number: G0411 is the
    /// G411 of the table (machine-config 5, D105).
    /// </summary>
    /// <param name="word">The source word.</param>
    /// <param name="raw">The [raw] table; null when the machine file has none.</param>
    public static bool IsBuilderCode(SourceWord word, RawTable? raw)
    {
        if (raw is null)
        {
            return false;
        }

        string code = NativeCode.Of(word) ?? word.Address;
        foreach (string known in raw.Known)
        {
            if (NativeCode.SameCode(known, code))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes the block as RAW blocks, the text of each of its lines verbatim, and reports one WARNING for it (D5).
    /// </summary>
    /// <param name="builder">The builder of the program; no block is open.</param>
    /// <param name="block">The source block.</param>
    /// <param name="dialect">The controller or builder of the RAW word, "FANUC", "NAKAMURA".</param>
    /// <param name="reason">Why the block cannot be read, in the words of the machine: "G10 writes the datum table".
    /// </param>
    /// <param name="diagnostics">The diagnostics of the file.</param>
    public static void Emit(NcxBuilder builder, SourceBlock block, string dialect, string reason,
        Diagnostics diagnostics)
    {
        // The source text is kept verbatim, block skip, block number and comment included, since the RAW word is the
        // text the controller reads back (language 4.1). A block the controller continues over several lines is one
        // RAW block per line, because an NCX string holds no line break (language 3, string). The builder adds RAW as
        // a word of the block begun (wave-1 question #78), so a RAW block is Begin(line).Raw(dialect, text).End().
        builder.Begin(block.Line).Raw(dialect, block.Text).End();
        for (int index = 0; index < block.Continuation.Count; index++)
        {
            builder.Begin(block.Line + index + 1).Raw(dialect, block.Continuation[index]).End();
        }

        // Each RAW is a WARNING at read time and compiles only to the same controller or builder (controller-mapping
        // 9, D5).
        diagnostics.Warning(block.Line, DiagnosticCodes.KeptAsRaw,
            $"The block is kept as RAW:{dialect}: {reason}; it compiles only to the same controller or builder (D5, "
            + "controller-mapping 9).");
    }

    private static bool HoldsBuilderCode(SourceBlock block, RawTable? raw)
    {
        foreach (SourceWord word in block.Words)
        {
            if (IsBuilderCode(word, raw))
            {
                return true;
            }
        }

        return false;
    }
}
