using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers;

// The words of the file structure and the written blocks that carry them (language 3 EBNF, 4.1, 4.9, 4.13).
internal sealed partial class StructurePass
{
    private const string FileKey = "FILE";
    private const string ProgramKey = "PROGRAM";
    private const string SubKey = "SUB";
    private const string NcxKey = "NCX";
    private const string NameKey = "NAME";
    private const string NumberKey = "NUMBER";
    private const string ChannelKey = "CHANNEL";
    private const string LabelKey = "LABEL";
    private const string JumpKey = "JUMP";
    private const string ReturnKey = "RETURN";
    private const string BeginValue = "BEGIN";
    private const string EndValue = "END";

    // The label after the header that a Fanuc M99 in a program jumps back to (controller-mapping 6).
    private const string StartLabel = "START";

    // The format version of language 4.1.
    private const long NcxVersion = 1;

    // A block of the file structure that stands on its own line among the blocks of a source block.
    private static ReadStep Write(SourceBlock source, int line, IReadOnlyList<Word> words)
    {
        return new ReadStep { Kind = ReadStepKind.Write, Source = source, Line = line, Words = words };
    }

    // A block that stands in the place of its source block: it carries the block skip of the source and may take its
    // comment (language 4.1, SKIP; controller-mapping 1 and 6).
    private ReadStep InPlace(int index, IReadOnlyList<Word> words)
    {
        SourceBlock source = _blocks[index];
        return Write(source, source.Line, words) with { CarriesSkip = true, TakesComment = true };
    }

    // FILE=BEGIN, PROGRAM=END, JUMP=END: a key with an identifier.
    private static Word Frame(string key, string value)
    {
        return new Word { Key = key, Value = new IdentValue(value) };
    }

    private static Word Number(string key, long number)
    {
        return new Word { Key = key, Value = new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture)) };
    }

    // A section name is an identifier, an integer or a string with SUB=BEGIN, a string with PROGRAM=BEGIN (language
    // 4.1, D90); a label an integer or an identifier (language 4.9).
    private static Value NameOrLabelValue(string text)
    {
        if (IsDigits(text) && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long number))
        {
            return new IntegerValue(number, text);
        }

        return IsIdentifier(text) ? new IdentValue(text) : new StringValue(text);
    }

    private static bool IsDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    // [A-Z][A-Z0-9_]* (language 3, identifier).
    private static bool IsIdentifier(string text)
    {
        if (text.Length == 0 || !char.IsAsciiLetterUpper(text[0]))
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}
