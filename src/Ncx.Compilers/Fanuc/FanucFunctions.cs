using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The M codes of a Fanuc block (controllers fanuc.md 1, 5; controller-mapping 1, 4; machine-config 5): the named
/// functions of [func], the spindles, the coolant channels of [coolant], MFUNC with a WARNING, M0 and M1 for STOP, the
/// workpiece selection of [workpiece], up to three M codes in one block.
/// </summary>
internal static class FanucFunctions
{
    // Up to three M codes per block on 30i (controllers fanuc.md 1).
    // TODO(question): fanuc 1 allows one M code per block "on older controls" without naming them, and dialect names
    // no older control; three are written per block whatever the dialect, until that is answered.
    private const int MostMCodes = 3;

    /// <summary>
    /// The functions of the block, in the order the sources write them: the named functions, the spindles with their
    /// S, the coolant, MFUNC, STOP and the workpiece (the TODO(question) of FanucLine).
    /// </summary>
    public static void Write(FanucBlock write)
    {
        Block block = write.Block;
        foreach (Word word in block.Words)
        {
            if (word.Key == "FUNC" && word.Addr is string name)
            {
                write.Written(word);
                string state = word.Value.ToCanonical();
                AddRendered(write, write.Machine.FindFunction(name, state),
                    $"[func] {name} {state} (machine-config 5)");
            }
        }

        FanucToolWords.AddSpindleFunctions(write);
        foreach (Word word in block.Words)
        {
            if (word.Key == "COOLANT")
            {
                write.Written(word);
                string channel = word.Addr ?? "STANDARD";
                string state = word.Value.ToCanonical();
                string? template = write.Machine.Coolant.TryGetValue(channel, out FunctionTable? table)
                    ? table.States.GetValueOrDefault(state)
                    : null;
                AddRendered(write, template, $"[coolant] {channel} {state} (machine-config 5)");
            }
        }

        AddMFunc(write);
        AddStop(write);
        AddWorkpiece(write);
    }

    /// <summary>
    /// Adds the words of a function to the main line, or to a line after it where the main line holds the most M
    /// codes a block takes (controllers fanuc.md 1).
    /// </summary>
    public static void Add(FanucBlock write, string words)
    {
        int codes = MCodes(words);
        foreach (string written in write.Main.Functions)
        {
            codes += MCodes(written);
        }

        if (codes > MostMCodes && write.Main.Functions.Count > 0)
        {
            write.Following.Add(words);
            return;
        }

        write.Main.Function(words);
    }

    private static void AddRendered(FanucBlock write, string? template, string what)
    {
        if (write.Render(template, what, new TemplateValues()) is string text && text.Length > 0)
        {
            Add(write, text);
        }
    }

    // MFUNC=n is the M code by number, for a function the machine configuration does not name; the compiler warns
    // (language 4.6, MFUNC).
    private static void AddMFunc(FanucBlock write)
    {
        if (write.Block.Find("MFUNC") is not Word mfunc)
        {
            return;
        }

        write.Written(mfunc);
        string number = mfunc.Value.ToCanonical();
        write.Warning(DiagnosticCodes.FanucMFuncWritten,
            $"MFUNC={number} is written as M{number}, a function that no table of the machine names (language 4.6).");
        Add(write, "M" + number);
    }

    // STOP=PROGRAM is M0, STOP=OPTIONAL M1 (controllers fanuc.md 1; controller-mapping 1, STOP).
    private static void AddStop(FanucBlock write)
    {
        if (write.Block.Find("STOP") is Word stop)
        {
            write.Written(stop);
            Add(write, stop.Value.ToCanonical() == "OPTIONAL" ? "M1" : "M0");
        }
    }

    // WORKPIECE=role from the [workpiece] template of the role (machine-config 5, D57).
    // TODO(question): D222: a [workpiece] template may carry the datum, "G54 M428", which the reader reads as ORIGIN
    // where it stands; the template is written as the machine file gives it, and a code of it that the line holds
    // already, G54 of ORIGIN=1 in the same block, stands once, as D222 writes ORIGIN=1 WORKPIECE=MAIN as G54 M428,
    // until D222 is answered.
    private static void AddWorkpiece(FanucBlock write)
    {
        if (write.Block.Find("WORKPIECE") is not Word workpiece)
        {
            return;
        }

        write.Written(workpiece);
        string role = workpiece.Value.ToCanonical();
        string? template = write.Machine.Workpiece?.Templates.GetValueOrDefault(role);
        if (write.Render(template, $"[workpiece] {role} (machine-config 5)", new TemplateValues()) is not string text)
        {
            return;
        }

        var words = new List<string>();
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!write.Main.HasCode(word))
            {
                words.Add(word);
            }
        }

        if (words.Count > 0)
        {
            Add(write, string.Join(' ', words));
        }
    }

    // The M codes among the words of a function: M3, not the S or the P that stands with it.
    private static int MCodes(string words)
    {
        int count = 0;
        foreach (string word in words.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length > 1 && word[0] == 'M' && char.IsAsciiDigit(word[1]))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// A number as a whole number of its text, for the placeholders of the templates.
    /// </summary>
    public static decimal? NumberOf(Value value)
    {
        return value is IntegerValue or DecimalValue
            ? decimal.Parse(value.ToCanonical(), CultureInfo.InvariantCulture)
            : null;
    }
}
