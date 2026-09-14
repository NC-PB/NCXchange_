using System.Globalization;
using Ncx.Core.Catalog;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Writing;

namespace Ncx.Core.Expander;

/// <summary>
/// limits = "clamp": the expander rewrites RPM, F and a target beyond the machine limits to the limit, and the WARNING
/// says so (machine-config 1, virtual machine 5, D64). Under limits = "warn", the default, it rewrites nothing, and the
/// virtual machine reports the limits.
/// </summary>
internal static class LimitClamp
{
    // The source a clamped block names (machine-config 1).
    private const string Source = "[machine] limits";

    /// <summary>
    /// Clamps every block the virtual machine executes for a block of the file, each in its place: the generated blocks
    /// before and after it and the block itself. Machine limits that are the same for every program are configuration,
    /// and the expander applies them (virtual machine 7, architecture 9).
    /// </summary>
    public static void Apply(BlockExpansion expansion, MachineConfig machine, Diagnostics diagnostics)
    {
        for (int index = 0; index < expansion.Before.Count; index++)
        {
            expansion.Before[index] = Clamp(expansion.Before[index], expansion.Origin, machine, diagnostics);
        }

        expansion.Block = Clamp(expansion.Block, expansion.Origin, machine, diagnostics);
        for (int index = 0; index < expansion.After.Count; index++)
        {
            expansion.After[index] = Clamp(expansion.After[index], expansion.Origin, machine, diagnostics);
        }
    }

    // The block with every word beyond a limit rewritten to the limit and one WARNING per word, reported on the
    // rewritten block and so on the line of its origin (D98); the block as it is when no word is beyond a limit. A
    // block of the file becomes a block in its place, which ncx format writes as read (language 4.15).
    private static Block Clamp(Block block, Block origin, MachineConfig machine, Diagnostics diagnostics)
    {
        var words = new List<Word>(block.Words);
        var findings = new List<string>();
        for (int index = 0; index < words.Count; index++)
        {
            if (ClampWord(words[index], block, machine, findings) is Word clamped)
            {
                words[index] = clamped;
            }
        }

        if (findings.Count == 0)
        {
            return block;
        }

        Block rewritten = block with { Words = words };
        GeneratedBlock generated = GeneratedText.Rewritten(block, origin, Source, string.Join("; ", findings));
        Block marked = GeneratedText.Mark(rewritten with { SourceText = NcxWriter.WriteBlock(rewritten) }, generated);
        foreach (string finding in findings)
        {
            diagnostics.Warning(marked, DiagnosticCodes.LimitClamped,
                finding + " (machine-config 1, virtual machine 5, D64).");
        }

        return marked;
    }

    // RPM, F and a target of a machine-frame motion, each compared as a number; an expression is not evaluated before
    // the run and is not compared (virtual machine 1).
    private static Word? ClampWord(Word word, Block block, MachineConfig machine, List<string> findings)
    {
        decimal number;
        if (word.Value is IntegerValue integer)
        {
            number = integer.Number;
        }
        else if (word.Value is DecimalValue fraction)
        {
            number = fraction.Number;
        }
        else
        {
            return null;
        }

        return word.Key switch
        {
            "RPM" => ClampSpeed(word, number, machine, findings),
            "F" => ClampFeed(word, number, block, machine, findings),
            _ => ClampTarget(word, number, block, machine, findings),
        };
    }

    // RPM above the rpm_max or below the rpm_min of its spindle (virtual machine 5): the limits of the [spindle.ROLE]
    // table of its role, of the default spindle for RPM without one (machine-config 5; virtual machine 3.8 rule 2).
    private static Word? ClampSpeed(Word word, decimal rpm, MachineConfig machine, List<string> findings)
    {
        string? role = word.Addr ?? StateKeys.RoleOfDefaultSpindle(machine);
        if (role is null || !machine.SpindleTables.TryGetValue(role, out FunctionTable? table))
        {
            return null;
        }

        if (table.RpmMax is decimal max && rpm > max)
        {
            return Clamped(word, max, $"above rpm_max {Text(max)} of [spindle.{role}]", findings);
        }

        if (table.RpmMin is decimal min && rpm < min)
        {
            return Clamped(word, min, $"below rpm_min {Text(min)} of [spindle.{role}]", findings);
        }

        return null;
    }

