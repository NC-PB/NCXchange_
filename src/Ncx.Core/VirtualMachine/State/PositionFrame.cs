namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The frame the position of an axis is known in (virtual machine 2.2, 3.4): UNKNOWN for an axis unknown in every
/// frame, the workpiece frame, the MACHINE frame after HOME or a FRAME=MACHINE move (D35), the polar frame under
/// POLAR=ON and the cylinder frame under CYLINDER=n (D102).
/// </summary>
public enum PositionFrame
{
    /// <summary>
    /// Unknown in every frame, the start value of the position frame of every axis (virtual machine 2.2).
    /// </summary>
    Unknown,

    /// <summary>
    /// The workpiece frame: ORIGIN and the transform chain of the current workpiece holder (D31, D57).
    /// </summary>
    Workpiece,

    /// <summary>
    /// The MACHINE frame, the G53 / M91 frame of the reference points and limits (D35, D100).
    /// </summary>
    Machine,

    /// <summary>
    /// The polar frame of POLAR=ON: the face plane of the X word and the C word as a length (D102).
    /// </summary>
    Polar,

    /// <summary>
    /// The cylinder frame of CYLINDER=n: the cylinder axis and the C word as a length on the circumference (D102).
    /// </summary>
    Cylinder,
}
