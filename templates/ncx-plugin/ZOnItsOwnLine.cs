// A second example, an IBlockWriter: it puts Z on a Heidenhain line of its own, the other
// thing plugin authors ask for first. It is commented out, so that the plugin does one thing
// until you want the second. To use it, remove the two slashes in front of every line below.
// The same rule, with its tests, is the sample samples/plugins/ZOnItsOwnLine of NCXchange.
//
// namespace MyShopRules;
//
// /// <summary>
// /// Puts Z on a line of its own: L X+10 Z-5 becomes L X+10 and L Z-5. Z goes down after
// /// the other axes and up before them, so that the tool never moves sideways below the
// /// height it had.
// /// </summary>
// public sealed class ZOnItsOwnLine : IBlockWriter
// {
//     // Every block passes through here once, with the lines the compiler wrote for it,
//     // before they reach the NC file. Change the lines in place.
//     public void Write(BlockWriteEvent blockWrite)
//     {
//         // Before and After are the state of the machine around the block. A block whose Z
//         // is not known on both sides stays as it is: then nobody knows whether Z goes up
//         // or down.
//         if (!blockWrite.Before.Motion.Position.TryGetValue("Z", out AxisPosition zBefore)
//             || !blockWrite.After.Motion.Position.TryGetValue("Z", out AxisPosition zAfter)
//             || !zBefore.Known
//             || !zAfter.Known
//             || zBefore.Frame != zAfter.Frame)
//         {
//             return;
//         }
//
//         bool zGoesUp = zAfter.Value > zBefore.Value;
//         for (int index = 0; index < blockWrite.OutputLines.Count; index++)
//         {
//             // Only a straight line L that moves Z and another axis: L X+10 Z-5.
//             List<string> words = [.. blockWrite.OutputLines[index].Split(' ')];
//             string? z = ZWord(words);
//             if (words[0] != "L" || z is null || AxisWords(words) < 2)
//             {
//                 continue;
//             }
//
//             // Z gets its own line L Z-5. FMAX, the rapid of Heidenhain, counts for its own
//             // line only, so the Z line gets it too.
//             words.Remove(z);
//             string zLine = words.Contains("FMAX") ? "L " + z + " FMAX" : "L " + z;
//             string otherLine = string.Join(' ', words);
//
//             // Up first, down last.
//             blockWrite.OutputLines[index] = zGoesUp ? zLine : otherLine;
//             blockWrite.OutputLines.Insert(index + 1, zGoesUp ? otherLine : zLine);
//             index++;
//         }
//     }
//
//     // The Z word of a line, Z-5; null when the line has none.
//     private static string? ZWord(List<string> words)
//     {
//         foreach (string word in words)
//         {
//             if (word.StartsWith("Z+", StringComparison.Ordinal)
//                 || word.StartsWith("Z-", StringComparison.Ordinal))
//             {
//                 return word;
//             }
//         }
//
//         return null;
//     }
//
//     // How many words of a line are coordinates of an axis: a letter of an axis and a sign.
//     private static int AxisWords(List<string> words)
//     {
//         int count = 0;
//         foreach (string word in words)
//         {
//             if (word.Length > 1 && "XYZABCUVW".Contains(word[0]) && (word[1] == '+' || word[1] == '-'))
//             {
//                 count++;
//             }
//         }
//
//         return count;
//     }
// }
