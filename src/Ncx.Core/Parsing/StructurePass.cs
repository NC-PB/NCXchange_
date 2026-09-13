using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.Parsing;

/// <summary>
/// The pre-pass over the blocks of a file (virtual machine 3.6; 2.1, file.programs and file.subs): the file frame of
/// FILE=BEGIN NCX=1 and FILE=END, the programs PROGRAM=BEGIN ... PROGRAM=END and the subprograms SUB=BEGIN NAME= ...
/// SUB=END as sections, and the structural ERRORs of virtual machine 5 (language 3 EBNF, 4.1, 4.13).
/// </summary>
internal sealed class StructurePass
{
    // The words of the file frame and of the section frames (language 3 EBNF, 4.1, 4.9).
    private const string FileKey = "FILE";
    private const string ProgramKey = "PROGRAM";
    private const string SubKey = "SUB";
    private const string BeginValue = "BEGIN";
    private const string EndValue = "END";
    private const string NcxKey = "NCX";
    private const string NameKey = "NAME";
    private const string NumberKey = "NUMBER";
    private const string ChannelKey = "CHANNEL";
    private const string LabelKey = "LABEL";

    // The format version of language 4.1.
    private const long NcxVersion = 1;

    // The words each frame block holds by its grammar, its frame word first: FILE=BEGIN NCX=1, FILE=END, PROGRAM=BEGIN
    // with the header words NAME, NUMBER and CHANNEL, PROGRAM=END, SUB=BEGIN NAME=, SUB=END (language 3 EBNF, 4.1).
    private static readonly string[] s_fileBeginWords = [FileKey, NcxKey];
    private static readonly string[] s_fileEndWords = [FileKey];
    private static readonly string[] s_programBeginWords = [ProgramKey, NameKey, NumberKey, ChannelKey];
    private static readonly string[] s_programEndWords = [ProgramKey];
    private static readonly string[] s_subBeginWords = [SubKey, NameKey];
    private static readonly string[] s_subEndWords = [SubKey];

    private readonly IReadOnlyList<Block> _blocks;
    private readonly Diagnostics _diagnostics;
    private readonly List<Section> _sections = [];

    // The index of the BEGIN block of the program and of the subprogram the walk stands in; null outside of one.
    private int? _openProgram;
    private int? _openSub;

