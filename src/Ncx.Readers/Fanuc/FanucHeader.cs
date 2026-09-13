using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The complete header of a program that every writer emits after PROGRAM=BEGIN, FEED_MODE, COMP=OFF, UNITS,
/// WORKPLANE and CYCLE=OFF (D34), with DIAMETER=ON where the X axis of the machine is programmed in diameters (D60;
/// controller-mapping 1, DIAMETER). The values come from the modal codes of the blocks the program begins with, the
/// header of the source (2.5D_FRAESEN: N10 G0 G40, N20 G80 G90 G94 G98), and where those do not set them from the
/// configuration: units_default for UNITS (controller-mapping 1), the initial state of the virtual machine for the rest
/// (virtual machine 2.1, 2.2). The codes of the header then change nothing and write no word of their own.
/// </summary>
internal static class FanucHeader
{
    // The codes a header block of the source may hold besides its block number (controllers fanuc.md 3): the motion
    // codes without a motion, the plane, the units, the feed mode, G40, G80, G90/G91 and the return level.
    private static readonly string[] s_commonCodes = ["G0", "G1", "G2", "G3", "G17", "G18", "G19", "G40", "G80"];
    private static readonly string[] s_millCodes = ["G20", "G21", "G90", "G91", "G94", "G95", "G98", "G99"];
    private static readonly string[] s_systemACodes = ["G20", "G21", "G98", "G99"];
    private static readonly string[] s_systemCCodes = ["G70", "G71", "G90", "G91", "G94", "G95", "G98", "G99"];

    /// <summary>
    /// The header block of a program, and the values it writes recorded as the modal words the reader has written.
    /// </summary>
    /// <param name="fanuc">The facts of the file, which record the header.</param>
    /// <param name="begin">The block the program begins with, its O line or its first block.</param>
    /// <param name="machine">The machine.</param>
    public static DraftBlock Compose(FanucState fanuc, SourceBlock begin, MachineConfig machine)
    {
        GcodeSystem? system = machine.Machine.GcodeSystem;
        string? units = machine.Machine.UnitsDefault?.ToUpperInvariant() is "MM" or "INCH"
            ? machine.Machine.UnitsDefault.ToUpperInvariant()
            : null;

        // TODO(question): virtual machine 2.1 starts the workplane "from TOML (XY)" and machine-config names no key for
        // it (wave-1 question #73); a program that sets no plane in its header gets WORKPLANE=XY.
        string plane = "XY";
        string feedMode = "PER_MIN";
        int index = fanuc.IndexOf(begin) + (begin.Find("O") is null ? 0 : 1);
        for (; index >= 0 && index < fanuc.Blocks.Count; index++)
        {
            SourceBlock block = fanuc.Blocks[index];
            if (!HoldsHeaderCodesOnly(block, system))
            {
                break;
            }

            foreach (SourceWord word in block.Words)
            {
                switch (NativeCode.Of(word))
                {
                    case "G17":
                        plane = "XY";
                        break;
                    case "G18":
                        plane = "ZX";
                        break;
                    case "G19":
                        plane = "YZ";
                        break;
                    case "G20" or "G70":
                        units = "INCH";
                        break;
                    case "G21" or "G71":
                        units = "MM";
                        break;
                    case "G94" when system != GcodeSystem.A:
                    case "G98" when system == GcodeSystem.A:
                        feedMode = "PER_MIN";
                        break;
                    case "G95" when system != GcodeSystem.A:
                    case "G99" when system == GcodeSystem.A:
                        feedMode = "PER_REV";
                        break;
                }
            }
        }

        var header = new DraftBlock();
        Add(fanuc, header, "FEED_MODE", feedMode);
        Add(fanuc, header, "COMP", "OFF");
        if (units is not null)
        {
            Add(fanuc, header, "UNITS", units);
        }

        Add(fanuc, header, "WORKPLANE", plane);
        if (machine.ResolveAxis("X")?.Programming == Programming.Diameter)
        {
            Add(fanuc, header, "DIAMETER", "ON");
        }

        Add(fanuc, header, "CYCLE", "OFF");
        return header;
    }

    private static void Add(FanucState fanuc, DraftBlock header, string key, string value)
    {
        header.Add(key, new IdentValue(value));
        fanuc.Record(key, value);
    }

    // A block of the source header holds only N and the header codes of its G-code system, and no block skip.
    private static bool HoldsHeaderCodesOnly(SourceBlock block, GcodeSystem? system)
    {
        if (block.BlockSkip)
        {
            return false;
        }

        string[] codes = system switch
        {
            null or GcodeSystem.B => s_millCodes,
            GcodeSystem.A => s_systemACodes,
            _ => s_systemCCodes,
        };
        foreach (SourceWord word in block.Words)
        {
            string? code = word.Address == "G" ? NativeCode.Of(word) : null;
            bool headerCode = code is not null && (s_commonCodes.Contains(code) || codes.Contains(code));
            if (word.Address != "N" && !headerCode)
            {
                return false;
            }
        }

        return true;
    }
}
