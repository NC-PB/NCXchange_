namespace Ncx.Readers.Heidenhain;

/// <summary>
/// One entry of the chain of transforms the reader wrote (language 4.2, D31): the SHIFT of cycle 7, the MIRROR of
/// cycle 8, the ROTATE of cycle 10, the TILT of PLANE SPATIAL, the TILT_AXIS of PLANE AXIAL and cycle 19, with the NCX
/// block that wrote it.
/// </summary>
/// <param name="Kind">SHIFT, MIRROR, ROTATE, TILT or TILT_AXIS.</param>
/// <param name="Block">The NCX block that appended the entry.</param>
internal sealed record HeidenhainChainEntry(string Kind, HeidenhainDraftBlock Block);
