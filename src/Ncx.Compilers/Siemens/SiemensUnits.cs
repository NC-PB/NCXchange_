namespace Ncx.Compilers.Siemens;

/// <summary>
/// One output file per unit (controllers siemens.md 1, 12 rule 1; machine-config 2): under program_layout =
/// "file_per_program" every program and every subprogram of the archive the compiler wrote becomes a file of its own,
/// NAME.mpf or NAME.spf; the archive holds each subprogram once and every unit under a name of its own
/// (SiemensFile.UnitNameOf), so every unit is a file.
/// </summary>
internal static class SiemensUnits
{
    // The extensions of the files of a main program and a subprogram (controllers siemens.md 1, MPF and SPF).
    private const string ProgramExtension = ".mpf";
    private const string SubExtension = ".spf";

    /// <summary>
    /// The units of an archive as files of their own: a unit runs from its %_N_NAME_MPF or %_N_NAME_SPF line to the
    /// line before the next; the header line itself stays out of the file, which is named after the unit. Lines before
    /// the first header stay at the head of the first unit, so that no line is dropped (language 2 rule 8).
    /// </summary>
    // TODO(question): siemens 12 rule 1 writes the %_N_ headers "when program_layout = one_file, else one file per
    // unit", and siemens 1 says the header is not part of the text on the control itself; a file of one unit is written
    // without its header, until that is answered.
    public static List<CompiledFile> Split(CompiledFile archive)
    {
        var files = new List<CompiledFile>();
        string? name = null;
        var text = new System.Text.StringBuilder();
        foreach (string line in LinesOf(archive.Text))
        {
            string content = line.TrimEnd('\r', '\n');
            if (UnitName(content) is string unit)
            {
                if (name is not null)
                {
                    files.Add(new CompiledFile { Name = name, Text = text.ToString() });
                    text.Clear();
                }

                name = unit;
                continue;
            }

            text.Append(line);
        }

        files.Add(new CompiledFile { Name = name ?? archive.Name, Text = text.ToString() });
        return files;
    }

    // The file name of a header line: SHAFT.mpf of %_N_SHAFT_MPF, L100.spf of %_N_L100_SPF; null for another line.
    private static string? UnitName(string line)
    {
        if (!line.StartsWith(SiemensProgramFrame.HeaderPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        string unit = line.Substring(SiemensProgramFrame.HeaderPrefix.Length);
        if (unit.EndsWith(SiemensProgramFrame.ProgramSuffix, StringComparison.Ordinal))
        {
            return string.Concat(unit.AsSpan(0, unit.Length - SiemensProgramFrame.ProgramSuffix.Length),
                ProgramExtension);
        }

        return unit.EndsWith(SiemensProgramFrame.SubSuffix, StringComparison.Ordinal)
            ? string.Concat(unit.AsSpan(0, unit.Length - SiemensProgramFrame.SubSuffix.Length), SubExtension)
            : null;
    }

    // The lines of a text with their line endings.
    private static List<string> LinesOf(string text)
    {
        var lines = new List<string>();
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                lines.Add(text.Substring(start, index + 1 - start));
                start = index + 1;
            }
        }

        if (start < text.Length)
        {
            lines.Add(text.Substring(start));
        }

        return lines;
    }
}
