namespace Ncx.Readers;

/// <summary>
/// The block range a cycle names as its contour, by the labels of its first and its last block: "10" and "20" of a
/// Fanuc G71 P10 Q20 (machine-config 6, contour; language 4.7.1; D65).
/// </summary>
/// <param name="First">The label of the first block of the contour.</param>
/// <param name="Last">The label of the last block of the contour.</param>
public sealed record SourceContour(string First, string Last);
