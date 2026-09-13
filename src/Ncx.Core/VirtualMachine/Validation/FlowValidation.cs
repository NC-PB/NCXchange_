using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The control flow (language 4.9, 4.13; virtual machine 2.7, 3.6, 3.9, 5; D89, D99): labels and jump targets, the call
/// of a subprogram (raised by the STATIC walk), JUMP=END from a subprogram, RETURN in a program, and the blocks no
/// LABEL makes reachable. The labels and targets are checked by the pre-pass over the file, before execution.
/// </summary>
internal static class FlowValidation
{
    // JUMP=END continues at the PROGRAM=END of the current program; END is no label (language 4.9, virtual machine
    // 2.7).
    private const string EndTarget = "END";

    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Flow",
        Summary = "Labels, jumps, calls and the reach of the blocks of a program (language 4.9, 4.13; VM 2.7, 3.6, "
            + "3.9, 5; D89, D99).",
        Rules =
        [
            ValidationRule.Error(DiagnosticCodes.LabelEnd, "LABEL=END; END is reserved for JUMP=END.",
                "language 4.9; VM 2.7, 5"),
            ValidationRule.Error(DiagnosticCodes.CallDepthExceeded,
                "Call depth exceeded; the subprogram is not entered.", "VM 3.6, 3.9, 5; D99"),
            ValidationRule.Error(DiagnosticCodes.CallOfProgram, "CALL of a program.", "language 4.13; VM 3.6, 5"),
            ValidationRule.Error(DiagnosticCodes.CallTargetMissing, "Missing call target.", "VM 3.6, 5"),
            ValidationRule.Error(DiagnosticCodes.DuplicateLabel, "Duplicate LABEL in one program or subprogram.",
                "language 4.9; VM 3.6, 5"),
            ValidationRule.Error(DiagnosticCodes.JumpTargetMissing,
                "Missing jump target: a JUMP or REPEAT to a label its program or subprogram does not hold.",
                "language 4.9; VM 3.6, 5"),
            ValidationRule.Warning(DiagnosticCodes.JumpEndInSub,
                "JUMP=END from inside a subprogram: it ends the program from a call.", "VM 3.6, 5"),
            ValidationRule.Warning(DiagnosticCodes.UnreachableBlock,
                "Unreachable block of a program after an unconditional JUMP that no LABEL makes reachable.",
                "language 4.13; VM 3.9, 5; D89"),
            ValidationRule.Warning(DiagnosticCodes.ReturnInProgram,
                "RETURN in the main program; it is treated as JUMP=END.", "language 4.9, 4.13; VM 3.6, 5"),
            ValidationRule.Error(DiagnosticCodes.BlockCapExceeded,
                "Block cap exceeded: possible endless loop. INTERPRETED mode.", "VM 3.6, 5; machine-config 7"),
            ValidationRule.Error(DiagnosticCodes.RepeatDepthExceeded,
                "A REPEAT nested deeper than the configured depth, which calls and repeats share. INTERPRETED mode.",
                "VM 3.6, 5; machine-config 7"),
            ValidationRule.Error(DiagnosticCodes.ExternalProgramNotFound,
                "Missing call target: an external program the working directory does not hold. INTERPRETED mode.",
                "language 4.9; VM 3.6, 5"),
            ValidationRule.Error(DiagnosticCodes.ExternalProgramContradictsCaller,
                "An external program that contradicts the caller's UNITS or WORKPLANE. INTERPRETED mode.", "VM 3.6"),
            ValidationRule.Error(DiagnosticCodes.ProgramToRunMissing,
                "The program the command line or the job names is not a program of the file. INTERPRETED mode.",
                "language 4.13; VM 3.6"),
        ],
    };

    /// <summary>
    /// The pre-pass over one program or subprogram (virtual machine 3.6): its labels, duplicates and missing targets
    /// being ERRORs before execution; JUMP=END from a subprogram and RETURN in a program; the blocks of a program
    /// that no LABEL makes reachable (virtual machine 3.9, 5).
    /// </summary>
    public static void CheckSection(NcxProgram program, Section section, Diagnostics diagnostics)
    {
        Dictionary<string, Block> labels = CollectLabels(program, section, diagnostics);
        for (int index = section.FirstBlock; index <= section.LastBlock; index++)
        {
            Block block = program.Blocks[index];
            CheckTarget(block, "JUMP", labels, section, diagnostics);
            CheckTarget(block, "REPEAT", labels, section, diagnostics);
            CheckEarlyEnd(block, section, diagnostics);
        }

        if (section.Kind == SectionKind.Program)
        {
            CheckReachable(program, section, diagnostics);
        }
    }

    // A LABEL is the jump target and repeat start of its program or subprogram and unique there; a duplicate is an
    // ERROR before execution (language 4.9, virtual machine 3.6).
    private static Dictionary<string, Block> CollectLabels(NcxProgram program, Section section, Diagnostics diagnostics)
    {
        var labels = new Dictionary<string, Block>(StringComparer.Ordinal);
        for (int index = section.FirstBlock; index <= section.LastBlock; index++)
        {
            Block block = program.Blocks[index];
            if (block.Find("LABEL") is not Word label)
            {
                continue;
            }

            string name = label.Value.ToCanonical();
            if (labels.TryGetValue(name, out Block? first))
            {
                diagnostics.Error(block, DiagnosticCodes.DuplicateLabel, string.Create(CultureInfo.InvariantCulture,
                    $"{label.ToCanonical()} stands in line {first.Line} of {SectionName(section)} already; a label is "
                    + $"unique per program or subprogram (language 4.9, virtual machine 3.6)."));
                continue;
            }

            labels.Add(name, block);
        }

        return labels;
    }

    // JUMP sets pc to a LABEL of the current section, REPEAT repeats the blocks from its label; a target the section
    // does not hold is an ERROR before execution (language 4.9, virtual machine 3.6). JUMP=END needs no label.
    private static void CheckTarget(Block block, string key, Dictionary<string, Block> labels, Section section,
        Diagnostics diagnostics)
    {
        if (block.Find(key) is not Word jump
            || (key == "JUMP" && jump.Value is IdentValue { Name: EndTarget })
            || labels.ContainsKey(jump.Value.ToCanonical()))
        {
            return;
        }

        diagnostics.Error(block, DiagnosticCodes.JumpTargetMissing,
            $"{jump.ToCanonical()}: {SectionName(section)} holds no LABEL={jump.Value.ToCanonical()}; a jump continues "
            + "at a label of its own program or subprogram (language 4.9, virtual machine 3.6).");
    }

    // JUMP=END from inside a subprogram is a WARNING: it ends the program from a call, as a Fanuc M30 in a subprogram
    // does. RETURN in a program, which no caller entered, is a WARNING and treated as JUMP=END (language 4.9, 4.13;
    // virtual machine 3.6, 5).
    private static void CheckEarlyEnd(Block block, Section section, Diagnostics diagnostics)
    {
        if (section.Kind == SectionKind.Sub && block.Has("JUMP", null, EndTarget))
        {
            diagnostics.Warning(block, DiagnosticCodes.JumpEndInSub,
                $"JUMP=END in {SectionName(section)} ends the program from a call, as a Fanuc M30 in a subprogram does "
                + "(virtual machine 3.6).");
        }

        if (section.Kind == SectionKind.Program && block.Has("RETURN"))
        {
            diagnostics.Warning(block, DiagnosticCodes.ReturnInProgram,
                $"RETURN in {SectionName(section)} returns to no caller and is treated as JUMP=END; a program ends "
                + "early with JUMP=END (language 4.9, 4.13, virtual machine 3.6).");
        }
    }

    // Inside a program, a block after an unconditional JUMP that no LABEL makes reachable is unreachable: a WARNING per
    // block (language 4.13, virtual machine 3.9, D89). A JUMP is unconditional without IF (language 4.9) and without
    // SKIP, whose block the switch of the control may skip (language 4.1); RETURN in a program is treated as JUMP=END
    // (virtual machine 3.6). A LABEL makes its block and the blocks after it reachable, and PROGRAM=END is the target
    // of JUMP=END (language 4.9, virtual machine 2.7), so it is never unreachable.
    // TODO(question): virtual machine 3.9 and language 4.13 name the unreachable block "inside a program" and virtual
    // machine 5 lists it without that restriction; the blocks of a subprogram are not checked until that is answered.
    private static void CheckReachable(NcxProgram program, Section section, Diagnostics diagnostics)
    {
        Block? unconditionalJump = null;
        for (int index = section.FirstBlock + 1; index < section.LastBlock; index++)
        {
            Block block = program.Blocks[index];
            if (block.Has("LABEL"))
            {
                unconditionalJump = null;
            }

            if (unconditionalJump is not null)
            {
                string jump = unconditionalJump.Find("JUMP")?.ToCanonical() ?? "RETURN";
                diagnostics.Warning(block, DiagnosticCodes.UnreachableBlock, string.Create(
                    CultureInfo.InvariantCulture,
                    $"The block follows the unconditional {jump} of line {unconditionalJump.Line}, and no LABEL makes "
                    + $"it reachable (language 4.13, virtual machine 3.9, D89)."));
                continue;
            }

            if ((block.Has("JUMP") || block.Has("RETURN")) && !block.Has("IF") && !block.Skip)
            {
                unconditionalJump = block;
            }
        }
    }

    // "the program SLOT_ROW", "the subprogram 100".
    private static string SectionName(Section section)
    {
        string kind = section.Kind == SectionKind.Program ? "the program" : "the subprogram";
        return section.Name is null ? kind : kind + " " + section.Name;
    }
}
