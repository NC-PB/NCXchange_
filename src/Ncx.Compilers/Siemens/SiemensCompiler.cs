using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// Writes an NCX program as a SINUMERIK 840D sl program (controllers siemens.md 12; controller-mapping, the Siemens
/// column): the units %_N_NAME_MPF and %_N_NAME_SPF with PROC, the equals sign after every address with an extension, T
/// and M6 per [tool_change] with D, S and M3 with SETMS in a block of its own, S2= and M2=3 for the other spindles, the
/// frame chain as TRANS, ATRANS, AROT and AMIRROR, CYCLE800 from [transform], the cycles with their signature and
/// MCALL, the flow as GOTOF, WAITM from [sync], CYCLE832 for TOLERANCE, RAW:SIEMENS verbatim. One concern per file:
/// SiemensProgramFrame, SiemensMotion with SiemensArcs, SiemensTools, SiemensSpindles, SiemensFunctions, SiemensFrames,
/// SiemensCycles, SiemensFlow, SiemensChannels.
/// </summary>
public sealed class SiemensCompiler : CompilerBase, ICompiler
{
    // What the compiler knows of the file being written across its blocks.
    private SiemensFile? _file;

    /// <summary>
    /// The Siemens family, the SINUMERIK 840D and 840D sl (controllers siemens.md).
    /// </summary>
    public override Controller Controller => Controller.Siemens;

    /// <summary>
    /// A SINUMERIK program is an .mpf file, the extension of every program of the corpus (controllers siemens.md 1;
    /// controller-mapping 11.1).
    /// </summary>
    protected override string FileExtension => ".mpf";

    /// <summary>
    /// Compiles one NCX file: under program_layout = "one_file" into one archive with the %_N_ headers, else into one
    /// file per unit (controllers siemens.md 12 rule 1; machine-config 2, D48).
    /// </summary>
    CompileResult ICompiler.Compile(NcxProgram program, MachineConfig machine, CompileOptions options)
    {
        if (machine.Format is not OutputFormat format || format.ProgramLayout != ProgramLayout.FilePerProgram)
        {
            return Compile(program, machine, options);
        }

        // The units are written as one archive and cut at their headers, so that every subprogram stands once, in a
        // file of its own, and not after every program that calls it as the framework lays them out.
        MachineConfig archive = machine with { Format = format with { ProgramLayout = ProgramLayout.OneFile } };
        CompileResult result = Compile(program, archive, options);
        var files = new List<CompiledFile>();
        foreach (CompiledFile file in result.Files)
        {
            files.AddRange(SiemensUnits.Split(file));
        }

        return result with { Files = files };
    }

    /// <summary>
    /// A comment with a semicolon (controllers siemens.md 1).
    /// </summary>
    protected override string CommentLine(string text)
    {
        return "; " + text;
    }

    /// <summary>
    /// The unit headers, the PROC lines, the comment lines, the lines of a skipped block, whose skip mark stands before
    /// the N number, and a RAW line with a number of its own take no block number of block_numbers (controllers
    /// siemens.md 1; machine-config 2).
    /// </summary>
    protected override bool TakesBlockNumber(string line)
    {
        if (line.Length == 0)
        {
            return true;
        }

        bool ownNumber = line.Length > 1 && line[0] == 'N' && char.IsAsciiDigit(line[1]);
        return !(line[0] is '%' or ';' or '/' || ownNumber || line.StartsWith("PROC ", StringComparison.Ordinal));
    }

    /// <summary>
    /// Writes one block: the structure of the file, RAW verbatim, and every other block concern by concern in the
    /// order the control executes them, state before motion (language 5 rule 3): the label, the frames, the tool
    /// change, the spindles, the transformations, the modal call, the main line with the modal codes, the motion and
    /// the functions, then the call at the current position, HOME, SETPOS, RETRACT, the dwell, the variables, the
    /// flow, the channels and the comment.
    /// </summary>
    protected override void WriteBlock(Block block, ChannelSnapshot before, ChannelSnapshot after)
    {
        if (_file is null || block.Has("FILE", null, "BEGIN"))
        {
            _file = SiemensFile.Of(LookAhead.Steps);
        }

        string fileStem = Path.GetFileNameWithoutExtension(Diagnostics.File);
        var write = new SiemensBlock
        {
            Step = Step,
            Machine = Machine,
            Target = Target,
            Numbers = Numbers,
            Templates = Templates,
            Diagnostics = Diagnostics,
            File = _file,
            Writer = Line,
            CommentText = CommentText,
        };
        _file.MeetJumpsOverCalls(write);
        if (!SiemensProgramFrame.WriteBegin(write, fileStem))
        {
            if (!WriteRaw(write))
            {
                WriteWords(write, fileStem);
            }

            SiemensProgramFrame.WriteEnd(write);
        }

        write.ReportUnwritten();
    }

