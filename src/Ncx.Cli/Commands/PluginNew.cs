using System.Security;
using System.Text;
using Ncx.Core.Model;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx plugin new &lt;name&gt;: copies the plugin template next to ncx, templates/ncx-plugin/, into a new folder of the
/// working directory named after the plugin, and renames it, so that it builds and runs unchanged (code-guidelines 11;
/// implementation 17, P7-02). Each file written is named on the standard output. Exit code 0; 1 when a file cannot be
/// written; 2 for a name that is no name of a plugin, a folder that is there already or a missing template (D97).
/// </summary>
internal static class PluginNew
{
    // The template lies in templates/ncx-plugin/ of the folder of ncx, where the build puts it (Ncx.Cli.csproj).
    private const string TemplatesFolder = "templates";
    private const string TemplateName = "ncx-plugin";

    // The name of the plugin of the template, which becomes the name of the new one (code-guidelines 11).
    private const string TemplatePlugin = "MyShopRules";

    // The place of the folder of ncx in the project files of the template.
    private const string NcxFolderPlaceholder = "NCX_FOLDER";

    // What a build of the template in its own folder leaves in bin/ and obj/ is no part of it.
    private static readonly string[] s_buildOutputFolders = ["bin", "obj"];

    // The files of a plugin are UTF-8 text without a byte order mark, as every file ncx writes (language 3, Encoding).
    private static readonly UTF8Encoding s_utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Makes a new plugin from the template.
    /// </summary>
    /// <param name="name">The name of the plugin: MyShopRules.</param>
    /// <param name="settings">The working directory, the folder of ncx and --strict.</param>
    /// <param name="output">The standard output, for the files written.</param>
    /// <param name="error">The standard error, for the diagnostics (D98).</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(string name, PluginCommandSettings settings, TextWriter output, TextWriter error)
    {
        var diagnostics = new Diagnostics(Program.ToolName);

        // The name becomes the folder, the namespace, the project and the DLL of the plugin, which is the plugin's name
        // in plugins/, in ncx.toml and in every diagnostic about it (code-guidelines 11; implementation 17, P7-01). A
        // name that cannot be all of these is a usage error, and so is a folder of that name, which stays as it is
        // (D97).
        string template = Path.Combine(settings.ToolFolder, TemplatesFolder, TemplateName);
        string folder = Path.Combine(settings.WorkingDirectory, name);
        if (!IsPluginName(name))
        {
            diagnostics.Error(Program.CommandLine, DiagnosticCodes.PluginNameInvalid,
                $"\"{name}\" is no name of a plugin: letters, digits and underscores, parts joined by dots, none "
                + "beginning with a digit, since the name becomes the folder, the namespace and the DLL of the plugin "
                + "(code-guidelines 11).");
        }
        else if (!Directory.Exists(template))
        {
            diagnostics.Add(new Diagnostic
            {
                Severity = Severity.Error,
                File = template,
                Line = Program.CommandLine,
                Code = DiagnosticCodes.PluginTemplateMissing,
                Message = "The plugin template is not in the folder of ncx, so there is nothing to copy "
                    + "(code-guidelines 11).",
            });
        }
        else if (Directory.Exists(folder) || File.Exists(folder))
        {
            diagnostics.Error(Program.CommandLine, DiagnosticCodes.PluginFolderExists,
                $"The working directory holds {name} already, and ncx plugin new makes a new folder and changes none; "
                + "choose another name (architecture 10).");
        }

        if (diagnostics.HasErrors)
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.NotStarted;
        }

        Copy(template, name, settings, output, diagnostics);
        error.Write(diagnostics.ToText());
        return ExitCodes.OfRun(diagnostics, settings.Strict);
    }

    // "Copies and renames" (implementation 17, P7-02): every file of the template, MyShopRules replaced by the name in
    // its path and its text, and the folder of ncx written into the project files, so that the plugin and its tests
    // are built against the Ncx assemblies of this ncx (D106). A file that cannot be written is an ERROR of the run,
    // which has started (D97).
    private static void Copy(string template, string name, PluginCommandSettings settings, TextWriter output,
        Diagnostics diagnostics)
    {
        string ncxFolder = SecurityElement.Escape(settings.NcxFolder);
        foreach (string file in FilesOf(template))
        {
            string written = Path.Combine(name, file.Replace(TemplatePlugin, name, StringComparison.Ordinal));
            string path = Path.Combine(settings.WorkingDirectory, written);
            try
            {
                string text = File.ReadAllText(Path.Combine(template, file))
                    .Replace(TemplatePlugin, name, StringComparison.Ordinal)
                    .Replace(NcxFolderPlaceholder, ncxFolder, StringComparison.Ordinal);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? settings.WorkingDirectory);
                File.WriteAllText(path, text, s_utf8);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new Diagnostic
                {
                    Severity = Severity.Error,
                    File = written,
                    Line = Program.CommandLine,
                    Code = DiagnosticCodes.OutputUnwritable,
                    Message = $"The file of the plugin cannot be written: {exception.Message.TrimEnd('.')} "
                        + "(architecture 10).",
                });
                return;
            }

            output.Write(written + "\n");
        }
    }

    // A name of letters, digits and underscores, parts joined by dots, no part beginning with a digit: a namespace of
    // C# and the name of a DLL at once (machine-config 10 names one MyShop.NcxPlugins.dll).
    private static bool IsPluginName(string name)
    {
        foreach (string part in name.Split('.'))
        {
            if (part.Length == 0 || char.IsAsciiDigit(part[0]))
            {
                return false;
            }

            foreach (char character in part)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character != '_')
                {
                    return false;
                }
            }
        }

        return true;
    }

    // The files of the template but bin/ and obj/, each by its path in the template, in the ordinal order of the paths
    // written with forward slashes, so that every system names them in one order.
    private static List<string> FilesOf(string template)
    {
        var files = new List<string>();
        AddFiles(template, template, files);
        files.Sort((first, second) => string.CompareOrdinal(first.Replace('\\', '/'), second.Replace('\\', '/')));
        return files;
    }

    private static void AddFiles(string template, string folder, List<string> files)
    {
        foreach (string file in Directory.GetFiles(folder))
        {
            files.Add(Path.GetRelativePath(template, file));
        }

        foreach (string subfolder in Directory.GetDirectories(folder))
        {
            if (!s_buildOutputFolders.Contains(Path.GetFileName(subfolder)))
            {
                AddFiles(template, subfolder, files);
            }
        }
    }
}
