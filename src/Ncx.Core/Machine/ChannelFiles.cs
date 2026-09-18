namespace Ncx.Core.Machine;

/// <summary>
/// [format] channel_files: how the job compiler names the output file of each channel of a job (implementation 16,
/// P6-02; controller-mapping 7, CHANNEL; controllers fanuc.md 1).
/// </summary>
public enum ChannelFiles
{
    /// <summary>
    /// channel_files = "path_suffix": the memory card convention of Nakamura, O1000 for the first channel of [machine]
    /// channels and O1000.P-2, O1000.P-3 for the others, the O number of the channel's program.
    /// </summary>
    PathSuffix,

    /// <summary>
    /// channel_files = "channel_suffix": the job name with _C1, _C2 per channel and the extension of the controller,
    /// TEST 5_C1.MPF on the STAMA.
    /// </summary>
    ChannelSuffix,

    /// <summary>
    /// channel_files = "program_name": the NAME of the channel's program with the extension of the controller, 1000
    /// and 2000 for the units %_N_1000_MPF and %_N_2000_MPF of the DMG templates.
    /// </summary>
    ProgramName,
}
