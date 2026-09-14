namespace Ncx.Core.Model;

// The codes of the job scheduler, VM850-VM899 (the ranges in DiagnosticCodes.cs, D98; implementation 16, P6-01): the
// rules of a job of several channels beyond the deadlock at SYNC (VM571) and the spindle shared between marks (VM572),
// which the channel family of the validation holds since phase 1. Every code has its row in the channel family of the
// table of the validation (VirtualMachine/Validation/ChannelValidation.cs).
public static partial class DiagnosticCodes
{
    /// <summary>
    /// VM850, a WARNING: two channels command one axis of the job's [shared] axes between two marks, checked like the
    /// spindles (virtual machine 3.7; machine-config 8, D20, F24).
    /// </summary>
    public const string AxisSharedBetweenMarks = "VM850";

    /// <summary>
    /// VM851: WITH, WAIT_CHANNEL or START_CHANNEL names a channel that the job does not run (language 4.8; virtual
    /// machine 3.7).
    /// </summary>
    public const string ChannelNotInJob = "VM851";

    /// <summary>
    /// VM852, a WARNING: START_CHANNEL of a channel that runs or has run already; nothing starts (language 4.8; virtual
    /// machine 3.7).
    /// </summary>
    public const string ChannelAlreadyStarted = "VM852";

    /// <summary>
    /// VM853, a WARNING: the CHANNEL of the program's header names another channel than the job runs the program on;
    /// the channel of the job applies (language 4.14; virtual machine 2.8; machine-config 8).
    /// </summary>
    public const string ChannelOtherThanHeader = "VM853";

    /// <summary>
    /// VM854: the job manifest names one channel twice; a channel runs one program of the job (machine-config 8;
    /// virtual machine 2.8, 3.7).
    /// </summary>
    public const string ChannelTwiceInJob = "VM854";
}
