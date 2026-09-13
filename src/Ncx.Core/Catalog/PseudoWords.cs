namespace Ncx.Core.Catalog;

/// <summary>
/// The pseudo-words of generated blocks, @SAVE and @RESTORE (language 4.15, virtual machine 3.10): internal entries
/// with a state key KEY[:ADDR] as their value, which the parser accepts only under the option the expander uses for
/// generated text; in a user file they are the ERROR "pseudo-word in a user file" (D95).
/// </summary>
internal static class PseudoWords
{
    /// <summary>
    /// The two pseudo-words. They act when their block executes (virtual machine 3.10).
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        new WordDefinition
        {
            Key = "@SAVE",
            Group = WordKind.Pseudo,
            ValueKinds = ValueKinds.StateKey,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Save,
            IsInternal = true,
            Section = "4.15",
            Description = "Pushes the value of the state variable its state key names on the restore stack; generated "
                + "blocks only (virtual machine 3.10, D95).",
        },
        new WordDefinition
        {
            Key = "@RESTORE",
            Group = WordKind.Pseudo,
            ValueKinds = ValueKinds.StateKey,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Restore,
            IsInternal = true,
            Section = "4.15",
            Description = "Pops that value and applies it again as if the program had written the word; generated "
                + "blocks only (virtual machine 3.10, D95).",
        },
    ];
}
