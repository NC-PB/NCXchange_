namespace Ncx.Config;

/// <summary>
/// ncx.toml, the settings of the working directory: the machine file that --machine defaults to, the machine and cycle
/// folders, the output folder and the plugin assemblies (machine-config 10, architecture 10). The folders are relative
/// to the folder of ncx.toml, the working directory.
/// </summary>
public sealed record ProjectSettings
{
    /// <summary>
    /// The name of the file, read from the working directory (D74, architecture 10).
    /// </summary>
    public const string FileName = "ncx.toml";

    /// <summary>
    /// The machine folder of the project layout, machines/ (machine-config 10).
    /// </summary>
    public const string MachinesFolder = "machines";

    /// <summary>
    /// The cycle folder of the project layout, cycles/ (machine-config 10).
    /// </summary>
    public const string CyclesFolder = "cycles";

    /// <summary>
    /// The output folder of the project layout, out/, with one folder per machine (machine-config 10, architecture 10).
    /// </summary>
    public const string OutFolder = "out";

    /// <summary>
    /// machine: the machine file that --machine defaults to, a name in the machine folders or a path; null when
    /// ncx.toml names none, and without --machine the built-in default machine of D103 applies (architecture 10).
    /// </summary>
    public string? Machine { get; init; }

    /// <summary>
    /// The line of the machine key, which a machine that cannot be found is reported on (D98); 1 without the key.
    /// </summary>
    public int MachineLine { get; init; } = 1;

    /// <summary>
    /// machines: the machine folder, machines/ when left out (machine-config 10).
    /// </summary>
    public string Machines { get; init; } = MachinesFolder;

    /// <summary>
    /// cycles: the cycle folder, where the catalog file that [cycles] catalog of a machine names is found, cycles/ when
    /// left out (machine-config 6, 10).
    /// </summary>
    public string Cycles { get; init; } = CyclesFolder;

    /// <summary>
    /// out: the output folder of the compilers, out/ when left out (machine-config 10, architecture 10).
    /// </summary>
    public string Out { get; init; } = OutFolder;

    /// <summary>
    /// plugins: the plugin assemblies, plugins = ["MyShop.NcxPlugins.dll"] (machine-config 10); empty when left out.
    /// </summary>
    public IReadOnlyList<string> Plugins { get; init; } = [];

    /// <summary>
    /// The settings of each plugin: its own [plugins.&lt;name&gt;] section, a string dictionary that the plugin reads
    /// through its context (D80); empty when ncx.toml has none.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> PluginSettings { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>();
}
