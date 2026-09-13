using System.Text;
using Ncx.Core.Model;

namespace Ncx.Core.Catalog;

/// <summary>
/// Checks a word against its catalog entry: whether it may carry its address, and whether the entry accepts its value
/// (language 3, 4). What the entry does not accept is an ERROR on the block with a code of the catalog (D98); the block
/// rules of language 5 are the parser's.
/// </summary>
internal static class WordCheck
{
    // The optional block skip, whose value is the number of a block skip switch (language 4.1).
    private const string SkipKey = "SKIP";

    /// <summary>
    /// Checks the address and the value of a word against its entry and reports what the entry does not accept.
    /// </summary>
    /// <param name="word">The word as written.</param>
    /// <param name="definition">The entry of its key.</param>
    /// <param name="block">The block of the word, which the diagnostic names (D98).</param>
    /// <param name="diagnostics">Where an ERROR goes.</param>
    /// <returns>True when the entry accepts the word.</returns>
    public static bool Accepts(Word word, WordDefinition definition, Block block, Diagnostics diagnostics)
    {
        return AcceptsAddr(word, definition, block, diagnostics) && AcceptsValue(word, definition, block, diagnostics);
    }

    // No address on a word that takes none, an address on a word written with one only, and one of the fixed set
    // where the entry has one (language 3, ADDR; language 4).
    private static bool AcceptsAddr(Word word, WordDefinition definition, Block block, Diagnostics diagnostics)
    {
        string section = $"(language {definition.Section})";
        if (word.Addr is null)
        {
            if (definition.IsAddrRequired)
            {
                string needed = definition.AddrRanks.Count > 0
                    ? ": " + JoinChoices(AddrsInCanonicalOrder(definition))
                    : ", the " + AddrKindName(definition.AddrKind);
                diagnostics.Error(block, DiagnosticCodes.WordNeedsAddress,
                    $"{definition.Key} needs an address{needed} {section}.");
                return false;
            }

            return true;
        }

        if (definition.AddrKind == AddrKind.None)
        {
            diagnostics.Error(block, DiagnosticCodes.WordTakesNoAddress,
                $"{definition.Key} takes no address, not {definition.Key}:{word.Addr} {section}.");
            return false;
        }

        if (definition.AddrRanks.Count > 0 && !definition.AddrRanks.ContainsKey(word.Addr))
        {
            diagnostics.Error(block, DiagnosticCodes.WordAddressNotAccepted,
                $"{definition.Key} takes the address {JoinChoices(AddrsInCanonicalOrder(definition))}, "
                + $"not {definition.Key}:{word.Addr} {section}.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// The value types a word accepts in its block, by which the parser converts its value (P0-04): those of its
    /// entry, unless its address or a partner word in the block changes them.
    /// </summary>
    /// <param name="word">The word as written.</param>
    /// <param name="definition">The entry of its key.</param>
    /// <param name="block">The block of the word, with all its words.</param>
    public static ValueKinds ValueKindsOf(Word word, WordDefinition definition, Block block)
    {
        // An address can change the value types: CYCLE:HEIDENHAIN=251 takes the native cycle number, TOLERANCE:ROTARY
        // a number (language 4.1, 4.7).
        if (word.Addr is not null && definition.AddressedValueKinds is ValueKinds addressed)
        {
            return addressed;
        }

        // A partner word in the block can change them: NAME is a string with PROGRAM=BEGIN and START_CHANNEL, and an
        // identifier, an integer or a string with SUB=BEGIN (language 4.1, D90).
        foreach (PartnerValueKinds withPartner in definition.ValueKindsWithPartner)
        {
            if (block.Has(withPartner.Partner, null, withPartner.PartnerValue))
            {
                return withPartner.ValueKinds;
            }
        }

        return definition.ValueKinds;
    }

    // A value of the value types the word takes in its block, an identifier of its set, and for SKIP a switch 1 to 9
    // (language 3, value types; language 4).
    private static bool AcceptsValue(Word word, WordDefinition definition, Block block, Diagnostics diagnostics)
    {
        string section = $"(language {definition.Section})";
        ValueKinds kinds = ValueKindsOf(word, definition, block);
        if (!IsOfKinds(word.Value, kinds, definition.AllowedIdents))
        {
            string accepted = Describe(kinds, definition.AllowedIdents);
            string onlyWithPartner = OnlyWithPartner(word.Value, kinds, definition);
            string message = word.Value is NoValue
                ? $"{definition.Key} needs a value: {accepted}{onlyWithPartner} {section}."
                : $"{definition.Key} takes {accepted}, not {word.Value.ToCanonical()}{onlyWithPartner} {section}.";
            diagnostics.Error(block, DiagnosticCodes.WordValueNotAccepted, message);
            return false;
        }

        // SKIP=n names the block skip switch n, 1 to 9 (language 4.1).
        if (definition.Key == SkipKey && word.Value is IntegerValue skipSwitch && skipSwitch.Number is < 1 or > 9)
        {
            diagnostics.Error(block, DiagnosticCodes.SkipSwitchOutOfRange,
                $"SKIP takes a block skip switch 1 to 9, not {skipSwitch.Text} {section}.");
            return false;
        }

        return true;
    }

    // The value types of language 3 are the value records of the model, one to one; a state key is the value of the
    // pseudo-words only (D95).
    private static bool IsOfKinds(Value value, ValueKinds kinds, IReadOnlyList<string> allowedIdents)
    {
        return value switch
        {
            NoValue => kinds.HasFlag(ValueKinds.Bare),
            IntegerValue => kinds.HasFlag(ValueKinds.Integer),
            DecimalValue => kinds.HasFlag(ValueKinds.Decimal),
            IdentValue ident => kinds.HasFlag(ValueKinds.Ident)
                && (allowedIdents.Count == 0 || allowedIdents.Contains(ident.Name)),
            ListValue => kinds.HasFlag(ValueKinds.List),
            StringValue => kinds.HasFlag(ValueKinds.String),
            ExprValue => kinds.HasFlag(ValueKinds.Expr),
            StateKeyValue => kinds.HasFlag(ValueKinds.StateKey),
            _ => false,
        };
    }

    // What a partner word would add, where it would accept the value: PROGRAM=BEGIN NAME=SHAFT is told that an
    // identifier names a SUB=BEGIN section only (language 4.1, D90).
    private static string OnlyWithPartner(Value value, ValueKinds kinds, WordDefinition definition)
    {
        var text = new StringBuilder();
        foreach (PartnerValueKinds withPartner in definition.ValueKindsWithPartner)
        {
            if (withPartner.ValueKinds != kinds && IsOfKinds(value, withPartner.ValueKinds, definition.AllowedIdents))
            {
                text.Append("; ").Append(Describe(withPartner.ValueKinds & ~kinds, definition.AllowedIdents))
                    .Append(" only with ").Append(withPartner.PartnerText);
            }
        }

        return text.ToString();
    }

    // The values of the entry as a message names them: "CW, CCW or OFF", "a number, an expression or OFF".
    private static string Describe(ValueKinds kinds, IReadOnlyList<string> allowedIdents)
    {
        var choices = new List<string>();
        if (kinds.HasFlag(ValueKinds.Bare))
        {
            choices.Add("no value");
        }

        if (kinds.HasFlag(ValueKinds.Number))
        {
            choices.Add("a number");
        }
        else if (kinds.HasFlag(ValueKinds.Integer))
        {
            choices.Add("an integer");
        }
        else if (kinds.HasFlag(ValueKinds.Decimal))
        {
            choices.Add("a decimal");
        }

        AddIf(choices, kinds, ValueKinds.Expr, "an expression");
        AddIf(choices, kinds, ValueKinds.List, "a list");
        AddIf(choices, kinds, ValueKinds.String, "a string");
        AddIf(choices, kinds, ValueKinds.StateKey, "a state key");
        if (kinds.HasFlag(ValueKinds.Ident) && allowedIdents.Count == 0)
        {
            choices.Add("an identifier");
        }
        else if (kinds.HasFlag(ValueKinds.Ident))
        {
            choices.AddRange(allowedIdents);
        }

        return JoinChoices(choices);
    }

    private static void AddIf(List<string> choices, ValueKinds kinds, ValueKinds kind, string name)
    {
        if (kinds.HasFlag(kind))
        {
            choices.Add(name);
        }
    }

    // "LEN or RAD", "X, Y, Z, C, IX, IY, IZ or IC".
    private static string JoinChoices(List<string> choices)
    {
        if (choices.Count == 1)
        {
            return choices[0];
        }

        return string.Join(", ", choices.GetRange(0, choices.Count - 1)) + " or " + choices[^1];
    }

    // The fixed addresses of an entry in the order of the rank table (D90).
    private static List<string> AddrsInCanonicalOrder(WordDefinition definition)
    {
        var addrRanks = new List<KeyValuePair<string, int>>(definition.AddrRanks);
        addrRanks.Sort((first, second) => first.Value.CompareTo(second.Value));
        var addrs = new List<string>();
        foreach (KeyValuePair<string, int> addrRank in addrRanks)
        {
            addrs.Add(addrRank.Key);
        }

        return addrs;
    }

    // What an open address names, in the words of language 3 and 4.
    private static string AddrKindName(AddrKind addrKind)
    {
        return addrKind switch
        {
            AddrKind.Axis => "axis name",
            AddrKind.OffsetKind => "offset kind",
            AddrKind.ToleranceKind => "tolerance kind",
            AddrKind.Channel => "coolant channel",
            AddrKind.Role => "role",
            AddrKind.Function => "function name",
            AddrKind.Variable => "variable name",
            AddrKind.Argument => "argument name",
            AddrKind.Controller => "controller or builder dialect",
            _ => "address",
        };
    }
}
