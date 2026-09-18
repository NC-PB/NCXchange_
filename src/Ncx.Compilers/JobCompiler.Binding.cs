using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers;

// The channel-bound words of a job (virtual machine 3.8 rule 2a; machine-config 5; D56): a word of a table the machine
// accepts only from channel n moves to the program of channel n at the same mark, a word of a table it needs in every
// channel program is duplicated into each behind a SYNC the job compiler generates. Every function the compiler
// writes is bound (3.8 rule 2a), so the words are those of the expanded programs: those of the file and those an
// expansion rule or a rewriter generates (architecture 5.5).
public sealed partial class JobCompiler
{
    // What the blocks the job compiler moves or duplicates name as the writer that made them (language 4.15).
    private const string Writer = "job compiler";

    // Every block of every expanded channel program in the order of the manifest and of the program, the generated
    // blocks among them: the words bound to another channel move, the words bound to every channel are duplicated
    // (D56).
    // TODO(question): machine-config 5a puts the blocks of an expansion rule around the block that triggers it, and
    // architecture 5.5 expands the program once before it runs, but no document says whether those blocks follow a
    // word that the job compiler moves or duplicates into another channel program; they stay around the block of the
    // program that writes the word, and only the bound word moves or is duplicated, itself not expanded again, until
    // that is answered.
    // TODO(question): D56 does not say what the job compiler does with a bound word in a subprogram, whose blocks run
    // at every CALL, maybe behind other marks each time (virtual machine 3.9); it is the ERROR CMP703, as a mark in a
    // subprogram that a word would go after is, until that is answered.
    private static void BindWords(List<JobChannel> channels, MachineConfig machine)
    {
        int? generatedMark = GeneratedMark(channels, machine);
        var duplicated = new List<Duplication>();
        var reportedInSubs = new HashSet<Block>(ReferenceEqualityComparer.Instance);
        foreach (JobChannel channel in channels)
        {
            foreach (Section sub in channel.Marks.Subs)
            {
                for (int index = sub.FirstBlock; index <= sub.LastBlock; index++)
                {
                    Block block = channel.File.Blocks[index];
                    List<Word> bound = BoundWords(block, channel, channels, machine);
                    if (bound.Count > 0 && reportedInSubs.Add(block))
                    {
                        channel.Diagnostics.Error(block, DiagnosticCodes.BoundWordInSubprogram,
                            $"{Text(bound)} stands in SUB {sub.Name}, whose blocks run at every CALL, so the job "
                            + "compiler has no one place for it in the program of another channel (D56, virtual "
                            + "machine 3.9).");
                    }
                }
            }

            for (int index = channel.Program.FirstBlock + 1; index < channel.Program.LastBlock; index++)
            {
                BindBlock(channel, index, channels, machine, generatedMark, duplicated);
            }
        }
    }

    // The bound words of one block of a program: to each other channel the words bound to it, then the words bound to
    // every channel.
    private static void BindBlock(JobChannel channel, int index, List<JobChannel> channels, MachineConfig machine,
        int? generatedMark, List<Duplication> duplicated)
    {
        Block block = channel.File.Blocks[index];
        var owners = new List<int>();
        var everyChannel = new List<Word>();
        foreach (Word word in BoundWords(block, channel, channels, machine))
        {
            if (ChannelBinding.BoundTableOf(word, machine)?.Channel is int owner)
            {
                AddOnce(owners, owner);
            }
            else
            {
                everyChannel.Add(word);
            }
        }

        foreach (int owner in owners)
        {
            Move(channel, index, WordsBoundTo(block, owner, machine), owner, channels, machine);
        }

        if (everyChannel.Count > 0)
        {
            Duplicate(channel, index, everyChannel, channels, machine, generatedMark, duplicated);
        }
    }

