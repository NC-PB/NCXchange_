using System.Globalization;
using System.Text;
using Ncx.Core.Catalog;

namespace Ncx.Core.Tests.Catalog;

/// <summary>
/// Writes the word catalog as the Markdown table of docs/spec/generated/word-catalog.md: one row per word and one per
/// form with a rank of its own, in canonical order, each with the section of the language it comes from, so that the
/// catalog can be laid next to the specification (phase 0, P0-03; D90).
/// </summary>
internal static class WordCatalogTable
{
    // What the file is, where it comes from and how to read it.
    private const string Head = """
        # Word catalog

        Generated from the word catalog in `src/Ncx.Core/Catalog/` by the test `WordCatalogTableTests` (P0-03);
        change the catalog and run the tests instead of editing this file. The table is the reference for the
        canonical order of language 5 rule 6 (D90): a word of lower rank stands first. It lists every word of the
        tables of section 4 of `../ncx-language.md` with the section it comes from, the pseudo-words of 4.15
        (internal, D95), and every form with a rank of its own: the addresses of a fixed set (`OFFSET:LEN`,
        `CENTER:IX`, `TOLERANCE:ROTARY`), the reset form of `SHIFT`, `TILT` and `TILT_AXIS`, the machine axis words
        (D93) and the native cycle parameters (D94). Words of one key with an open address (a role, a channel, a
        variable) sort under their rank by the address text. The ten verbs share one rank; a block has at most one.

        | Rank | Word | Value | Verb | Axis words | Address | Scope | Section | Meaning |
        |---:|---|---|---|---|---|---|---|---|

        """;

    /// <summary>
    /// The text of the file, with LF line endings.
    /// </summary>
    public static string Write()
    {
        // The rows by rank; rows of one rank, the ten verbs, in the order of the tables of language 4.
        var rowsByRank = new SortedDictionary<int, List<string>>();
        foreach (WordDefinition definition in WordCatalog.All())
        {
            // A word written with an address of its fixed set only (CENTER) has its rows in the address rows.
            if (!definition.IsAddrRequired || definition.AddrRanks.Count == 0)
            {
                Add(rowsByRank, definition.CanonicalRank, WordRow(definition));
            }

            foreach (KeyValuePair<string, int> addrRank in definition.AddrRanks)
            {
                Add(rowsByRank, addrRank.Value, AddrRow(definition, addrRank.Key, addrRank.Value));
            }

            if (definition.ResetRank is int resetRank)
            {
                Add(rowsByRank, resetRank, ResetRow(definition, resetRank));
            }
        }

        AddRulesForKeysTheCatalogDoesNotList(rowsByRank);

        var text = new StringBuilder(Head.ReplaceLineEndings("\n"));
        foreach (List<string> rows in rowsByRank.Values)
        {
            foreach (string row in rows)
            {
                text.Append(row).Append('\n');
            }
        }

        return text.ToString();
    }

    // The machine axis words (D93) and the native cycle parameters (D94) have no entry but a rank of their own.
    private static void AddRulesForKeysTheCatalogDoesNotList(SortedDictionary<int, List<string>> rowsByRank)
    {
        Add(rowsByRank, CanonicalRanks.MachineAxis, Row(CanonicalRanks.MachineAxis,
        [
            "machine axis words (D93)", "number, expression; none under `HOME`", "", "is one", "", "block", "4.3",
            "A key of the form `[XYZABCUVW][0-9]{0,2}` that is no catalog word, in a block whose verb carries axis "
            + "words; machine axes sort by letter, then by number: `C2`, `W`, `Z2`.",
        ]));
        Add(rowsByRank, CanonicalRanks.IncrementalMachineAxis, Row(CanonicalRanks.IncrementalMachineAxis,
        [
            "incremental machine axis words (D93)", "number, expression", "", "is one", "", "block", "4.3",
            "`I` followed by a machine axis name: `IC2`, `IW`, `IZ2`, in the order of the absolute words.",
        ]));
        Add(rowsByRank, CanonicalRanks.NativeParameter, Row(CanonicalRanks.NativeParameter,
        [
            "native cycle parameters (D94)", "number, expression", "", "", "", "with partner", "4.7.1",
            "Every key the catalog does not know in a block that carries `CYCLE:<controller>=n`, in source order.",
        ]));
    }

    private static void Add(SortedDictionary<int, List<string>> rowsByRank, int rank, string row)
    {
        if (!rowsByRank.TryGetValue(rank, out List<string>? rows))
        {
            rows = [];
            rowsByRank.Add(rank, rows);
        }

        rows.Add(row);
    }

