namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The verb of a block (language 5 rule 1, virtual machine 2.2): the motion verbs and the frame verbs; a block has at
/// most one.
/// </summary>
public enum Verb
{
    /// <summary>
    /// RAPID: rapid positioning (G0).
    /// </summary>
    Rapid,

    /// <summary>
    /// LINE: linear interpolation at the active feed (G1).
    /// </summary>
    Line,

    /// <summary>
    /// ARC=CW or ARC=CCW: circular interpolation in the working plane.
    /// </summary>
    Arc,

    /// <summary>
    /// RETRACT: retract along the tool axis (D83).
    /// </summary>
    Retract,

    /// <summary>
    /// HOME: reference point return of the named axes (D100).
    /// </summary>
    Home,

    /// <summary>
    /// CYCLE_CALL: execute the active cycle.
    /// </summary>
    CycleCall,

    /// <summary>
    /// SHIFT: a datum shift appended to the transform chain.
    /// </summary>
    Shift,

    /// <summary>
    /// TILT: a tilted working plane by spatial angles.
    /// </summary>
    Tilt,

    /// <summary>
    /// TILT_AXIS: a tilted working plane by rotary axis positions (D82).
    /// </summary>
    TiltAxis,

    /// <summary>
    /// SETPOS: declare the current position (D55).
    /// </summary>
    Setpos,
}
