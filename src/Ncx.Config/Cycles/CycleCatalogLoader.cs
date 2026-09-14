using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Cycles;

/// <summary>
/// Loads a cycle catalog file, cycles/fanuc.toml, over the built-in drilling family of its controller family, and puts
/// it under the [[cycle]] entries of a machine file (machine-config 6). The TOML document is walked table by table like
/// the machine file, so that a mistake is reported with its line (P2-01). The machine file names the catalog in
/// [cycles] catalog and the cycles folder of the project holds it (machine-config 10); the command line finds it.
/// </summary>
public static partial class CycleCatalogLoader
{
    // A catalog file holds one [[cycle]] per entry and nothing else (machine-config 6).
    private static readonly string[] s_catalogTables = ["cycle"];

    /// <summary>
    /// Loads the catalog file at a path. A file that cannot be read is an I/O error, thrown for the composition root,
    /// which reports it with the file name (code-guidelines 6).
    /// </summary>
    /// <param name="path">The catalog file.</param>
    /// <param name="controller">The controller family of the catalog, the controller of the machine that names
    /// it.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The catalog, or null when the file has an ERROR.</returns>
    public static CycleCatalog? Load(string path, Controller controller, Diagnostics diagnostics)
    {
        return LoadText(File.ReadAllText(path), controller, diagnostics);
    }

    /// <summary>
    /// Loads a catalog file from its text over the built-in drilling family of its controller family: unknown keys
    /// are WARNINGs, wrong types, missing names and natives and unusable entries ERRORs, each on its line (P2-01,
    /// machine-config 6).
    /// </summary>
    /// <param name="text">The TOML text of the catalog file.</param>
    /// <param name="controller">The controller family of the catalog, the controller of the machine that names
    /// it.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The catalog, or null when the file has an ERROR.</returns>
    public static CycleCatalog? LoadText(string text, Controller controller, Diagnostics diagnostics)
    {
        int errorsBefore = TomlDocument.ErrorCount(diagnostics);
        TomlDocument? document = TomlDocument.Parse(text, diagnostics);
        if (document is null)
        {
            return null;
        }

        // A catalog file holds [[cycle]] entries only; any other table is a WARNING that names [[cycle]], as an
        // unknown table of the machine file is (machine-config 6, P2-01).
        ConfigTable root = document.Root("machine-config 6");
        root.WarnUnknownTables(s_catalogTables, s_catalogTables);

        // The catalog file of a controller family overrides the built-in drilling family of that family entry by
        // entry and adds the catalog cycles (machine-config 6, language 4.7.1).
        IReadOnlyList<CycleEntry> entries = ReadEntries(root.Tables("cycle", "[[cycle]]"), controller);
        CycleCatalog catalog = DrillingFamily.Catalog(controller).Override(entries);
        return TomlDocument.ErrorCount(diagnostics) > errorsBefore ? null : catalog;
    }

    /// <summary>
    /// The machine with a catalog file of its controller family beneath its own [[cycle]] entries: the built-in
    /// drilling family, the catalog file, the entries of the machine file (machine-config 6).
    /// </summary>
    /// <param name="machine">The loaded machine, which keeps its [[cycle]] entries in CycleEntries.</param>
    /// <param name="catalog">The catalog file that [cycles] catalog of the machine names, loaded for its
    /// controller.</param>
    /// <exception cref="InvalidOperationException">The catalog belongs to another controller family.</exception>
    public static MachineConfig WithCatalog(MachineConfig machine, CycleCatalog catalog)
    {
        // The catalog of another controller family maps native cycles that the reader and the compiler of the machine
        // do not write; handing it over is a mistake of the caller, not of a file (code-guidelines 6).
        if (catalog.Controller != machine.Machine.Controller)
        {
            throw new InvalidOperationException(
                $"The {catalog.Controller} cycle catalog cannot serve the {machine.Machine.Controller} machine "
                + $"{machine.Machine.Name} (machine-config 6).");
        }

        // The [[cycle]] entries of the machine file override the catalog per machine (machine-config 6).
        return machine with { CycleCatalog = catalog.Override(machine.CycleEntries) };
    }

    /// <summary>
    /// The cycle catalog of a machine before its catalog file is loaded: the [[cycle]] entries of the machine file
    /// over the built-in drilling family of its controller (machine-config 6).
    /// </summary>
    /// <param name="controller">The controller of the machine; null on a file that names none.</param>
    /// <param name="machineEntries">The [[cycle]] entries of the machine file as it writes them.</param>
    internal static CycleCatalog MachineCatalog(Controller? controller, IReadOnlyList<CycleEntry> machineEntries)
    {
        // A file without a controller has no family; it reports that ERROR itself (machine-config 1).
        CycleCatalog family = controller is Controller known ? DrillingFamily.Catalog(known) : new CycleCatalog();
        return family.Override(machineEntries);
    }
}