    private static string WordRow(WordDefinition definition)
    {
        string value = ValueText(definition.ValueKinds, definition.AllowedIdents);
        if (definition.AddressedValueKinds is ValueKinds addressed && definition.AddrRanks.Count == 0)
        {
            value += "; with an address: " + ValueText(addressed, []);
        }

        foreach (PartnerValueKinds withPartner in definition.ValueKindsWithPartner)
        {
            value += "; with `" + withPartner.PartnerText + "`: "
                + ValueText(withPartner.ValueKinds, definition.AllowedIdents);
        }

        string axisWords = "";
        if (definition.TakesAxisWords)
        {
            axisWords = "carries";
        }
        else if (definition.IsAxisWord)
        {
            axisWords = "is one";
        }

        return Row(definition.CanonicalRank,
        [
            "`" + definition.Key + "`" + (definition.IsInternal ? " (internal)" : ""),
            value,
            definition.IsVerb ? "verb" : "",
            axisWords,
            AddressText(definition),
            ScopeText(definition.Scope),
            definition.Section,
            definition.Description,
        ]);
    }

    private static string AddrRow(WordDefinition definition, string addr, int rank)
    {
        ValueKinds kinds = definition.AddressedValueKinds ?? definition.ValueKinds;
        return Row(rank,
        [
            "`" + definition.Key + ":" + addr + "`",
            ValueText(kinds, definition.AllowedIdents),
            "",
            definition.IsAxisWord ? "is one" : "",
            "`" + addr + "`",
            ScopeText(definition.Scope),
            definition.Section,
            definition.Description,
        ]);
    }

    private static string ResetRow(WordDefinition definition, int rank)
    {
        return Row(rank,
        [
            "`" + definition.Key + "=RESET`",
            "`RESET`",
            "",
            "",
            "",
            ScopeText(definition.Scope),
            definition.Section,
            "Removes the entry of `" + definition.Key + "` and what follows it from the frame chain; a frame word, "
            + "not the verb.",
        ]);
    }

    private static string Row(int rank, IReadOnlyList<string> cells)
    {
        return "| " + rank.ToString(CultureInfo.InvariantCulture) + " | " + string.Join(" | ", cells) + " |";
    }

    // The value types in the words of language 3, the identifiers of a set in backticks.
    private static string ValueText(ValueKinds kinds, IReadOnlyList<string> allowedIdents)
    {
        var parts = new List<string>();
        if (kinds.HasFlag(ValueKinds.Bare))
        {
            parts.Add("none");
        }

        if (kinds.HasFlag(ValueKinds.Number))
        {
            parts.Add("number");
        }
        else if (kinds.HasFlag(ValueKinds.Integer))
        {
            parts.Add("integer");
        }
        else if (kinds.HasFlag(ValueKinds.Decimal))
        {
            parts.Add("decimal");
        }

        AddIf(parts, kinds, ValueKinds.Expr, "expression");
        AddIf(parts, kinds, ValueKinds.List, "list");
        AddIf(parts, kinds, ValueKinds.String, "string");
        AddIf(parts, kinds, ValueKinds.StateKey, "state key");
        if (kinds.HasFlag(ValueKinds.Ident) && allowedIdents.Count == 0)
        {
            parts.Add("identifier");
        }
        else if (kinds.HasFlag(ValueKinds.Ident))
        {
            foreach (string ident in allowedIdents)
            {
                parts.Add("`" + ident + "`");
            }
        }

        return string.Join(", ", parts);
    }

    private static void AddIf(List<string> parts, ValueKinds kinds, ValueKinds kind, string name)
    {
        if (kinds.HasFlag(kind))
        {
            parts.Add(name);
        }
    }

    private static string AddressText(WordDefinition definition)
    {
        string kind = definition.AddrKind switch
        {
            AddrKind.None => "",
            AddrKind.Axis => "axis",
            AddrKind.OffsetKind => "offset kind",
            AddrKind.ToleranceKind => "tolerance kind",
            AddrKind.Channel => "coolant channel",
            AddrKind.Role => "role",
            AddrKind.Function => "function name",
            AddrKind.Variable => "variable name",
            AddrKind.Argument => "argument name",
            AddrKind.Controller => "controller",
            _ => definition.AddrKind.ToString(),
        };
        if (kind.Length == 0)
        {
            return "";
        }

        return kind + (definition.IsAddrRequired ? ", required" : ", optional");
    }

    private static string ScopeText(Scope scope)
    {
        return scope switch
        {
            Scope.None => "",
            Scope.File => "file",
            Scope.Program => "program",
            Scope.Header => "header",
            Scope.Block => "block",
            Scope.Modal => "modal",
            Scope.WithPartner => "with partner",
            _ => scope.ToString(),
        };
    }
}
