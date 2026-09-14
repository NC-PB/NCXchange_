# Tool vector change

The tool vector change of virtual machine 8 and architecture 9 (implementation 14, P4-03): per `MOTION` the angle between the tool vector at its start and at its end, from `TX TY TZ` when the program gives them (D81) and else from the rotary axes, with the minimum, maximum and mean per verb, a histogram over configurable bins, the ten largest changes with their lines and vectors, and the motions skipped and why, over the block range of D67.

Start with `ToolVectorConvention.cs`, the convention the phase file documents until the kinematics module exists (virtual machine 10; D24 keeps kinematics out of NCX), which the report header repeats: the tool axis is the normal of the `WORKPLANE`; a rotary axis named A, B or C turns it about the machine X, Y or Z axis in the order of `[[axis]]`; a head axis (owner: the tool spindle) turns the tool, a table axis (owner: a work spindle or a table) the workpiece, and so the tool vector the other way. It is enough for the change within a motion; it is not a pose. `ConventionAxis.cs` is one such axis, `ToolVector.cs` the vector with its turn and its angle. `ToolVectorAnalytic.cs` is the listener, `ToolVectorAnalytic.Report.cs` writes the report.

Never here: a pose, a position of the tool tip, or anything the kinematics module will compute from the `[[node]]` tree (machine-config 9).
