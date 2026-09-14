namespace Ncx.Plugins;

// The diagnostic codes of Ncx.Plugins (D98, code-guidelines 6, implementation 00-method 4).
//
// A code is the area prefix PLG and three digits; the codes of Ncx.Core (PAR, VM) live in the DiagnosticCodes class of
// that project. A diagnostic renders as "file(line): ERROR PLG003: message" with the severities ERROR, WARNING and
// INFO, and the message of every one begins with the name of the plugin it is about, "plugin MyShopRules: ..."
// (implementation 17, P7-01). A constant is named after its rule, and a code is never renumbered or reused, so that
// tests and users can rely on it.
//
//   PLG001-PLG099  the plugin loader and the four places a plugin acts in (P7-01)

/// <summary>
/// The diagnostic codes of Ncx.Plugins: one constant per rule, named after the rule, the area prefix PLG and three
/// digits (D98).
/// </summary>
public static class DiagnosticCodes
{
    /// <summary>
    /// PLG001, an INFO: a program rewriter of a plugin inserted generated blocks around a block, "plugin MyShopRules:
    /// inserted 2 blocks at line 12" (D98, code-guidelines 11).
    /// </summary>
    public const string InsertedBlocks = "PLG001";

    /// <summary>
    /// PLG002: a plugin DLL is not there, is no assembly, or one of its classes cannot be made; the plugin is left out
    /// for the run, which goes on without it (implementation 17, P7-01; D106).
    /// </summary>
    public const string PluginNotLoaded = "PLG002";

    /// <summary>
    /// PLG003: a class of a plugin threw in its method, Rewrite, Read, On or Write, or gave no answer; the plugin is
    /// left out for the rest of the run, which goes on without it (implementation 17, P7-01).
    /// </summary>
    public const string PluginFailed = "PLG003";

    /// <summary>
    /// PLG004: a block a program rewriter of a plugin gives does not parse under the option of generated text; its
    /// answer for the block is left out (implementation 17, P7-01; code-guidelines 10.3; D95).
    /// </summary>
    public const string TextDoesNotParse = "PLG004";

    /// <summary>
    /// PLG005, a WARNING: an assembly among the plugins holds no public class that implements one of the four plugin
    /// interfaces, so it does nothing (architecture 9, D106).
    /// </summary>
    public const string NoPluginClass = "PLG005";
}