    // A word the machine accepts only from channel n stands in the program of channel n at the same mark as in its own
    // program (virtual machine 3.8 rule 2a, D56): the words leave their block, and the block of them stands after the
    // mark in the program of channel n.
    private static void Move(JobChannel from, int index, List<Word> words, int owner, List<JobChannel> channels,
        MachineConfig machine)
    {
        Block block = from.File.Blocks[index];
        string table = ChannelBinding.TableName(words[0], machine);
        if (Find(channels, owner) is not JobChannel to)
        {
            from.Diagnostics.Error(block, DiagnosticCodes.BoundChannelNotInJob, string.Create(
                CultureInfo.InvariantCulture,
                $"{Text(words)}: the machine accepts {table} only from channel {owner}, and the job runs no program "
                + $"on channel {owner} to move it to (D56, machine-config 8)."));
            return;
        }

        if (Destination(from, index, to) is not int destination)
        {
            return;
        }

        from.Remove(index, words);
        string? comment = words.Count == WordsOtherThanSkip(block) ? block.Comment : null;
        string reason = string.Create(CultureInfo.InvariantCulture,
            $"{table} channel = {owner}: moved from channel {from.Channel}");
        to.InsertAfter(destination, WordsBlock(words, block, to.File.Blocks[destination].Line, comment, reason));
    }

    // A word the machine needs in every channel program stands in each behind a SYNC the job compiler generates
    // (machine-config 5, channels = "all"; D56, as Mori Seiki requires for M34 and M35): in its own program the SYNC
    // stands before its block, in every other program the SYNC and the word stand after the same mark.
    // TODO(question): D56 does not say what the job compiler does when every channel program already writes the word
    // behind the same mark, as a job read from the programs of such a machine does; the words are left as they stand,
    // until that is answered.
    private static void Duplicate(JobChannel from, int index, List<Word> words, List<JobChannel> channels,
        MachineConfig machine, int? mark, List<Duplication> duplicated)
    {
        Block block = from.File.Blocks[index];
        string table = ChannelBinding.TableName(words[0], machine);
        if (channels.Count < 2 || StandsInEveryChannel(from, index, words, channels))
        {
            return;
        }

        if (mark is not int syncMark)
        {
            from.Diagnostics.Error(block, DiagnosticCodes.NoMarkForGeneratedSync,
                $"{Text(words)}: {table} must stand in every channel program behind a SYNC, and [sync] of the machine "
                + "gives no wait template or no mark_range for it (machine-config 5, D56).");
            return;
        }

        var others = new List<JobChannel>();
        var destinations = new List<int>();
        foreach (JobChannel other in channels)
        {
            if (ReferenceEquals(other, from))
            {
                continue;
            }

            if (Destination(from, index, other) is not int destination)
            {
                return;
            }

            others.Add(other);
            destinations.Add(destination);
        }

        // The generated SYNCs pair in execution order (virtual machine 3.7), so every program needs the duplicated
        // words of one place between two marks behind them in one order; each channel's own words stand where its
        // program writes them, after the words of the other channels, which stand after the mark.
        // TODO(question): D56 and virtual machine 3.8 rule 2a do not say in which order the words bound to every
        // channel stand in every program when two channels write such words between the same marks (or both before
        // every mark), nor whether a channel's own word may leave its place for that order; the words of the second
        // channel are the ERROR CMP705, until that is answered.
        MarkRelease? after = ReleaseBefore(from, index);
        if (DuplicatedByAnotherChannel(duplicated, from, after) is Duplication first)
        {
            from.Diagnostics.Error(block, DiagnosticCodes.AllChannelsWordsOfTwoChannels, string.Create(
                CultureInfo.InvariantCulture,
                $"{Text(words)}: {table} must stand in every channel program behind a SYNC, and channel "
                + $"{first.Channel.Channel} writes {Text(first.Words)} ({first.Channel.File.FileName} line "
                + $"{first.Block.Line}) between the same marks; the SYNCs pair in execution order, and no document "
                + $"says in which order the words of two channels stand in every program (D56; virtual machine 3.7, "
                + $"3.8 rule 2a)."));
            return;
        }

        duplicated.Add(new Duplication(from, after, block, words));
        string reason = table + " channels = \"all\"";
        from.InsertBefore(index, SyncBlock(syncMark, block, block.Line, reason, GeneratedPlacement.Before));
        for (int position = 0; position < others.Count; position++)
        {
            JobChannel other = others[position];
            int line = other.File.Blocks[destinations[position]].Line;
            Block sync = SyncBlock(syncMark, block, line, reason, GeneratedPlacement.After);
            other.InsertAfter(destinations[position], sync);
            other.InsertAfter(destinations[position], WordsBlock(words, block, line, null, reason));
        }
    }

