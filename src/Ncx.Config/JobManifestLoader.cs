using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config;

/// <summary>
/// Loads a job manifest, &lt;name&gt;.ncxjob.toml, into the JobManifest record of the machine model: the machine,
/// the program of each channel, and the spindles and axes the channels share (machine-config 8, D15, D20, D48, F24).
/// </summary>
public static class JobManifestLoader
{
    private static readonly string[] s_rootTables = ["job", "channel", "shared"];
    private static readonly string[] s_rootTableArrays = ["channel"];
    private static readonly string[] s_jobKeys = ["name", "machine"];
    private static readonly string[] s_channelKeys = ["id", "file", "program"];
    private static readonly string[] s_sharedKeys = ["spindles", "axes"];

    /// <summary>
    /// Loads the job manifest at a path. A file that cannot be read is an I/O error, thrown for the composition root
    /// (code-guidelines 6).
    /// </summary>
    /// <param name="path">The job manifest.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The job, or null when the file has an ERROR.</returns>
    public static JobManifest? Load(string path, Diagnostics diagnostics)
    {
        return LoadText(File.ReadAllText(path), diagnostics);
    }

    /// <summary>
    /// Loads a job manifest from its text: unknown keys are WARNINGs, wrong types and missing required keys ERRORs,
    /// each on its line (P2-01).
    /// </summary>
    /// <param name="text">The TOML text of the job manifest.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The job, or null when the file has an ERROR.</returns>
    public static JobManifest? LoadText(string text, Diagnostics diagnostics)
    {
        int errorsBefore = TomlDocument.ErrorCount(diagnostics);
        TomlDocument? document = TomlDocument.Parse(text, diagnostics);
        if (document is null)
        {
            return null;
        }

        ConfigTable root = document.Root("machine-config 8");
        root.WarnUnknownTables(s_rootTables, s_rootTableArrays);

        // [job] names the job and the machine file it runs on; the machine is required (machine-config 8, F24).
        if (!root.Has("job"))
        {
            diagnostics.Error(1, DiagnosticCodes.MissingKey,
                "The job manifest has no [job] table, which is required (machine-config 8).");
        }

        ConfigTable? job = root.Table("job");
        job?.WarnUnknownKeys(s_jobKeys);
        string? name = job?.Text("name");
        string? machine = job?.RequiredText("machine");

        // [[channel]]: the file whose program runs on the channel, the first program of the file unless program names
        // another (machine-config 8, D48); id and file are required.
        var channels = new List<ChannelProgram>();
        foreach (ConfigTable channel in root.Tables("channel", "[[channel]]"))
        {
            channel.WarnUnknownKeys(s_channelKeys);
            int? id = channel.RequiredInteger("id");
            string? file = channel.RequiredText("file");
            string? program = channel.Text("program");
            if (id is int channelId && file is not null)
            {
                channels.Add(new ChannelProgram { Id = channelId, File = file, Program = program });
            }
        }

        // [shared]: the spindles and axes commanded from more than one channel, which the scheduler checks (D20, F24).
        ConfigTable? shared = root.Table("shared");
        shared?.WarnUnknownKeys(s_sharedKeys);
        IReadOnlyList<string> sharedSpindles = shared?.TextList("spindles") ?? [];
        IReadOnlyList<string> sharedAxes = shared?.TextList("axes") ?? [];

        if (machine is null || TomlDocument.ErrorCount(diagnostics) > errorsBefore)
        {
            return null;
        }

        return new JobManifest
        {
            Name = name,
            Machine = machine,
            Channels = channels,
            SharedSpindles = sharedSpindles,
            SharedAxes = sharedAxes,
        };
    }
}
