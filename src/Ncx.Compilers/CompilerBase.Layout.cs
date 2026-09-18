using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers;

// The output files (machine-config 2, D48; language 4.13, D99): the programs and subprograms of the NCX file laid out
// per program_layout, and the warning block of D10 at the head of a program whose {kind} took the default.
public abstract partial class CompilerBase
{
    // What the warning block of D10 names when the machine file names no tool table.
    private const string NoToolTable = "NONE, THE MACHINE FILE NAMES NO TOOL_TABLE";

    // The output files of the compile, per program_layout (machine-config 2, D48).
    // TODO(question): machine-config 2 gives program_layout no default; a machine file that leaves it out writes one
    // file, as "one_file", the first value it lists, until that is answered.
    private List<CompiledFile> Files()
    {
        NcxProgram program = _program ?? throw NotCompiling();
        string stem = Path.GetFileNameWithoutExtension(program.FileName);
        return Machine.Format?.ProgramLayout == ProgramLayout.FilePerProgram ? FilePerProgram(stem) : OneFile(stem);
    }

    // program_layout = "one_file": every program and subprogram of the NCX file in one output file (machine-config 2,
    // D48), the programs in file order, each SUB once, after the first program that calls it (language 4.13, D99).
    // TODO(question): language 4.13 places a subprogram after the M30 of its caller, and no document says where one
    // that no program calls goes in one file; it follows the last program, before FILE=END, until that is answered.
    private List<CompiledFile> OneFile(string stem)
    {
        NcxProgram source = _program ?? throw NotCompiling();
        OutputBuffer output = NewOutput();
        output.Add(_fileBegin);
        var placed = new List<Section>();
        foreach (ProgramText program in _programs)
        {
            WriteProgram(output, program);
            var subs = new List<Section>();
            foreach (Section sub in source.Subs)
            {
                if (program.CalledSubs.Contains(sub) && !placed.Contains(sub))
                {
                    subs.Add(sub);
                    placed.Add(sub);
                }
            }

            WriteSubprograms(output, subs);
            output.Add(program.Footer);
        }

        var uncalled = new List<Section>();
        foreach (Section sub in source.Subs)
        {
            if (!placed.Contains(sub))
            {
                uncalled.Add(sub);
            }
        }

        WriteSubprograms(output, uncalled);
        output.Add(_fileEnd);
        return [new CompiledFile { Name = stem + FileExtension, Text = output.ToText(Diagnostics) }];
    }

    // program_layout = "file_per_program": one output file per program, the subprograms copied after the M30 of every
    // program that calls them (machine-config 2, D48; language 4.13, D99).
    // TODO(question): under "file_per_program" a subprogram is written once per calling program (D99), and no
    // document says what becomes of one that no program calls; it stands in no file, with a WARNING, until that is
    // answered.
    private List<CompiledFile> FilePerProgram(string stem)
    {
        NcxProgram source = _program ?? throw NotCompiling();
        var files = new List<CompiledFile>();
        var names = new List<string>();
        var called = new List<Section>();
        foreach (ProgramText program in _programs)
        {
            OutputBuffer output = NewOutput();
            output.Add(_fileBegin);
            WriteProgram(output, program);
            var subs = new List<Section>();
            foreach (Section sub in source.Subs)
            {
                if (program.CalledSubs.Contains(sub))
                {
                    subs.Add(sub);
                    called.Add(sub);
                }
            }

            WriteSubprograms(output, subs);
            output.Add(program.Footer);
            output.Add(_fileEnd);
            string name = FileNameOf(program.Program, stem, names) + FileExtension;
            files.Add(new CompiledFile { Name = name, Text = output.ToText(Diagnostics) });
        }

        foreach (Section sub in source.Subs)
        {
            if (!called.Contains(sub))
            {
                Diagnostics.Warning(source.Blocks[sub.FirstBlock], DiagnosticCodes.UncalledSubprogramNotWritten,
                    $"SUB {sub.Name} is called by no program, and under program_layout = \"file_per_program\" a "
                    + "subprogram stands in the file of every program that calls it, so it is in no output file "
                    + "(machine-config 2, language 4.13, D99).");
            }
        }

        return files;
    }

    // The lines of a program, with the warning block of D10 at its head when a {kind} took the default.
    private void WriteProgram(OutputBuffer output, ProgramText program)
    {
        output.Add(program.Lines.GetRange(0, program.HeadEnd));
        var tools = new List<ToolRef>(program.ToolsWithoutData);
        if (ReferenceEquals(program, _programs[0]))
        {
            foreach (ToolRef tool in _toolsWithoutDataOutsidePrograms)
            {
                if (!tools.Contains(tool))
                {
                    tools.Add(tool);
                }
            }
        }

        if (tools.Count > 0)
        {
            output.Add(ToolDataWarning(program, tools));
        }

        output.Add(program.Lines.GetRange(program.HeadEnd, program.Lines.Count - program.HeadEnd));
    }

    // Each SUB section once, as its first walk wrote it (D99), in the order of the file.
    private void WriteSubprograms(OutputBuffer output, IReadOnlyList<Section> subs)
    {
        foreach (Section sub in subs)
        {
            if (_subs.TryGetValue(sub, out SubText? text))
            {
                output.Add(text.Lines);
            }
        }
    }

    // D10: without the tool table, or for a tool it does not describe, {kind} takes the default and the compiler
    // writes a warning block at the head of the program that names the expected file and the tools it lacks, so that
    // nobody runs the program by accident. Its lines pass BLOCK_WRITE as lines of PROGRAM=BEGIN.
    // TODO(question): D10 does not say what the warning block is made of, nor whether it stands in every program of a
    // machine without a tool table or only where a template needed the tool data. It is comment lines of the
    // controller, written where a template with {kind} took the default, and it stops nothing, until that is
    // answered.
    private List<OutputLine> ToolDataWarning(ProgramText program, List<ToolRef> tools)
    {
        Block block = program.Begin.Block;
        var toolNames = new List<string>();
        foreach (ToolRef tool in tools)
        {
            toolNames.Add(tool.ToString());
        }

        string[] texts =
        [
            "WARNING: TOOL DATA MISSING, D10",
            "EXPECTED TOOL TABLE: " + (Options.ToolTableFile ?? NoToolTable),
            $"TOOLS WITHOUT DATA: {string.Join(", ", toolNames)} - KIND {DefaultKind} WRITTEN, CHECK BEFORE RUNNING",
        ];
        var lines = new List<string>();
        foreach (string text in texts)
        {
            string written = CommentCharset.Transliterate(text, Machine.Format?.CommentCharset, block, Diagnostics);
            lines.Add(CommentLine(written));
        }

        return OutputLinesOf(RaiseBlockWrite(program.Begin, lines), block);
    }

    private OutputBuffer NewOutput()
    {
        return new OutputBuffer(Machine.Format, BlockNumberPrefix, TakesBlockNumber, OwnBlockNumber);
    }

    // TODO(question): under "file_per_program" the file of a program is named after the program, as Klartext names a
    // file after its program (controllers heidenhain.md 1), and no document names the file of a program without NAME.
    // It takes the name of the NCX file, and a name that is taken already gets _2, _3 in the order of the programs,
    // until that is answered.
    private static string FileNameOf(Section program, string stem, List<string> names)
    {
        string name = program.Name ?? stem;
        string candidate = name;
        for (int suffix = 2; names.Contains(candidate); suffix++)
        {
            candidate = name + "_" + suffix.ToString(CultureInfo.InvariantCulture);
        }

        names.Add(candidate);
        return candidate;
    }
}