    // RAW of this controller or of the machine's builder is written verbatim (controllers siemens.md 12 rule 6;
    // language 4.1, D5), and what the control has active is not known after it: the modal codes, the feed, the edge,
    // the datum, the diameter programming and the master spindle, which the next block that needs them writes again.
    // The modal call of the compiler ends before it, since every block with a position, one in the RAW text too, would
    // call it, and a jump in the RAW text would carry it to its label (controllers siemens.md 7, 8).
    private static bool WriteRaw(SiemensBlock write)
    {
        if (RawText(write.Block) is not string raw)
        {
            return false;
        }

        write.Written("RAW");
        write.Written("SKIP");
        SiemensCycles.EndModalCall(write);
        if (raw.StartsWith('/'))
        {
            write.Writer(raw);
        }
        else
        {
            write.Write(raw);
        }

        SiemensMotion.ForgetAfterRaw(write);
        foreach (string key in new[] { "D", "SETMS", "G54", "DIAMON" })
        {
            write.MakeUnknown(key);
        }

        SiemensCycles.AfterRaw(write, raw);
        SiemensProgramFrame.WriteComment(write);
        return true;
    }

    private void WriteWords(SiemensBlock write, string fileStem)
    {
        write.Written("SKIP");
        SiemensFlow.WriteLabel(write);
        SiemensFrames.WriteChain(write);
        WriteTools(write);
        SiemensSpindles.Write(write);
        SiemensFrames.WriteTransformations(write);
        SiemensCycles.WriteBefore(write);
        SiemensMotion.WriteModalWords(write);
        SiemensFrames.WriteOrigin(write);
        SiemensFrames.WriteDiameter(write);
        SiemensMotion.Write(write);
        SiemensTools.ReportTwoRegisters(write);
        SiemensTools.WriteOffset(write);
        SiemensFunctions.Write(write);
        write.WriteMain();
        SiemensCycles.WriteAfter(write);
        SiemensFrames.WriteVerbs(write);
        SiemensMotion.WriteDwell(write);
        SiemensFlow.WriteVariables(write);
        SiemensFlow.Write(write, fileStem);
        SiemensChannels.Write(write);
        SiemensProgramFrame.WriteComment(write);
    }

    // The tool change and the preload are written from [tool_change] by the framework, with {offset} the D of the
    // holder (machine-config 3; controllers siemens.md 12 rule 3; virtual machine 3.5); the framework writes the lines
    // without the skip mark of the block.
    private void WriteTools(SiemensBlock write)
    {
        Block block = write.Block;
        bool change = block.Has("TOOL");
        bool preload = block.Has("PRELOAD");
        if (!change && !preload)
        {
            return;
        }

        write.Written("TOOL");
        write.Written("PRELOAD");
        if (block.Skip)
        {
            write.Error(DiagnosticCodes.SiemensSkipOnToolChange,
                "SKIP on a block with TOOL or PRELOAD: the tool change is written from [tool_change] without the skip "
                + "mark /, so the block cannot be written as skipped (controller-mapping 1, SKIP).");
            return;
        }

        if (change)
        {
            WriteToolChange(SiemensTools.ChangeValues(write));
            SiemensTools.AfterChange(write, ChangeTemplate(write));
        }

        if (preload)
        {
            WritePreload();
        }
    }

    // The template the framework wrote the change with: unload for TOOL=0, change_preloaded for a preloaded tool where
    // the machine has it, change otherwise (machine-config 3).
    private static string? ChangeTemplate(SiemensBlock write)
    {
        ToolChangeConfig? config = write.Machine.ToolChange;
        if (write.After.LastHolder is not string holder
            || !write.After.Holders.TryGetValue(holder, out HolderSnapshot? after))
        {
            return config?.Change;
        }

        if (after.SpindleTool.Number == 0)
        {
            return config?.Unload;
        }

        bool preloaded = write.Before.Holders.TryGetValue(holder, out HolderSnapshot? before)
            && before.Preloaded == after.SpindleTool;
        return preloaded && config?.ChangePreloaded is not null ? config.ChangePreloaded : config?.Change;
    }
}
