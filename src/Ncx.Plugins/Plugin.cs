namespace Ncx.Plugins;

/// <summary>
/// One loaded plugin, one DLL: its name, which its [plugins.&lt;name&gt;] section of ncx.toml and every diagnostic
/// about it carry, its settings, and whether it is left out for the rest of the run (D80; implementation 17, P7-01).
/// </summary>
internal sealed class Plugin
{
    public Plugin(string name, IReadOnlyDictionary<string, string> settings)
    {
        Name = name;
        Settings = settings;
    }

    /// <summary>
    /// The name of the plugin, the name of its DLL without .dll: MyShopRules.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The plugin's own section of ncx.toml as strings (D80); empty without one.
    /// </summary>
    public IReadOnlyDictionary<string, string> Settings { get; }

    /// <summary>
    /// True once one of its classes failed: nothing of the plugin is asked again in the run (implementation 17, P7-01).
    /// </summary>
    public bool IsDropped { get; private set; }

    /// <summary>
    /// Leaves the plugin out for the rest of the run, every class of it in every place.
    /// </summary>
    public void Drop()
    {
        IsDropped = true;
    }
}