    // The block of the other channel's program a word goes after (D56; virtual machine 3.7: marks are matched in
    // execution order): the SYNC of the same release as the last mark before the word, or the PROGRAM=BEGIN of the
    // program when no mark stands before the word; null after the ERROR.
    // TODO(question): D56 and implementation 16 move the word "to the owning channel's program at the same mark"
    // without saying where between that mark and the next, nor what becomes of it when the owning channel takes no
    // part in that mark (WITH); it stands directly after the mark, where the channels have just been released together,
    // after the PROGRAM=BEGIN when no mark stands before it, and a channel without the same mark is the ERROR CMP704,
    // until that is answered.
    private static int? Destination(JobChannel from, int index, JobChannel to)
    {
        Block block = from.File.Blocks[index];
        int passed = from.Marks.MarksBefore.GetValueOrDefault(index);
        if (passed == 0)
        {
            return to.Program.FirstBlock;
        }

        MarkRelease release = from.Marks.Releases[passed - 1];
        MarkRelease? same = SameRelease(to, release);
        if (same is null)
        {
            from.Diagnostics.Error(block, DiagnosticCodes.NoSameMarkInChannel, string.Create(
                CultureInfo.InvariantCulture,
                $"Channel {to.Channel} takes no part in SYNC={release.Mark} with {string.Join(",", release.Channels)}, "
                + $"the mark before this block, so its program has no same mark for the words bound to it (D56; "
                + $"language 4.8)."));
            return null;
        }

        if (same.InSubprogram || same.Block is not int syncBlock)
        {
            from.Diagnostics.Error(block, DiagnosticCodes.BoundWordInSubprogram, string.Create(
                CultureInfo.InvariantCulture,
                $"The SYNC={release.Mark} of channel {to.Channel} after which the words would stand is in a "
                + $"subprogram or on no block of its file, so the job compiler has no one place for them there (D56, "
                + $"virtual machine 3.9)."));
            return null;
        }

        return syncBlock;
    }

    // The release of the last mark a channel passed before a block of its program; null before every mark.
    private static MarkRelease? ReleaseBefore(JobChannel channel, int index)
    {
        int passed = channel.Marks.MarksBefore.GetValueOrDefault(index);
        return passed == 0 ? null : channel.Marks.Releases[passed - 1];
    }

    // The first words another channel duplicated from the same place between two marks: behind the same release, or
    // before every mark as well; null when no other channel did.
    private static Duplication? DuplicatedByAnotherChannel(List<Duplication> duplicated, JobChannel from,
        MarkRelease? after)
    {
        foreach (Duplication earlier in duplicated)
        {
            bool samePlace = earlier.After is null
                ? after is null
                : after is not null && earlier.After.IsSameRelease(after);
            if (!ReferenceEquals(earlier.Channel, from) && samePlace)
            {
                return earlier;
            }
        }

        return null;
    }

    // Whether every other channel program already holds the words behind the same mark.
    private static bool StandsInEveryChannel(JobChannel from, int index, List<Word> words, List<JobChannel> channels)
    {
        int passed = from.Marks.MarksBefore.GetValueOrDefault(index);
        foreach (JobChannel other in channels)
        {
            if (ReferenceEquals(other, from))
            {
                continue;
            }

            int? otherPassed = passed == 0 ? 0 : PassedWith(other, from.Marks.Releases[passed - 1]);
            if (otherPassed is not int count || !HoldsWords(other, count, words))
            {
                return false;
            }
        }

        return true;
    }

