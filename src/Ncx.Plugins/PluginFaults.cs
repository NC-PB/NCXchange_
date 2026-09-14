using System.Reflection;

namespace Ncx.Plugins;

/// <summary>
/// What counts as the fault of a plugin, and how it is told: whatever the code of a plugin throws, while it loads or
/// in one of its methods, since a failing plugin must never take the command line down (implementation 17, P7-01;
/// code-guidelines 6).
/// </summary>
internal static class PluginFaults
{
    /// <summary>
    /// Every exception from the code of a plugin is its own fault, reported with its name; only a process that ran out
    /// of memory is past reporting anything.
    /// </summary>
    public static bool IsPluginFault(Exception exception)
    {
        return exception is not OutOfMemoryException;
    }

    /// <summary>
    /// What was thrown and its message, on one line, as a diagnostic tells it: "InvalidOperationException: the shop
    /// table is missing". A constructor's own exception is told, not the reflection that carried it.
    /// </summary>
    public static string Describe(Exception exception)
    {
        Exception thrown = exception is TargetInvocationException { InnerException: Exception inner }
            ? inner
            : exception;
        return thrown.GetType().Name + ": " + thrown.Message.ReplaceLineEndings(" ");
    }
}
