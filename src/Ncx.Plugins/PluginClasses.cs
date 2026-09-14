namespace Ncx.Plugins;

/// <summary>
/// Loads one plugin DLL into a context of its own and makes an object of every public class of it that implements one
/// of the four interfaces (architecture 9, D106; implementation 17, P7-01).
/// </summary>
internal static class PluginClasses
{
    /// <summary>
    /// The objects of the plugin classes of one DLL, in the ordinal order of the full names of their classes (the
    /// TODO(question) of PluginLoader.Files); null, reported with the plugin's name, when the DLL or one of its classes
    /// cannot be loaded, so that nothing of the plugin acts.
    /// </summary>
    /// <param name="name">The name of the plugin.</param>
    /// <param name="file">The DLL as the user finds it, which the diagnostics name.</param>
    /// <param name="fullPath">The full path of the DLL.</param>
    /// <param name="diagnostics">Where a failure is reported.</param>
    public static List<object>? Load(string name, string file, string fullPath, PluginDiagnostics diagnostics)
    {
        if (!File.Exists(fullPath))
        {
            diagnostics.NotLoaded(name, file, "there is no such file");
            return null;
        }

        // A DLL that is no assembly, or whose types need an assembly it does not bring, fails to load
        // (implementation 17, P7-01).
        Type[] types;
        try
        {
            var context = new PluginLoadContext(name, fullPath);
            types = context.LoadFromFile(fullPath).GetExportedTypes();
        }
        catch (Exception exception) when (PluginFaults.IsPluginFault(exception))
        {
            diagnostics.NotLoaded(name, file, PluginFaults.Describe(exception));
            return null;
        }

        var classes = new List<Type>();
        foreach (Type type in types)
        {
            if (PluginLoader.IsPluginClass(type))
            {
                classes.Add(type);
            }
        }

        classes.Sort((first, second) => string.CompareOrdinal(first.FullName, second.FullName));

        // A class is made with its public constructor without parameters; a class that cannot be made fails the load
        // of its plugin.
        var instances = new List<object>();
        foreach (Type type in classes)
        {
            try
            {
                if (Activator.CreateInstance(type) is object instance)
                {
                    instances.Add(instance);
                }
            }
            catch (Exception exception) when (PluginFaults.IsPluginFault(exception))
            {
                diagnostics.NotLoaded(name, file, $"{type.Name} cannot be made: {PluginFaults.Describe(exception)}");
                return null;
            }
        }

        return instances;
    }
}
