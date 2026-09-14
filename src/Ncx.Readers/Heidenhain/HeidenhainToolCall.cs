using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// TOOL CALL and TOOL DEF (controllers heidenhain.md 4, 7 rule 2; controller-mapping 1 WORKPLANE, 3; language 4.4;
/// virtual machine 3.5): TOOL CALL n Z S gives TOOL=n, RPM and both offset words with n, because the length and the
/// radius come from the tool table with the call; a changed axis letter gives WORKPLANE; TOOL CALL S alone gives RPM;
/// TOOL DEF n gives PRELOAD=n.
/// </summary>
internal static class HeidenhainToolCall
{
    /// <summary>
    /// Reads a TOOL CALL or TOOL DEF block.
    /// </summary>
    /// <param name="block">The block being read, whose first words are TOOL CALL or TOOL DEF.</param>
    public static void Read(HeidenhainBlock block)
    {
        block.MarkLeading(2);
        if (block.Keyword(1) == "DEF")
        {
            ReadPreload(block);
        }
        else
        {
            ReadCall(block);
        }
    }

    // TOOL CALL 4 Z S1592: tool 4 in the spindle, Z as the tool axis, which also sets the working plane, speed 1592
    // (controllers heidenhain.md 4). The offsets come from the tool table with the call, so the reader emits both
    // offset words with the number of the tool (heidenhain 7 rule 2; language 2 rule 4).
    // TODO(question): heidenhain 4 names the F of TOOL CALL without saying what it does (the modal feed, or the feed
    // that F AUTO takes), and OFFSET:LEN and OFFSET:RAD take a register number (language 4.4) that a tool called by
    // name has not; a TOOL CALL with F, or by name, is kept RAW.
    private static void ReadCall(HeidenhainBlock block)
    {
        SourceWord? number = NumberWord(block);
        SourceWord? axis = AxisWord(block);
        SourceWord? speed = block.Take("S");
        foreach (SourceWord word in block.Unread())
        {
            string reason = word.Address switch
            {
                "F" => "heidenhain 4 does not say what the F of TOOL CALL does",
                "DL" or "DR" or "DR2" => $"the delta offset {word.Address} has no NCX word (language 4.4)",
                "" when word.Text.StartsWith('"') => "a tool called by name has no offset register for OFFSET:LEN and "
                    + "OFFSET:RAD (language 4.4)",
                _ => $"{word.Address}{word.Text} has no NCX word in a TOOL CALL",
            };
            block.Draft.KeepAsRaw(reason);
            return;
        }

        if (number is null && axis is not null)
        {
            block.Draft.KeepAsRaw("a TOOL CALL with an axis and without a tool number has no NCX word");
            return;
        }

        if (axis is not null)
        {
            ReadAxis(block, axis);
        }

        // TOOL CALL S2000 without a number only changes the speed (controllers heidenhain.md 4; controller-mapping 3;
        // wave-1 question #9 asks whether the iTNC 530 takes it).
        if (speed is not null && ValueOf(block, speed) is Value rpm)
        {
            block.Draft.AddState("RPM", null, rpm);
        }

        if (number?.Number is not decimal tool)
        {
            return;
        }

        int toolNumber = decimal.ToInt32(tool);
        IntegerValue value = Integer(toolNumber);
        block.Draft.AddState("TOOL", null, value);
        block.Draft.AddState("OFFSET", "LEN", value);
        block.Draft.AddState("OFFSET", "RAD", value);
        if (block.State.Preloaded is ToolRef preloaded && preloaded.Number == toolNumber)
        {
            block.State.Preloaded = null;
        }

        // TOOL ends the cycle of NCX (virtual machine 4), while the control keeps the definition: the reader writes it
        // again before its next call.
        block.Heidenhain.CycleOn = false;
        block.Heidenhain.CycleCalled = false;
    }

    // The tool axis letter of the TOOL CALL sets the working plane (controller-mapping 1, WORKPLANE): Z the plane XY, Y
    // the plane ZX, X the plane YZ; a changed axis letter gives WORKPLANE (heidenhain 7 rule 2).
    private static void ReadAxis(HeidenhainBlock block, SourceWord axis)
    {
        Workplane plane = axis.Address switch
        {
            "X" => Workplane.YZ,
            "Y" => Workplane.ZX,
            _ => Workplane.XY,
        };
        HeidenhainState heidenhain = block.Heidenhain;
        if (heidenhain.Plane != plane)
        {
            block.Draft.AddState("WORKPLANE", null, new IdentValue(plane.ToString().ToUpperInvariant()));
            heidenhain.Pole = null;
            heidenhain.Tangent = null;
        }

        heidenhain.Plane = plane;
        block.State.Workplane = plane;
    }

    // TOOL DEF 5 prepares the next tool, PRELOAD=5; TOOL DEF 0 clears the preload (controllers heidenhain.md 4;
    // language 4.4); TOOL DEF "NAME" prepares a tool by name.
    private static void ReadPreload(HeidenhainBlock block)
    {
        SourceWord? number = NumberWord(block);
        SourceWord? name = block.Find("");
        if (number is null && name is not null && name.Text.Length > 2 && name.Text.StartsWith('"')
            && name.Text.EndsWith('"'))
        {
            block.MarkRead(name);
            block.Draft.AddState("PRELOAD", null, new StringValue(name.Text.Substring(1, name.Text.Length - 2)));
            block.State.Preloaded = null;
            return;
        }

        if (number?.Number is not decimal tool || block.Unread().Count > 0)
        {
            block.Draft.KeepAsRaw("TOOL DEF names no tool number");
            return;
        }

        int toolNumber = decimal.ToInt32(tool);
        block.Draft.AddState("PRELOAD", null, Integer(toolNumber));
        block.State.Preloaded = toolNumber == 0 ? null : new ToolRef(toolNumber);
    }

    // The tool number, a whole number after TOOL CALL or TOOL DEF.
    private static SourceWord? NumberWord(HeidenhainBlock block)
    {
        SourceWord? word = block.Find("");
        if (word?.Number is decimal number && number >= 0 && number == decimal.Truncate(number)
            && number <= int.MaxValue && !word.Text.Contains('.') && !word.Text.Contains(','))
        {
            block.MarkRead(word);
            return word;
        }

        return null;
    }

    // The tool axis letter, X, Y or Z without a value.
    private static SourceWord? AxisWord(HeidenhainBlock block)
    {
        foreach (string letter in new[] { "X", "Y", "Z" })
        {
            if (block.Find(letter) is SourceWord word && word.Text.Length == 0)
            {
                block.MarkRead(word);
                return word;
            }
        }

        return null;
    }

    private static Value? ValueOf(HeidenhainBlock block, SourceWord word)
    {
        Value? value = HeidenhainExpression.ValueOf(word.Text, block.Line, block.Diagnostics.File, out string? problem);
        if (value is null)
        {
            block.Draft.KeepAsRaw(problem!);
        }

        return value;
    }

    private static IntegerValue Integer(int number)
    {
        return new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture));
    }
}
