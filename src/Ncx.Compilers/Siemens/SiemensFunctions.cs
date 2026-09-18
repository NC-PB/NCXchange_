using System.Text.RegularExpressions;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The machine functions of a SINUMERIK block (controllers siemens.md 5; controller-mapping 1, 4; machine-config 5;
/// language 4.1, 4.6, 4.10): COOLANT and FUNC through their tables, MFUNC as the M function with a WARNING, STOP as M0
/// and M1, WORKPIECE through [workpiece] with the mirror of its side.
/// </summary>
internal static partial class SiemensFunctions
{
    // The channel of a COOLANT word without address (virtual machine 2.5).
    private const string DefaultCoolant = "STANDARD";

    // The target key of the settable frame, which a [workpiece] template may select (controllers siemens.md 4).
    private const string DatumGroup = "G54";

    /// <summary>
    /// Writes the functions of the block into the main line, a procedure of a template in a block of its own.
    /// </summary>
    public static void Write(SiemensBlock write)
    {
        foreach (Word word in write.Block.Words)
        {
            switch (word.Key)
            {
                case "COOLANT":
                    write.Written(word);
                    WriteCoolant(write, word);
                    break;
                case "FUNC":
                    write.Written(word);
                    WriteFunction(write, word);
                    break;
                case "MFUNC":
                    write.Written(word);
                    WriteRawFunction(write, word);
                    break;
                case "STOP":
                    // M0 stops the program, M1 is the optional stop (controllers siemens.md 1; controller-mapping 1,
                    // STOP).
                    write.Written(word);
                    write.Main.Function(word.Value.ToCanonical() == "OPTIONAL" ? "M1" : "M0");
                    break;
                case "WORKPIECE":
                    write.Written(word);
                    WriteWorkpiece(write, word);
                    break;
            }
        }
    }

    // COOLANT:channel=ON through [coolant], the channel STANDARD for a COOLANT without address (machine-config 5;
    // virtual machine 2.5).
    private static void WriteCoolant(SiemensBlock write, Word word)
    {
        string channel = word.Addr ?? DefaultCoolant;
        string state = word.Value.ToCanonical();
        string? template = write.Machine.Coolant.GetValueOrDefault(channel)?.States.GetValueOrDefault(state);
        Add(write, template, $"[coolant] {channel} {state} (machine-config 5)");
    }

    // FUNC:name=state through [func] (language 4.6; machine-config 5).
    private static void WriteFunction(SiemensBlock write, Word word)
    {
        string name = word.Addr ?? "";
        string state = word.Value.ToCanonical();
        Add(write, write.Machine.FindFunction(name, state), $"[func] {name} {state} (machine-config 5)");
    }

    // MFUNC=n is the M function n, for functions the machine configuration does not name, and the compiler warns
    // (language 4.6).
    private static void WriteRawFunction(SiemensBlock write, Word word)
    {
        string code = "M" + word.Value.ToCanonical();
        write.Warning(DiagnosticCodes.SiemensRawMFunction,
            $"{word.ToCanonical()} writes {code}, a function the machine configuration does not name (language 4.6).");
        write.Main.Function(code);
    }

    // WORKPIECE=role through [workpiece] (language 4.10; machine-config 5; D57); a side programmed mirrored gets its
    // mirror on, the side left its mirror off. A settable frame the template selects is the datum the control has
    // active afterwards.
    // TODO(question): D222: a [workpiece] template may carry the datum, and millturn1.toml writes datums only (MAIN =
    // "G54", SUB = "G55"); the template is written as the machine file gives it, and an ORIGIN that selects the same
    // datum afterwards is not written again, until D222 is answered.
    private static void WriteWorkpiece(SiemensBlock write, Word word)
    {
        WorkpieceConfig? workpiece = write.Machine.Workpiece;
        string role = word.Value.ToCanonical();
        string? leftRole = RoleOf(write, write.Before.Frame.WorkpieceHolder);
        if (leftRole is not null && leftRole != role
            && workpiece?.Frames.GetValueOrDefault(leftRole) == WorkpieceFrame.Mirror)
        {
            Mirror(write, workpiece, leftRole, "OFF");
        }

        if (workpiece?.Templates.GetValueOrDefault(role) is string template)
        {
            if (write.Render(template, $"[workpiece] {role} (machine-config 5)", new TemplateValues()) is string text)
            {
                AddText(write, text);
                if (Datum().IsMatch(text))
                {
                    write.Target.Set(DatumGroup, text);
                }
            }
        }

        if (workpiece?.Frames.GetValueOrDefault(role) == WorkpieceFrame.Mirror)
        {
            Mirror(write, workpiece, role, "ON");
        }
    }

    // The mirror of a side programmed mirrored: ROLE_mirror of [workpiece] (machine-config 5, D57).
    private static void Mirror(SiemensBlock write, WorkpieceConfig workpiece, string role, string state)
    {
        string? template = workpiece.Mirrors.GetValueOrDefault(role)?.States.GetValueOrDefault(state);
        Add(write, template, $"[workpiece] {role}_mirror {state} (machine-config 5, D57)");
    }

    // The role of a resource id of [roles].
    private static string? RoleOf(SiemensBlock write, string? id)
    {
        foreach (KeyValuePair<string, string> role in write.Machine.Roles)
        {
            if (role.Value == id)
            {
                return role.Key;
            }
        }

        return null;
    }

    private static void Add(SiemensBlock write, string? template, string what)
    {
        if (write.Render(template, what, new TemplateValues()) is string text)
        {
            AddText(write, text);
        }
    }

    // A template text into the main line, a procedure or several lines in blocks of their own.
    private static void AddText(SiemensBlock write, string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (text.Contains('(', StringComparison.Ordinal) || text.Contains('\n', StringComparison.Ordinal))
        {
            write.Write(text);
            return;
        }

        SiemensSpindles.AddToMain(write, text);
    }

    // G54 to G57, G505 to G599 and G500, the settable frames (controllers siemens.md 4).
    [GeneratedRegex("^G(5[4-7]|5[0-9][0-9])$", RegexOptions.CultureInvariant)]
    private static partial Regex Datum();
}
