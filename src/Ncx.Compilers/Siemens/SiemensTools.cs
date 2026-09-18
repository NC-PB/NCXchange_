using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The tool words of a SINUMERIK block (controllers siemens.md 5, 12 rule 3; controller-mapping 3; machine-config 3):
/// T and M6 per [tool_change], which the framework writes, with {offset} the D of the holder; D from OFFSET where the
/// change does not carry it, the one register of the cutting edge for length and radius.
/// </summary>
internal static class SiemensTools
{
    // The target key of the active D (controllers siemens.md 5).
    private const string Edge = "D";

    /// <summary>
    /// The values the controller gives the change template itself: {offset}, the D of the holder after the block
    /// (machine-config 3; controller-mapping 3, OFFSET:LEN).
    /// </summary>
    public static TemplateValues ChangeValues(SiemensBlock write)
    {
        var values = new TemplateValues();
        if (EdgeOf(write) is int edge)
        {
            values.Set("offset", edge);
        }

        return values;
    }

    /// <summary>
    /// After the change: the D the template wrote is active, and without {offset} the control selects the edge of the
    /// new tool itself, which the compiler does not know (controllers siemens.md 5).
    /// </summary>
    public static void AfterChange(SiemensBlock write, string? template)
    {
        if (write.HasPlaceholder(template, "offset") && EdgeOf(write) is int edge)
        {
            write.Target.Set(Edge, edge.ToString(CultureInfo.InvariantCulture));
            return;
        }

        write.MakeUnknown(Edge);
    }

    /// <summary>
    /// D from OFFSET in the main line where the control has another edge active and the change of the block did not
    /// write it: D0 in the retract block G0 G53 Z360 D0 (controllers siemens.md 12 rule 3; controller-mapping 1,
    /// FRAME=MACHINE, and 3, OFFSET:LEN).
    /// </summary>
    public static void WriteOffset(SiemensBlock write)
    {
        if (!write.Block.Has("OFFSET"))
        {
            return;
        }

        write.Written("OFFSET");
        if (EdgeOf(write) is int edge && write.Target.Changes(Edge, edge.ToString(CultureInfo.InvariantCulture)))
        {
            write.Main.Word(SiemensLine.OffsetRank, "D" + edge.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// The D of the holder of the last TOOL after the block: OFFSET, the combined register, where the program writes
    /// it; else OFFSET:LEN and OFFSET:RAD, which on this control are one register (controller-mapping 3, OFFSET:LEN;
    /// language 4.4, OFFSET).
    /// </summary>
    // TODO(question): controller-mapping 3 gives the D of the control as one register for length and radius and says
    // nothing of OFFSET:LEN and OFFSET:RAD that name two; the length register is written, the radius register where
    // the length is 0, with a WARNING, until that is answered.
    public static int? EdgeOf(SiemensBlock write)
    {
        ChannelSnapshot after = write.After;
        if (after.LastHolder is not string holder || !after.Holders.TryGetValue(holder, out HolderSnapshot? state))
        {
            return null;
        }

        if (write.Block.Find("OFFSET", null) is not null || state.OffsetCombined != 0)
        {
            return state.OffsetCombined;
        }

        return state.OffsetLen != 0 ? state.OffsetLen : state.OffsetRad;
    }

    /// <summary>
    /// A WARNING where OFFSET:LEN and OFFSET:RAD of the block leave the holder with two registers, of which the one D
    /// of the control carries the length (controller-mapping 3, OFFSET:LEN).
    /// </summary>
    public static void ReportTwoRegisters(SiemensBlock write)
    {
        ChannelSnapshot after = write.After;
        if ((write.Block.Find("OFFSET", "LEN") is null && write.Block.Find("OFFSET", "RAD") is null)
            || after.LastHolder is not string holder || !after.Holders.TryGetValue(holder, out HolderSnapshot? state)
            || state.OffsetCombined != 0 || state.OffsetLen == state.OffsetRad || state.OffsetLen == 0
            || state.OffsetRad == 0)
        {
            return;
        }

        write.Warning(DiagnosticCodes.SiemensOffsetRegistersDiffer,
            $"OFFSET:LEN={state.OffsetLen.ToString(CultureInfo.InvariantCulture)} and OFFSET:RAD="
            + $"{state.OffsetRad.ToString(CultureInfo.InvariantCulture)} name two registers, and the D of the control "
            + $"is one register for length and radius; D{state.OffsetLen.ToString(CultureInfo.InvariantCulture)} is "
            + "written (controller-mapping 3, OFFSET:LEN; controllers siemens.md 5).");
    }
}
