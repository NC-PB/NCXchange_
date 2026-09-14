using System.Text.RegularExpressions;
using Ncx.Core.Machine;

namespace Ncx.Readers.Siemens;

/// <summary>
/// Which NCX axis an address of a SINUMERIK block moves (controllers siemens.md 1; controller-mapping 2, IX; language
/// 4.3; D30, D93): the address the [[axis]] table of the machine writes as the letter of an axis, Z2 or C4, is that
/// axis; X Y Z A B C and an address of the machine axis form are the axis of that name, which the virtual machine
/// resolves; the orientation addresses A2 to C5 of TRAORI are no axes.
/// </summary>
internal static partial class SiemensAxes
{
    /// <summary>
    /// The NCX name of the axis a word moves; null for a word that is no axis word.
    /// </summary>
    /// <param name="block">The block, with the machine.</param>
    /// <param name="word">The word.</param>
    public static string? AxisOf(SiemensBlock block, SourceWord word)
    {
        return AxisOf(block.Machine, word.Address);
    }

    /// <summary>
    /// The NCX name of the axis an address names; null for an address that is no axis.
    /// </summary>
    /// <param name="machine">The machine with its [[axis]] table.</param>
    /// <param name="address">The address, "Z2".</param>
    public static string? AxisOf(MachineConfig machine, string address)
    {
        if (!AxisForm().IsMatch(address))
        {
            return null;
        }

        // An axis with an extension is written with its equals sign, Z2=450, C4=DC(90), by the letter the machine
        // file gives it (D30, machine-config 4).
        foreach (AxisDef axis in machine.Axes)
        {
            if (string.Equals(axis.Letter, address, StringComparison.Ordinal))
            {
                return axis.NcxName;
            }
        }

        return IsOrientation(address) ? null : address;
    }

    /// <summary>
    /// The NCX name of a machine axis a G74 or PRESETON names by its machine name, X1 of G74 X1=0 (controllers
    /// siemens.md 3): the axis of that id of [[axis]], else the axis of that letter.
    /// </summary>
    /// <param name="machine">The machine with its [[axis]] table.</param>
    /// <param name="address">The machine axis name.</param>
    public static string? MachineAxisOf(MachineConfig machine, string address)
    {
        return machine.FindAxis(address)?.NcxName ?? AxisOf(machine, address);
    }

    /// <summary>
    /// The orientation addresses of TRAORI: A2 B2 C2 the Euler or RPY angles, A3 B3 C3 the tool direction, A4 B4 C4 and
    /// A5 B5 C5 the surface normals (controllers siemens.md 6).
    /// </summary>
    /// <param name="address">The address.</param>
    public static bool IsOrientation(string address)
    {
        return address.Length == 2 && address[0] is 'A' or 'B' or 'C' && address[1] is >= '2' and <= '5';
    }

    // An axis address: X Y Z A B C U V W with up to two digits (language 3, D93).
    [GeneratedRegex(@"^[XYZABCUVW][0-9]{0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex AxisForm();
}
