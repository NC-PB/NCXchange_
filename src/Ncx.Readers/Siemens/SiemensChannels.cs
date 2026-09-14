using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The channel words of a SINUMERIK block (controllers siemens.md 9, 11 rule 7; controller-mapping 7; language 4.8):
/// WAITM(mark, channels) and WAITMC as SYNC=mark WITH=channels; INIT(channel, "program", "S") selects the program that
/// the START of the channel names, START(channel) as START_CHANNEL, WAITE(channel) as WAIT_CHANNEL; GROUP_BEGIN as a
/// SECTION with its text (controller-mapping 5, catalog cycles).
/// </summary>
internal static class SiemensChannels
{
    /// <summary>
    /// Reads the channel words of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        foreach (SourceWord word in block.Unread())
        {
            if (!word.Text.StartsWith('('))
            {
                continue;
            }

            switch (word.Address)
            {
                case "WAITM" or "WAITMC":
                    ReadWait(block, word);
                    return;
                case "INIT":
                    ReadInit(block, word);
                    return;
                case "START" or "WAITE":
                    ReadChannels(block, word);
                    return;
                case "GROUP_BEGIN":
                    ReadGroup(block, word);
                    return;
                case "GROUP_END":
                    // TODO(question): controller-mapping 5 and 9 read GROUP_BEGIN and GROUP_END as SECTION, and
                    // GROUP_END carries no text; it stays RAW.
                    block.MarkRead(word);
                    block.Draft.KeepAsRaw("GROUP_END closes a program group, which NCX writes as no SECTION of its own "
                        + "(controller-mapping 5)");
                    block.Draft.RawKeepsState = true;
                    return;
            }
        }
    }

    // WAITM(1, 1, 2) sets mark 1 and waits for channels 1 and 2, the own channel need not be listed; WAITMC brakes only
    // when the others are not there yet and reads the same; channels given by variables stay RAW (controller-mapping
    // 7, SYNC and WITH).
    private static void ReadWait(SiemensBlock block, SourceWord word)
    {
        block.MarkRead(word);
        List<string> arguments = SiemensArguments.Split(word.Text);
        int? mark = arguments.Count > 0 ? SiemensNumbers.WholeNumber(arguments[0]) : null;
        var channels = new List<string>();
        for (int index = 1; index < arguments.Count; index++)
        {
            if (SiemensNumbers.WholeNumber(arguments[index]) is not int channel || channel < 1)
            {
                block.Draft.KeepAsRaw($"{word.Address} names its channels by variables or not as numbers "
                    + "(controller-mapping 7, SYNC)");
                return;
            }

            channels.Add(channel.ToString(CultureInfo.InvariantCulture));
        }

        if (mark is not int number || number < 0)
        {
            block.Draft.KeepAsRaw($"{word.Address} names no mark 0 to 99 (controller-mapping 7, SYNC)");
            return;
        }

        block.Draft.AddState("SYNC", null, new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture)));
        if (channels.Count > 0)
        {
            block.Draft.AddState("WITH", null, new ListValue(channels));
        }
    }

    // INIT(2, "/_N_WKS_DIR/_N_SHAFT_WPD/_N_SHAFT2_MPF", "S") selects the program SHAFT2 in channel 2, which the START
    // of the channel names, START_CHANNEL=2 NAME="SHAFT2"; an INIT no START of its program follows stays RAW
    // (controllers siemens.md 9; language 4.8, START_CHANNEL).
    private static void ReadInit(SiemensBlock block, SourceWord word)
    {
        block.MarkRead(word);
        List<string> arguments = SiemensArguments.Split(word.Text);
        int? channel = arguments.Count >= 2 ? SiemensNumbers.WholeNumber(arguments[0]) : null;
        string? path = arguments.Count >= 2 ? SiemensArguments.StringOf(arguments[1]) : null;
        if (channel is not int number || path is null || !StartedLater(block, number))
        {
            block.Draft.KeepAsRaw("INIT selects a program no START of its channel follows, or names it by a variable "
                + "(controllers siemens.md 9)");
            return;
        }

        block.Siemens.ChannelPrograms[number] = ProgramName(path);
    }

    // START(2) starts channel 2, WAITE(2) waits for its end; one block per channel (language 4.8).
    private static void ReadChannels(SiemensBlock block, SourceWord word)
    {
        block.MarkRead(word);
        var channels = new List<int>();
        foreach (string argument in SiemensArguments.Split(word.Text))
        {
            if (SiemensNumbers.WholeNumber(argument) is not int channel || channel < 1)
            {
                block.Draft.KeepAsRaw($"{word.Address} names its channels by variables or not as numbers "
                    + "(controllers siemens.md 9)");
                return;
            }

            channels.Add(channel);
        }

        string key = word.Address == "START" ? "START_CHANNEL" : "WAIT_CHANNEL";
        for (int index = 0; index < channels.Count; index++)
        {
            int channel = channels[index];
            SiemensDraftBlock target = index == 0 ? block.Draft.Main : new SiemensDraftBlock();
            target.Add(key, new IntegerValue(channel, channel.ToString(CultureInfo.InvariantCulture)));
            if (key == "START_CHANNEL" && block.Siemens.ChannelPrograms.Remove(channel, out string? program))
            {
                target.Add("NAME", new StringValue(program));
            }

            if (index > 0)
            {
                block.Draft.After.Add(target);
            }
        }
    }

    // GROUP_BEGIN(0, "Kanal-1 ", 0, 0) opens a program group, structure only: SECTION with its text (controller-mapping
    // 5 and 9).
    private static void ReadGroup(SiemensBlock block, SourceWord word)
    {
        block.MarkRead(word);
        List<string> arguments = SiemensArguments.Split(word.Text);
        string? text = arguments.Count > 1 ? SiemensArguments.StringOf(arguments[1]) : null;
        if (text is null)
        {
            block.Draft.KeepAsRaw("GROUP_BEGIN names its group by no text (controller-mapping 5)");
            return;
        }

        block.Draft.Main.Add("SECTION", new StringValue(text));
    }

    // A START of the channel stands later in the unit.
    private static bool StartedLater(SiemensBlock block, int channel)
    {
        SiemensUnit? unit = block.Unit;
        bool after = false;
        foreach (SourceBlock candidate in unit?.Blocks ?? [])
        {
            if (ReferenceEquals(candidate, block.Source))
            {
                after = true;
                continue;
            }

            if (!after || candidate.Find("START") is not SourceWord start)
            {
                continue;
            }

            foreach (string argument in SiemensArguments.Split(start.Text))
            {
                if (SiemensNumbers.WholeNumber(argument) == channel)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // SHAFT2 of /_N_WKS_DIR/_N_SHAFT_WPD/_N_SHAFT2_MPF (controllers siemens.md 1).
    private static string ProgramName(string path)
    {
        string name = path.Substring(Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\')) + 1);
        if (name.StartsWith("_N_", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(3);
        }

        foreach (string suffix in new[] { "_MPF", "_SPF" })
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - suffix.Length);
            }
        }

        return name.ToUpperInvariant();
    }
}