    // F above the max_feed of an axis that moves (virtual machine 5): the feed is limited by the smallest max_feed of
    // the moving axes (virtual machine 8), which are the axes the axis words of the block name.
    // TODO(question): max_feed is in mm/min (deg/min on a rotary axis), F is in the units and the feed mode of the
    // program, and only the axis words of its own block tell which axes move, while the expander never sees the state
    // (architecture 5.5); F is compared as written, which never rewrites a feed within the limit (an INCH or a PER_REV
    // feed is smaller than its value in mm/min), until that is answered.
    private static Word? ClampFeed(Word word, decimal feed, Block block, MachineConfig machine, List<string> findings)
    {
        decimal? limit = null;
        string limitingAxis = "";
        foreach (Word axisWord in block.Words)
        {
            if (AxisNameOf(axisWord, block) is string name
                && machine.ResolveAxis(name)?.MaxFeed is decimal maxFeed
                && (limit is null || maxFeed < limit))
            {
                limit = maxFeed;
                limitingAxis = name;
            }
        }

        if (limit is decimal max && feed > max)
        {
            return Clamped(word, max, $"above max_feed {Text(max)} of the axis {limitingAxis}", findings);
        }

        return null;
    }

    // A target beyond the axis limits, compared in the MACHINE frame (virtual machine 5, D100): an absolute axis word
    // of a RAPID, LINE or ARC under FRAME=MACHINE is a machine coordinate, compared as written with the limits of its
    // linear axis (an X of a diameter-programmed axis is wave-1 question #4).
    // TODO(question): virtual machine 5 compares every target with the limits in the MACHINE frame, whose position only
    // the virtual machine knows for a workpiece-frame target, and the expander never sees the state (architecture 5.5);
    // only the targets of FRAME=MACHINE blocks are clamped, and the virtual machine reports the others, until that is
    // answered.
    // TODO(question): the limits of a rotary axis are travel limits on a swivel and the display range of a modulo axis
    // (machine-config 4, D100), and no key tells the two apart; a rotary target is not clamped until that is answered.
    private static Word? ClampTarget(Word word, decimal target, Block block, MachineConfig machine,
        List<string> findings)
    {
        bool machineFrameMotion = block.Has("FRAME", null, "MACHINE") && block.Verb?.Key is "RAPID" or "LINE" or "ARC";
        if (!machineFrameMotion || AxisNameOf(word, block) is not string name || IsIncremental(word))
        {
            return null;
        }

        AxisDef? axis = machine.ResolveAxis(name);
        if (axis is null || axis.Kind != AxisKind.Linear)
        {
            return null;
        }

        if (axis.Max is decimal max && target > max)
        {
            return Clamped(word, max, $"beyond the upper limit {Text(max)} of the axis {name}", findings);
        }

        if (axis.Min is decimal min && target < min)
        {
            return Clamped(word, min, $"beyond the lower limit {Text(min)} of the axis {name}", findings);
        }

        return null;
    }

    // The word with the limit as its value, and what the WARNING says of it.
    private static Word Clamped(Word word, decimal limit, string beyond, List<string> findings)
    {
        Word clamped = word with { Value = NumberValues.Of(limit) };
        findings.Add($"{word.ToCanonical()} is {beyond}; limits = \"clamp\" rewrites it to {clamped.ToCanonical()}");
        return clamped;
    }

    // The axis a word of a verb that takes axis words names: X Y Z A B C and the incremental forms without their I,
    // and a machine axis word of the D93 form outside a CYCLE:controller=n block, whose unknown keys are native
    // parameters (language 4.3; D93, D94); null for any other word.
    private static string? AxisNameOf(Word word, Block block)
    {
        if (word.Addr is not null || block.Verb?.Definition is not { TakesAxisWords: true })
        {
            return null;
        }

        if (word.Definition is not null)
        {
            if (!WordCatalog.IsStandardAxis(word.Key))
            {
                return null;
            }

            return word.Key.StartsWith('I') ? word.Key.Substring(1) : word.Key;
        }

        return !WordCatalog.IsNativeParameterAllowed(block)
            && WordCatalog.TryMachineAxis(word.Key, out MachineAxisWord? machineAxis)
                ? machineAxis.AxisName
                : null;
    }

    // The incremental form of an axis word, IX or IZ2 (language 4.3).
    private static bool IsIncremental(Word word)
    {
        if (word.Definition is not null)
        {
            return word.Key.StartsWith('I');
        }

        return WordCatalog.TryMachineAxis(word.Key, out MachineAxisWord? machineAxis) && machineAxis.IsIncremental;
    }

    private static string Text(decimal number)
    {
        return number.ToString(CultureInfo.InvariantCulture);
    }
}