    // Whether a block of the program behind the given number of marks holds every word, as canonical NCX writes it.
    private static bool HoldsWords(JobChannel channel, int passed, List<Word> words)
    {
        foreach (KeyValuePair<int, int> block in channel.Marks.MarksBefore)
        {
            if (block.Value == passed && HoldsAllWords(channel.File.Blocks[block.Key], words))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HoldsAllWords(Block block, List<Word> words)
    {
        foreach (Word word in words)
        {
            if (block.Find(word.Key, word.Addr)?.ToCanonical() != word.ToCanonical())
            {
                return false;
            }
        }

        return true;
    }

    // The number of marks a channel had passed with the same release; null when it took no part in it.
    private static int? PassedWith(JobChannel channel, MarkRelease release)
    {
        for (int position = 0; position < channel.Marks.Releases.Count; position++)
        {
            if (channel.Marks.Releases[position].IsSameRelease(release))
            {
                return position + 1;
            }
        }

        return null;
    }

    private static MarkRelease? SameRelease(JobChannel channel, MarkRelease release)
    {
        return PassedWith(channel, release) is int passed ? channel.Marks.Releases[passed - 1] : null;
    }

    // The words of a block bound to another channel than its own, or to every channel on a job of several channels.
    private static List<Word> BoundWords(Block block, JobChannel channel, List<JobChannel> channels,
        MachineConfig machine)
    {
        var words = new List<Word>();
        foreach (Word word in block.Words)
        {
            FunctionTable? table = ChannelBinding.BoundTableOf(word, machine);
            bool toAnother = table?.Channel is int owner && owner != channel.Channel;
            bool toEvery = table is { AllChannels: true } && channels.Count > 1;
            if (toAnother || toEvery)
            {
                words.Add(word);
            }
        }

        return words;
    }

    // The words of a block bound to one channel.
    private static List<Word> WordsBoundTo(Block block, int owner, MachineConfig machine)
    {
        var words = new List<Word>();
        foreach (Word word in block.Words)
        {
            if (ChannelBinding.BoundTableOf(word, machine)?.Channel == owner)
            {
                words.Add(word);
            }
        }

        return words;
    }

    // The block of moved or duplicated words, generated for the block they come from (language 4.15, D98): a
    // diagnostic on it names the line of the block it stands after and the line of the block the words came from, and
    // it carries the SKIP of that block (SkipOf).
    private static Block WordsBlock(List<Word> words, Block origin, int line, string? comment, string reason)
    {
        List<Word> blockWords = SkipOf(origin);
        blockWords.AddRange(words);
        var block = new Block { Line = line, Words = blockWords, Comment = comment };
        return Generated(block, origin, reason, GeneratedPlacement.After);
    }

    private static int WordsOtherThanSkip(Block block)
    {
        int count = 0;
        foreach (Word word in block.Words)
        {
            count += word.Key == "SKIP" ? 0 : 1;
        }

        return count;
    }

    private static JobChannel? Find(List<JobChannel> channels, int id)
    {
        foreach (JobChannel channel in channels)
        {
            if (channel.Channel == id)
            {
                return channel;
            }
        }

        return null;
    }

    private static string Text(List<Word> words)
    {
        var texts = new List<string>();
        foreach (Word word in words)
        {
            texts.Add(word.ToCanonical());
        }

        return string.Join(" ", texts);
    }

    // Words bound to every channel that the job compiler duplicated from a block of a channel program, and the release
    // of the mark before them, null before every mark.
    private sealed record Duplication(JobChannel Channel, MarkRelease? After, Block Block, List<Word> Words);

    private static void AddOnce(List<int> numbers, int number)
    {
        if (!numbers.Contains(number))
        {
            numbers.Add(number);
        }
    }
}
