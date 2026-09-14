namespace Ncx.Core.Machine;

/// <summary>
/// The cycle catalog of one controller family as a machine uses it: the built-in drilling family, the entries of the
/// catalog file of [cycles] and the [[cycle]] entries of the machine file, each overriding the entry of the same name
/// before it (machine-config 6, language 4.7.1). Readers look an entry up by its native cycle, compilers and the
/// expander by its NCX name (architecture 6).
/// </summary>
public sealed record CycleCatalog
{
    /// <summary>
    /// The controller family whose native cycles the entries map; null for the empty catalog of the default machine of
    /// D103, which serves no reader and no compiler.
    /// </summary>
    public Controller? Controller { get; init; }

    /// <summary>
    /// The entries in catalog order: the built-in drilling family first, then the cycles that the catalog file and the
    /// machine file add.
    /// </summary>
    public IReadOnlyList<CycleEntry> Entries { get; init; } = [];

    /// <summary>
    /// The entry of an NCX cycle name, CYCLE=RECT_POCKET (language 4.7.1); null when the catalog has none, which the
    /// caller reports.
    /// </summary>
    /// <param name="ncxName">The cycle name as a program writes it.</param>
    public CycleEntry? Find(string ncxName)
    {
        // A program may use any catalog name; the entry of that name maps it (language 4.7.1).
        foreach (CycleEntry entry in Entries)
        {
            if (entry.Name == ncxName)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// The first entry of a native cycle in catalog order, "G81", "200", "CYCLE81"; null when the catalog has none.
    /// </summary>
    /// <param name="native">The native cycle as the catalog writes it.</param>
    public CycleEntry? FindNative(string native)
    {
        // A reader turns a native cycle into the entry that maps it; of two entries of one cycle, DRILL and
        // DRILL_DWELL on cycle 200, the first in catalog order (controller-mapping 5).
        foreach (CycleEntry entry in Entries)
        {
            if (entry.Native == native)
            {
                return entry;
            }
        }

        return null;
    }

    // TODO(question): a Siemens position without a value takes the default of the cycle (siemens 7), and that default
    // is not in the documents (VARI of CYCLE83); a block that leaves out a fixed value fits no entry that fixes it
    // (D181).

    /// <summary>
    /// The entry of a native cycle whose fixed values the source block carries: CYCLE83 with VARI=1 is PECK, with
    /// VARI=0 CHIP_BREAK (machine-config 6, controller-mapping 5); an entry without fixed values fits every block of
    /// its cycle. Null when no entry fits.
    /// </summary>
    /// <param name="native">The native cycle as the catalog writes it.</param>
    /// <param name="nativeValues">The native parameters the source block gives, by native name.</param>
    public CycleEntry? FindNative(string native, IReadOnlyDictionary<string, decimal> nativeValues)
    {
        // The reader takes the entry whose fixed values the source block carries (machine-config 6).
        foreach (CycleEntry entry in Entries)
        {
            if (entry.Native == native && CarriesFixedValues(nativeValues, entry))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// This catalog with the entries of a later file over it: an entry of a name the catalog has overrides that entry
    /// by the keys it wrote, an entry of a new name is added at the end (machine-config 6).
    /// </summary>
    /// <param name="entries">The entries of the catalog file or of the machine file, as the file writes them.</param>
    public CycleCatalog Override(IReadOnlyList<CycleEntry> entries)
    {
        // The catalog file of the family overrides the built-in drilling family, the [[cycle]] entries of a machine
        // file override the catalog per machine (machine-config 6).
        var overridden = new List<CycleEntry>(Entries);
        foreach (CycleEntry entry in entries)
        {
            int index = IndexOfName(overridden, entry.Name);
            if (index < 0)
            {
                overridden.Add(entry);
            }
            else
            {
                overridden[index] = overridden[index].OverriddenBy(entry);
            }
        }

        return new CycleCatalog { Controller = Controller, Entries = overridden };
    }

    // TODO(question): language 4.7 calls n of CYCLE:<controller>=n "the native cycle number of that family" and shows
    // Heidenhain only (251); how a Fanuc cycle (G71 or 71) or a Siemens cycle (CYCLE952) is written as n is not in the
    // documents. FindNative compares n with the native of an entry as the catalog writes it (D144).

    /// <summary>
    /// Whether a native cycle written with this controller address, CYCLE:HEIDENHAIN=251, passes through this catalog:
    /// only when the address names the controller family of the catalog (language 4.7.1, D94).
    /// </summary>
    /// <param name="controllerAddress">The address of the CYCLE word, HEIDENHAIN.</param>
    public bool PassesThrough(string controllerAddress)
    {
        // A cycle that exists on one controller only may be written natively and compiles only to that family: its
        // compiler writes native cycle n, the cycle words through the entry of n when the catalog has one
        // (FindNative), and every key the word catalog does not know as a native parameter in source order after the
        // cycle words; a compiler of any other family reports the block as an ERROR like RAW (language 4.7.1, D45,
        // D94). NCX writes the address in capitals (language 3).
        return Controller is { } family && controllerAddress == Enum.GetName(family)?.ToUpperInvariant();
    }

    // An entry fits a source block when the block carries every fixed value of the entry (machine-config 6).
    private static bool CarriesFixedValues(IReadOnlyDictionary<string, decimal> nativeValues, CycleEntry entry)
    {
        foreach (KeyValuePair<string, decimal> fixedValue in entry.Fixed)
        {
            if (!nativeValues.TryGetValue(fixedValue.Key, out decimal value) || value != fixedValue.Value)
            {
                return false;
            }
        }

        return true;
    }

    // The place of the entry of a name, or -1 when there is none.
    private static int IndexOfName(List<CycleEntry> entries, string name)
    {
        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index].Name == name)
            {
                return index;
            }
        }

        return -1;
    }
}
