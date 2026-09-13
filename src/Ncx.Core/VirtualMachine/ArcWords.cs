using Ncx.Core.Geometry;
using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The words of an ARC block by their part in the arc (language 4.3, virtual machine 3.2): the end point on the two
/// plane axes, the tool-axis word of a helix, the other axis words, CENTER on the plane axes and outside them, R and
/// ANGLE.
/// </summary>
internal sealed class ArcWords
{
    /// <summary>
    /// The axis word on the first plane axis: X or IX in the XY plane.
    /// </summary>
    public Word? FirstEnd { get; private set; }

    /// <summary>
    /// The axis word on the second plane axis: Y or IY in the XY plane.
    /// </summary>
    public Word? SecondEnd { get; private set; }

    /// <summary>
    /// The axis word on the tool axis, which makes a helix (virtual machine 3.2).
    /// </summary>
    public Word? ToolEnd { get; private set; }

    /// <summary>
    /// The axis words on the other axes of the block.
    /// </summary>
    public List<Word> OtherAxes { get; } = [];

    /// <summary>
    /// CENTER on the first plane axis, absolute or incremental.
    /// </summary>
    public Word? FirstCenter { get; private set; }

    /// <summary>
    /// CENTER on the second plane axis, absolute or incremental.
    /// </summary>
    public Word? SecondCenter { get; private set; }

    /// <summary>
    /// CENTER on an axis that is not an axis of the plane.
    /// </summary>
    public Word? OutsideCenter { get; private set; }

    /// <summary>
    /// R, the signed radius.
    /// </summary>
    public Word? Radius { get; private set; }

    /// <summary>
    /// ANGLE, the sweep of D84.
    /// </summary>
    public Word? Angle { get; private set; }

    /// <summary>
    /// True when the block carries a CENTER word on any axis.
    /// </summary>
    public bool HasCenter => FirstCenter is not null || SecondCenter is not null || OutsideCenter is not null;

    /// <summary>
    /// Sorts the words of an ARC block by the axes of its working plane.
    /// </summary>
    /// <param name="context">The ARC block with its axis words resolved (step 2).</param>
    /// <param name="plane">The working plane of the arc.</param>
    public static ArcWords Of(BlockContext context, Plane plane)
    {
        var words = new ArcWords
        {
            Radius = context.Block.Find("R"),
            Angle = context.Block.Find("ANGLE"),
        };

        foreach (Word word in context.Block.Words)
        {
            if (context.AxisOf.ContainsKey(word))
            {
                words.AddAxisWord(word, MotionRules.AxisName(word), plane);
            }
            else if (word.Key == "CENTER" && word.Addr is string address)
            {
                words.AddCenterWord(word, MotionRules.WithoutIncrement(address), plane);
            }
        }

        return words;
    }

    private void AddAxisWord(Word word, string axisName, Plane plane)
    {
        if (axisName == plane.FirstAxis)
        {
            FirstEnd = word;
        }
        else if (axisName == plane.SecondAxis)
        {
            SecondEnd = word;
        }
        else if (axisName == plane.ToolAxis)
        {
            ToolEnd = word;
        }
        else
        {
            OtherAxes.Add(word);
        }
    }

    private void AddCenterWord(Word word, string axisName, Plane plane)
    {
        if (axisName == plane.FirstAxis)
        {
            FirstCenter = word;
        }
        else if (axisName == plane.SecondAxis)
        {
            SecondCenter = word;
        }
        else
        {
            OutsideCenter = word;
        }
    }
}
