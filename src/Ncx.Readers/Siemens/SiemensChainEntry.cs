namespace Ncx.Readers.Siemens;

/// <summary>
/// One entry of the chain of transforms the reader wrote (language 4.2, D31): its kind, SHIFT, ROTATE, MIRROR, TILT or
/// TILT_AXIS, the SINUMERIK instruction it came from, TRANS, ATRANS, ROT, AROT, MIRROR, AMIRROR, ROTS, AROTS, G58, G59
/// or CYCLE800, and the block the reader wrote for it.
/// </summary>
/// <param name="Kind">The kind of the entry.</param>
/// <param name="Instruction">The instruction it came from.</param>
/// <param name="Block">The NCX block of the entry.</param>
internal sealed record SiemensChainEntry(string Kind, string Instruction, SiemensDraftBlock Block);
