namespace Ncx.Core.Catalog;

/// <summary>
/// The value types a word accepts when a partner word with this value stands in its block, where they differ from the
/// value types of its entry: NAME="..." is a string, and with SUB=BEGIN an identifier, an integer or a string
/// (language 4.1, D90).
/// </summary>
/// <param name="Partner">The key of the partner word: "SUB".</param>
/// <param name="PartnerValue">The identifier the partner word carries: "BEGIN".</param>
/// <param name="ValueKinds">The value types the word accepts with that partner in its block.</param>
public sealed record PartnerValueKinds(string Partner, string PartnerValue, ValueKinds ValueKinds)
{
    /// <summary>
    /// The partner word as NCX writes it: "SUB=BEGIN".
    /// </summary>
    public string PartnerText => Partner + "=" + PartnerValue;
}
