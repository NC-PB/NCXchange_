using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The wait marks of several paths (controllers fanuc.md 8, 10 rule 4; controller-mapping 7; machine-config 5; language
/// 4.8): SYNC=m through the wait template of [sync], M{mark}, with the paths of WITH as a list (P12) or a bitmask (P3)
/// per [sync] paths; START_CHANNEL and WAIT_CHANNEL through their templates.
/// </summary>
internal static class FanucSync
{
    /// <summary>
    /// The wait mark of the block in a line of its own.
    /// </summary>
    public static void Write(FanucBlock write)
    {
        Block block = write.Block;
        WriteChannel(write, "START_CHANNEL", write.Machine.Sync?.StartChannel, "[sync] start_channel");
        WriteChannel(write, "WAIT_CHANNEL", write.Machine.Sync?.WaitChannel, "[sync] wait_channel");
        if (block.Find("SYNC")?.Value is not IntegerValue mark)
        {
            return;
        }

        write.Written("SYNC");
        write.Written("WITH");
        SyncConfig? sync = write.Machine.Sync;
        if (sync?.Wait is null || (sync.MarkRange is MarkRange range
            && (mark.Number < range.First || mark.Number > range.Last)))
        {
            write.Error(DiagnosticCodes.FanucMarkNotWritable,
                $"SYNC={mark.Text} has no wait code on this machine: [sync] gives no wait template, or the mark lies "
                + "outside its mark_range (machine-config 5; controllers fanuc.md 8).");
            return;
        }

        var values = new TemplateValues();
        values.Set("mark", mark.Number);
        values.Set("paths", PathsOf(write, sync));
        if (write.Render(sync.Wait, "[sync] wait (machine-config 5)", values) is string text)
        {
            write.Write(text);
        }
    }

    // The participating paths of WITH, all channels of the machine without it (language 4.8: default all channels of
    // the job), as a path list P12 or a bitmask P3 (controllers fanuc.md 8; controller-mapping 7).
    private static decimal PathsOf(FanucBlock write, SyncConfig sync)
    {
        var channels = new List<int>();
        if (write.Block.Find("WITH")?.Value is ListValue list)
        {
            foreach (string item in list.Items)
            {
                channels.Add(int.Parse(item, CultureInfo.InvariantCulture));
            }
        }
        else if (write.Block.Find("WITH")?.Value is IntegerValue single)
        {
            channels.Add((int)single.Number);
        }
        else
        {
            channels.AddRange(write.Machine.Machine.Channels);
        }

        decimal paths = 0m;
        foreach (int channel in channels)
        {
            paths = sync.Paths == SyncPaths.Bitmask ? paths + (1 << (channel - 1)) : (paths * 10m) + channel;
        }

        return paths;
    }

    private static void WriteChannel(FanucBlock write, string key, string? template, string what)
    {
        if (write.Block.Find(key)?.Value is not IntegerValue channel)
        {
            return;
        }

        write.Written(key);
        write.Written("NAME");
        var values = new TemplateValues();
        values.Set("channel", channel.Number);
        if (write.Render(string.IsNullOrEmpty(template) ? null : template, what + " (machine-config 5)", values)
            is string text)
        {
            write.Write(text);
        }
    }
}
