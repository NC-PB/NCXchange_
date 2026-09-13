namespace Ncx.Core.Machine;

/// <summary>
/// [tool_change]: the templates the compiler writes for PRELOAD and TOOL, the preload settings, and the expansion rule
/// of the change (machine-config 3, 5a).
/// </summary>
public sealed record ToolChangeConfig
{
    /// <summary>
    /// change: written for TOOL=n and for a bare TOOL, "T{tool} M6" ({tool} from the VM, D91); null when left out.
    /// </summary>
    public string? Change { get; init; }

    /// <summary>
    /// change_preloaded: written instead of change when the tool is already preloaded, "M6"; null when left out.
    /// </summary>
    public string? ChangePreloaded { get; init; }

    /// <summary>
    /// preload: written for PRELOAD=n, "T{tool}"; null on machines without a magazine, where PRELOAD is dropped with a
    /// WARNING.
    /// </summary>
    public string? Preload { get; init; }

    /// <summary>
    /// unload: written for TOOL=0, "T0 M6"; null when left out.
    /// </summary>
    public string? Unload { get; init; }

    /// <summary>
    /// auto_preload: the compiler inserts PRELOAD of the next tool after each TOOL when the program has none (virtual
    /// machine 3.5); false when left out.
    /// </summary>
    public bool AutoPreload { get; init; }

    /// <summary>
    /// preload_position: where the inserted preload stands; null when left out.
    /// </summary>
    public PreloadPosition? PreloadPosition { get; init; }

    /// <summary>
    /// offsets_with_change: OFFSET words are implicit in the change and not written (Heidenhain TOOL CALL); false when
    /// left out.
    /// </summary>
    public bool OffsetsWithChange { get; init; }

    /// <summary>
    /// tool_name_allowed: TOOL="NAME" is accepted (Heidenhain, Siemens) instead of being mapped to numbers; false when
    /// left out.
    /// </summary>
    public bool ToolNameAllowed { get; init; }

    /// <summary>
    /// kind_map: the value of {kind} per tool kind of the tool table, ROTARY = "0.", TURNING = "1." (machine-config 3,
    /// D52); empty when left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> KindMap { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// pre, post, requires, restore of the tool change, pre = ["HOME Z"] (machine-config 5a); null when the table
    /// has none of the four keys.
    /// </summary>
    public ExpansionRule? Rule { get; init; }
}
