namespace Ncx.Core.Model;

// The codes of INTERPRETED mode and its flow, VM750-VM849 (the ranges in DiagnosticCodes.cs, D98): the block cap, the
// nesting of repeats, the external programs a CALL loads, the program the run is asked for, and a value an expression
// gives that its word does not take (virtual machine 3.6, 5). The call depth, the call of a program and a missing call
// target keep the codes of the STATIC walk (VM070 to VM072), and the evaluation of an expression those of the evaluator
// (VM900 to VM908).
public static partial class DiagnosticCodes
{
    /// <summary>
    /// VM750: the block cap of INTERPRETED mode is reached, "possible endless loop" (virtual machine 3.6, 5;
    /// machine-config 7, block_cap).
    /// </summary>
    public const string BlockCapExceeded = "VM750";

    /// <summary>
    /// VM751: a REPEAT that would nest deeper than the configured depth, which calls and repeats share (virtual machine
    /// 3.6, 5; machine-config 7, call_depth).
    /// </summary>
    public const string RepeatDepthExceeded = "VM751";

    /// <summary>
    /// VM752: a CALL of an external program that is not found in the working directory (virtual machine 3.6, 5,
    /// missing call target).
    /// </summary>
    public const string ExternalProgramNotFound = "VM752";

    /// <summary>
    /// VM753: an external program whose UNITS or WORKPLANE contradicts the caller's (virtual machine 3.6).
    /// </summary>
    public const string ExternalProgramContradictsCaller = "VM753";

    /// <summary>
    /// VM754: an expression gives a number its word does not take, a number with decimals where the word takes an
    /// integer: TIMES={2.5} (language 3, 4.9; virtual machine 5, unknown value).
    /// </summary>
    public const string ValueNotAnInteger = "VM754";

    /// <summary>
    /// VM755: the program the command line or the job names is not a program of the file (language 4.13, virtual
    /// machine 3.6).
    /// </summary>
    public const string ProgramToRunMissing = "VM755";
}
