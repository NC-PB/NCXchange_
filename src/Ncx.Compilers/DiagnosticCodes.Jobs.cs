namespace Ncx.Compilers;

// The codes of the job compiler (P6-02), CMP700 to CMP799: CMP700-CMP709 the channel binding of the function tables
// (virtual machine 3.8 rule 2a, D56), CMP710-CMP719 the wait marks of a job (machine-config 5, [sync]), CMP720-CMP729
// the channel programs and their files.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CMP700: a single-channel compile writes a word of a function table that the machine accepts only from another
    /// channel, channel = n (virtual machine 3.8 rule 2a, D56); a job compile moves it to the program of that channel.
    /// </summary>
    public const string WordBoundToAnotherChannel = "CMP700";

    /// <summary>
    /// CMP701: a single-channel compile for a machine of several channels writes a word of a function table that must
    /// stand in every channel program behind a wait, channels = "all" (D56); a job compile duplicates it.
    /// </summary>
    public const string WordBoundToEveryChannel = "CMP701";

    /// <summary>
    /// CMP702: a word of a function table bound to a channel, channel = n, in a job that runs no program on channel n,
    /// so the word has no program to move to (D56, machine-config 8).
    /// </summary>
    public const string BoundChannelNotInJob = "CMP702";

    /// <summary>
    /// CMP703: a word bound to another channel or to every channel stands in a subprogram, or the mark it is moved to
    /// does, so it has no one place in the program of the channel (D56, virtual machine 3.9).
    /// </summary>
    public const string BoundWordInSubprogram = "CMP703";

    /// <summary>
    /// CMP704: the channel a word is moved or duplicated to takes no part in the mark that stands before the word, so
    /// the program of that channel has no same mark for it (D56; language 4.8, WITH).
    /// </summary>
    public const string NoSameMarkInChannel = "CMP704";

    /// <summary>
    /// CMP705: words bound to every channel, channels = "all", stand in the programs of two channels between the same
    /// marks, so that the job compiler would duplicate them into every program behind generated SYNCs in two orders,
    /// and the SYNCs pair in execution order (D56; virtual machine 3.7, 3.8 rule 2a).
    /// </summary>
    public const string AllChannelsWordsOfTwoChannels = "CMP705";

    /// <summary>
    /// CMP710: a SYNC mark outside the range of its channels, the range of the [sync] group that holds them or else
    /// mark_range (machine-config 5).
    /// </summary>
    public const string MarkOutsideItsRange = "CMP710";

    /// <summary>
    /// CMP711: the job compiler needs a SYNC of every channel for a word bound to every channel, and [sync] of the
    /// machine gives no wait template or no range of marks for it (machine-config 5, D56).
    /// </summary>
    public const string NoMarkForGeneratedSync = "CMP711";

    /// <summary>
    /// CMP712: a SYNC the job compiler generates, for [sync] start_mark or for a word bound to every channel, can never
    /// be released in the job as it would be written, the deadlock of the job (virtual machine 3.7; machine-config 5,
    /// D56), as when a channel waits for another with WAIT_CHANNEL or is started by START_CHANNEL (language 4.8).
    /// </summary>
    public const string GeneratedSyncDeadlocks = "CMP712";

    /// <summary>
    /// CMP720, a WARNING: a subprogram of a file of the job that no channel program calls stands in the output file of
    /// no channel (language 4.13, D99).
    /// </summary>
    public const string SubprogramOfNoChannel = "CMP720";
}
