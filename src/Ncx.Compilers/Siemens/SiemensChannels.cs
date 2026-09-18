using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The channel words of a SINUMERIK block (controllers siemens.md 9, 12 rule 6; controller-mapping 7; machine-config 5;
/// language 4.8): SYNC through the wait template of [sync], WAITM({mark},{channels}) with the channels of WITH;
/// START_CHANNEL through start_channel with the INIT of the program NAME selects, WAIT_CHANNEL through wait_channel;
/// each in a block of its own.
/// </summary>
internal static class SiemensChannels
{
    /// <summary>
    /// Writes the channel words of the block.
    /// </summary>
    public static void Write(SiemensBlock write)
    {
        SyncConfig? sync = write.Machine.Sync;
        WriteStart(write, sync);
        WriteChannel(write, "WAIT_CHANNEL", sync?.WaitChannel, "[sync] wait_channel");
        WriteWait(write, sync);
    }

    // SYNC=m through the wait template, {channels} the channels of WITH, else every channel of the machine (language
    // 4.8, WITH: default all channels of the job); a mark outside mark_range has no wait (machine-config 5;
    // controller-mapping 7, SYNC and WITH).
    private static void WriteWait(SiemensBlock write, SyncConfig? sync)
    {
        if (write.Block.Find("SYNC")?.Value is not IntegerValue mark)
        {
            return;
        }

        write.Written("SYNC");
        write.Written("WITH");
        if (sync?.Wait is null || (sync.MarkRange is MarkRange range
            && (mark.Number < range.First || mark.Number > range.Last)))
        {
            write.Error(DiagnosticCodes.SiemensMarkNotWritable,
                $"SYNC={mark.Text} has no wait on this machine: [sync] gives no wait template, or the mark lies "
                + "outside its mark_range (machine-config 5; controllers siemens.md 9).");
            return;
        }

        var values = new TemplateValues();
        values.Set("mark", mark.Number);
        values.Set("channels", ChannelsOf(write));
        if (write.Render(sync.Wait, "[sync] wait (machine-config 5)", values) is string text)
        {
            write.Write(text);
        }
    }

    // The channels of WITH as the control lists them, 1,2 (machine-config 5, [sync] wait).
    private static string ChannelsOf(SiemensBlock write)
    {
        Word? with = write.Block.Find("WITH");
        if (with?.Value is ListValue list)
        {
            return string.Join(",", list.Items);
        }

        if (with?.Value is IntegerValue single)
        {
            return single.Text;
        }

        var channels = new List<string>();
        foreach (int channel in write.Machine.Machine.Channels)
        {
            channels.Add(channel.ToString(CultureInfo.InvariantCulture));
        }

        return string.Join(",", channels);
    }

    // START_CHANNEL=n through start_channel; with NAME the program is selected first, INIT(n, "_N_NAME_MPF", "S")
    // (controllers siemens.md 9; language 4.8, START_CHANNEL).
    private static void WriteStart(SiemensBlock write, SyncConfig? sync)
    {
        if (write.Block.Find("START_CHANNEL")?.Value is not IntegerValue channel)
        {
            return;
        }

        if (write.Block.Find("NAME")?.Value is StringValue name)
        {
            write.Written("NAME");
            write.Write($"INIT({channel.Text},\"_N_{name.Content}_MPF\",\"S\")");
        }

        WriteChannel(write, "START_CHANNEL", sync?.StartChannel, "[sync] start_channel");
    }

    private static void WriteChannel(SiemensBlock write, string key, string? template, string what)
    {
        if (write.Block.Find(key)?.Value is not IntegerValue channel)
        {
            return;
        }

        write.Written(key);
        var values = new TemplateValues();
        values.Set("channel", channel.Number);
        if (write.Render(string.IsNullOrEmpty(template) ? null : template, what + " (machine-config 5)", values)
            is string text)
        {
            write.Write(text);
        }
    }
}