    private StructurePass(IReadOnlyList<Block> blocks, Diagnostics diagnostics)
    {
        _blocks = blocks;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Finds the file frame and the sections of the blocks of a file and reports the structural ERRORs.
    /// </summary>
    /// <param name="blocks">Every block of the file in file order; trivia is no block.</param>
    /// <param name="diagnostics">Where the ERRORs go.</param>
    public static FileStructure Run(IReadOnlyList<Block> blocks, Diagnostics diagnostics)
    {
        var pass = new StructurePass(blocks, diagnostics);
        Block? fileBegin = pass.CheckFileBegin();
        Block? fileEnd = pass.CheckFileEnd();
        pass.CheckFrameWords();
        pass.WalkSections();
        pass._sections.Sort((first, second) => first.FirstBlock.CompareTo(second.FirstBlock));
        pass.CheckSectionNames();
        pass.CheckProgramPresent(fileBegin);
        return new FileStructure(pass._sections, fileBegin, fileEnd);
    }

    // FILE=BEGIN NCX=1 is the first block of every file and stands on no other; comment-only and blank lines may stand
    // before it, they are trivia (language 4.1; D92; virtual machine 5, missing or misplaced FILE=BEGIN).
    private Block? CheckFileBegin()
    {
        Block? fileBegin = null;
        for (int index = 0; index < _blocks.Count; index++)
        {
            Block block = _blocks[index];
            if (!IsFrameBlock(block, FileKey, BeginValue))
            {
                continue;
            }

            fileBegin ??= block;
            if (index > 0)
            {
                _diagnostics.Error(block, DiagnosticCodes.FileBeginNotFirstBlock,
                    "FILE=BEGIN stands on the first block of the file only (language 4.1).");
            }
        }

        if (_blocks.Count == 0 || !IsFrameBlock(_blocks[0], FileKey, BeginValue))
        {
            _diagnostics.Error(FirstLine(), DiagnosticCodes.FileBeginNotFirstBlock,
                "The first block of the file is FILE=BEGIN NCX=1; only comment-only and blank lines may stand before "
                + "it (language 4.1, D92).");
        }

        if (fileBegin is not null)
        {
            CheckNcxVersion(fileBegin);
        }

        return fileBegin;
    }

    // NCX=1 stands in the FILE=BEGIN block: the format version, and 1 is the version of this language (language 3
    // EBNF, 4.1; virtual machine 5, missing NCX). A value that is no integer is the ERROR of the catalog.
    private void CheckNcxVersion(Block fileBegin)
    {
        Word? ncx = fileBegin.Find(NcxKey);
        if (ncx is null)
        {
            _diagnostics.Error(fileBegin, DiagnosticCodes.NcxVersionMissing,
                "FILE=BEGIN carries the format version NCX=1 in its block (language 3 EBNF, 4.1).");
        }
        else if (ncx.Value is IntegerValue version && version.Number != NcxVersion)
        {
            _diagnostics.Error(fileBegin, DiagnosticCodes.NcxVersionUnknown,
                $"NCX={version.Text} is no format version of this language, which is NCX=1 (language 4.1).");
        }
    }

    // FILE=END is the last block of every file: no block follows it, comment-only and blank lines may (language 4.1;
    // D92; virtual machine 5, missing or misplaced FILE=END).
    private Block? CheckFileEnd()
    {
        Block? fileEnd = null;
        for (int index = 0; index < _blocks.Count; index++)
        {
            Block block = _blocks[index];
            if (!IsFrameBlock(block, FileKey, EndValue))
            {
                continue;
            }

            fileEnd = block;
            if (index < _blocks.Count - 1)
            {
                _diagnostics.Error(block, DiagnosticCodes.FileEndNotLastBlock, string.Create(
                    CultureInfo.InvariantCulture,
                    $"FILE=END is the last block of the file, and line {_blocks[index + 1].Line} holds a block after "
                    + $"it; only comment-only and blank lines may follow it (language 4.1, D92)."));
            }
        }

        if (fileEnd is null)
        {
            _diagnostics.Error(LastLine(), DiagnosticCodes.FileEndNotLastBlock,
                "The last block of the file is FILE=END, and the file has none (language 4.1).");
        }

        return fileEnd;
    }

    // The blocks of the file frame and of the section frames hold the words their grammar gives them, and NCX stands
    // in the FILE=BEGIN block only; SUB=BEGIN carries the NAME of its subprogram (language 3 EBNF, 4.1, 4.9; virtual
    // machine 5, misplaced NCX).
    private void CheckFrameWords()
    {
        foreach (Block block in _blocks)
        {
            Word? frameWord = FrameWordOf(block);
            string[]? frameWords = frameWord is null ? null : FrameWordsOf(frameWord);
            foreach (Word word in block.Words)
            {
                if (word.Key == NcxKey && !IsFrameBlock(block, FileKey, BeginValue))
                {
                    _diagnostics.Error(block, DiagnosticCodes.NcxMisplaced,
                        "NCX stands in the FILE=BEGIN block, the first block of the file, and nowhere else "
                        + "(language 4.1).");
                }
                else if (frameWord is not null && frameWords is not null && !frameWords.Contains(word.Key))
                {
                    _diagnostics.Error(block, DiagnosticCodes.StructuralBlockOtherWord,
                        FrameRule(frameWord, frameWords) + $", not {word.ToCanonical()} (language 3 EBNF, 4.1).");
                }
            }

            if (IsFrameBlock(block, SubKey, BeginValue) && !block.Has(NameKey))
            {
                _diagnostics.Error(block, DiagnosticCodes.SubNameMissing,
                    "SUB=BEGIN carries the NAME of its subprogram, SUB=BEGIN NAME=100 (language 3 EBNF, 4.9, 4.13).");
            }
        }
    }

    // Programs and subprograms stand in the file side by side, each from its BEGIN block to its END block, which is
    // its last block and appears once (language 4.13; virtual machine 3.6, 5).
    private void WalkSections()
    {
        for (int index = 0; index < _blocks.Count; index++)
        {
            Block block = _blocks[index];
            if (IsFrameBlock(block, ProgramKey, BeginValue))
            {
                BeginProgram(index);
            }
            else if (IsFrameBlock(block, ProgramKey, EndValue))
            {
                EndProgram(index);
            }
            else if (IsFrameBlock(block, SubKey, BeginValue))
            {
                BeginSub(index);
            }
            else if (IsFrameBlock(block, SubKey, EndValue))
            {
                EndSub(index);
            }
            else if (IsFrameBlock(block, FileKey, EndValue))
            {
                CloseOpenSections(index, "before FILE=END");
            }
            else if (!IsFrameBlock(block, FileKey, BeginValue))
            {
                OrdinaryBlock(block);
            }
        }

        CloseOpenSections(_blocks.Count, "at the end of the file");
    }

    // A program begins; programs and subprograms do not nest, so a section still open ends before it without its END
    // block (language 4.13).
    private void BeginProgram(int index)
    {
        CloseOpenSections(index, string.Create(
            CultureInfo.InvariantCulture, $"before the PROGRAM=BEGIN of line {_blocks[index].Line}"));
        _openProgram = index;
    }

    // PROGRAM=END ends its program exactly once, as its last block; outside a program it is an ERROR, and a
    // subprogram still open inside the program ends before it without its SUB=END (language 4.13).
    private void EndProgram(int index)
    {
        Block block = _blocks[index];
        if (_openProgram is not int program)
        {
            string where = _openSub is null ? "outside a program" : "in a subprogram, which ends with SUB=END";
            _diagnostics.Error(block, DiagnosticCodes.ProgramEndOutsideProgram,
                $"PROGRAM=END stands {where}; it ends its program exactly once, as the last block of the program "
                + "(language 4.13).");
            return;
        }

        if (_openSub is int sub)
        {
            ReportMissingEnd(SectionKind.Sub, sub, block.Line,
                string.Create(CultureInfo.InvariantCulture, $"before the PROGRAM=END of line {block.Line}"));
            AddSection(SectionKind.Sub, sub, index - 1);
            _openSub = null;
        }

        AddSection(SectionKind.Program, program, index);
        _openProgram = null;
    }

    // Subprograms stand in the file next to the programs, never inside one: SUB=BEGIN inside a program is an ERROR,
    // and the subprogram is read as a section of its own (language 4.9, 4.13; virtual machine 3.6, 5). A subprogram
    // still open ends before it without its SUB=END.
    private void BeginSub(int index)
    {
        Block block = _blocks[index];
        if (_openSub is int openSub)
        {
            ReportMissingEnd(SectionKind.Sub, openSub, block.Line,
                string.Create(CultureInfo.InvariantCulture, $"before the SUB=BEGIN of line {block.Line}"));
            AddSection(SectionKind.Sub, openSub, index - 1);
        }

        if (_openProgram is int program)
        {
            _diagnostics.Error(block, DiagnosticCodes.SubInsideProgram, string.Create(CultureInfo.InvariantCulture,
                $"SUB=BEGIN stands inside the program of line {_blocks[program].Line}; subprograms stand in the file "
                + $"next to the programs, never inside one (language 4.9, 4.13)."));
        }

        _openSub = index;
    }

    // SUB=END ends its subprogram as its last block, and stands nowhere else (language 4.13).
    private void EndSub(int index)
    {
        if (_openSub is not int sub)
        {
            _diagnostics.Error(_blocks[index], DiagnosticCodes.SubEndOutsideSub,
                "SUB=END stands outside a subprogram; it ends the subprogram that SUB=BEGIN begins, as its last block "
                + "(language 4.13).");
            return;
        }

        AddSection(SectionKind.Sub, sub, index);
        _openSub = null;
    }

    // Every block stands inside a program or a subprogram, and END is no label: it is reserved for JUMP=END
    // (language 4.9, 4.13; virtual machine 3.6, 5).
    private void OrdinaryBlock(Block block)
    {
        if (_openProgram is null && _openSub is null)
        {
            _diagnostics.Error(block, DiagnosticCodes.BlockOutsideSection,
                "The block stands outside every program and subprogram; a block stands between PROGRAM=BEGIN and "
                + "PROGRAM=END or between SUB=BEGIN and SUB=END (language 4.13).");
        }

        if (block.Has(LabelKey, null, EndValue))
        {
            _diagnostics.Error(block, DiagnosticCodes.LabelEnd,
                "LABEL=END is no label: END is reserved for JUMP=END, which continues at the PROGRAM=END of the "
                + "program (language 4.9).");
        }
    }

    // The sections still open end before the block of this index without their END block, each an ERROR on the line
    // where it should have ended (language 4.13; virtual machine 5, missing PROGRAM=END or SUB=END).
    private void CloseOpenSections(int nextIndex, string where)
    {
        int line = nextIndex < _blocks.Count ? _blocks[nextIndex].Line : LastLine();
        if (_openSub is int sub)
        {
            ReportMissingEnd(SectionKind.Sub, sub, line, where);
            AddSection(SectionKind.Sub, sub, nextIndex - 1);
            _openSub = null;
        }

        if (_openProgram is int program)
        {
            ReportMissingEnd(SectionKind.Program, program, line, where);
            AddSection(SectionKind.Program, program, nextIndex - 1);
            _openProgram = null;
        }
    }

    private void ReportMissingEnd(SectionKind kind, int beginIndex, int line, string where)
    {
        int beginLine = _blocks[beginIndex].Line;
        if (kind == SectionKind.Program)
        {
            _diagnostics.Error(line, DiagnosticCodes.ProgramEndMissing, string.Create(CultureInfo.InvariantCulture,
                $"The program of line {beginLine} has no PROGRAM=END {where}; PROGRAM=END is the last block of every "
                + $"program and appears exactly once (language 4.13)."));
            return;
        }

        _diagnostics.Error(line, DiagnosticCodes.SubEndMissing, string.Create(CultureInfo.InvariantCulture,
            $"The subprogram of line {beginLine} has no SUB=END {where}; SUB=END is the last block of every "
            + $"subprogram (language 4.13)."));
    }

    // A section from its BEGIN block to its last block, with the name, number and channel of its BEGIN block; a
    // subprogram has neither number nor channel (language 4.1, 4.13, 4.14).
    private void AddSection(SectionKind kind, int firstBlock, int lastBlock)
    {
        Block begin = _blocks[firstBlock];
        bool isProgram = kind == SectionKind.Program;
        _sections.Add(new Section
        {
            Kind = kind,
            Name = NameOf(begin),
            Number = isProgram ? IntegerOf(begin, NumberKey) : null,
            Channel = (isProgram ? IntegerOf(begin, ChannelKey) : null) ?? 1,
            FirstBlock = firstBlock,
            LastBlock = lastBlock,
        });
    }

    // A name names one section of the file: CALL finds a subprogram by it, and a CALL that names a program is an
    // ERROR, so programs and subprograms share the names; NAME=100 and NAME="100" are the same name (virtual
    // machine 3.6, 5).
    private void CheckSectionNames()
    {
        var firstByName = new Dictionary<string, Section>(StringComparer.Ordinal);
        foreach (Section section in _sections)
        {
            if (section.Name is null)
            {
                continue;
            }

            if (firstByName.TryGetValue(section.Name, out Section? first))
            {
                _diagnostics.Error(_blocks[section.FirstBlock], DiagnosticCodes.DuplicateSectionName,
                    string.Create(CultureInfo.InvariantCulture,
                        $"The name {section.Name} is taken by the section of line {_blocks[first.FirstBlock].Line}; "
                        + $"every program and subprogram of a file has a name of its own (virtual machine 3.6)."));
                continue;
            }

            firstByName.Add(section.Name, section);
        }
    }

    // A file holds one or more programs (language 4.1, 4.13; virtual machine 5).
    private void CheckProgramPresent(Block? fileBegin)
    {
        foreach (Section section in _sections)
        {
            if (section.Kind == SectionKind.Program)
            {
                return;
            }
        }

        _diagnostics.Error(fileBegin?.Line ?? FirstLine(), DiagnosticCodes.FileWithoutProgram,
            "The file holds no program; a file holds one or more programs and any number of subprograms between "
            + "FILE=BEGIN and FILE=END (language 4.1, 4.13).");
    }

    private int FirstLine()
    {
        return _blocks.Count > 0 ? _blocks[0].Line : 1;
    }

    private int LastLine()
    {
        return _blocks.Count > 0 ? _blocks[^1].Line : 1;
    }

    // A frame block carries FILE, PROGRAM or SUB with BEGIN or END, without an address.
    private static bool IsFrameBlock(Block block, string key, string value)
    {
        return block.Has(key, null, value);
    }

    // The first frame word of a block; null for a block that carries none.
    private static Word? FrameWordOf(Block block)
    {
        foreach (Word word in block.Words)
        {
            bool isFrameKey = word.Key is FileKey or ProgramKey or SubKey;
            if (isFrameKey && word.Addr is null && word.Value is IdentValue { Name: BeginValue or EndValue })
            {
                return word;
            }
        }

        return null;
    }

    // The words the grammar gives the block of a frame word (language 3 EBNF, 4.1).
    private static string[] FrameWordsOf(Word frameWord)
    {
        bool begins = frameWord.Value is IdentValue { Name: BeginValue };
        return frameWord.Key switch
        {
            FileKey => begins ? s_fileBeginWords : s_fileEndWords,
            ProgramKey => begins ? s_programBeginWords : s_programEndWords,
            _ => begins ? s_subBeginWords : s_subEndWords,
        };
    }

    // "FILE=END is a block of its own", "PROGRAM=BEGIN takes NAME, NUMBER and CHANNEL in its block".
    private static string FrameRule(Word frameWord, string[] frameWords)
    {
        string frame = frameWord.ToCanonical();
        if (frameWords.Length == 1)
        {
            return $"{frame} is a block of its own";
        }

        string others = frameWords.Length == 2
            ? frameWords[1]
            : string.Join(", ", frameWords, 1, frameWords.Length - 2) + " and " + frameWords[^1];
        return $"{frame} takes {others} in its block";
    }

    // The name of a section: the content of a string, an identifier, or the digits of an integer (language 4.1, 4.9).
    private static string? NameOf(Block begin)
    {
        return begin.Find(NameKey)?.Value switch
        {
            StringValue text => text.Content,
            IdentValue identifier => identifier.Name,
            IntegerValue integer => integer.Text,
            _ => null,
        };
    }

    // An integer header word, NUMBER or CHANNEL; null without one or beyond the range of an int.
    private static int? IntegerOf(Block begin, string key)
    {
        if (begin.Find(key)?.Value is IntegerValue integer && integer.Number is >= int.MinValue and <= int.MaxValue)
        {
            return (int)integer.Number;
        }

        return null;
    }
}
