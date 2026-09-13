namespace Ncx.Core.Catalog;

/// <summary>
/// The channel and synchronization words of language 4.8.
/// </summary>
internal static class ChannelWords
{
    /// <summary>
    /// The rows of the table of language 4.8.
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        new WordDefinition
        {
            Key = "SYNC",
            Group = WordKind.Channel,
            ValueKinds = ValueKinds.Integer,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Sync,
            Section = "4.8",
            Description = "Rendezvous mark: the channel waits until every participating channel has reached it "
                + "(M100 to M199, WAITM).",
        },

        // A list of channel numbers; one channel alone is an integer (language 3, list).
        new WordDefinition
        {
            Key = "WITH",
            Group = WordKind.Channel,
            ValueKinds = ValueKinds.List | ValueKinds.Integer,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.With,
            Section = "4.8",
            Description = "Participating channels of the SYNC in the same block; default all channels of the job.",
        },
        new WordDefinition
        {
            Key = "START_CHANNEL",
            Group = WordKind.Channel,
            ValueKinds = ValueKinds.Integer,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.StartChannel,
            Section = "4.8",
            Description = "Starts the program of that channel (Siemens START); an optional NAME selects the program.",
        },
        new WordDefinition
        {
            Key = "WAIT_CHANNEL",
            Group = WordKind.Channel,
            ValueKinds = ValueKinds.Integer,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.WaitChannel,
            Section = "4.8",
            Description = "Waits until that channel has finished (Siemens WAITE).",
        },
    ];
}
