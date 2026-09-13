namespace Ncx.Core.Model;

// The codes of the expression evaluator, VM900-VM949 (the ranges in DiagnosticCodes.cs, D98): one ERROR for each way
// an expression that parsed can fail when INTERPRETED mode evaluates it (language 4.12, virtual machine 3.6, 5). Every
// one stops the run on the line of the block the expression stands in.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// VM900: a division by zero, the divisor of / or of MOD, or 0 to a negative power (language 4.12).
    /// </summary>
    public const string DivisionByZero = "VM900";

    /// <summary>
    /// VM901: a variable is read before anything assigned it, and the configuration does not set unassigned = 0
    /// (virtual machine 3.6, D38).
    /// </summary>
    public const string UnassignedVariableRead = "VM901";

    /// <summary>
    /// VM902: a string where a number is required, the string a variable holds as an operand or a function argument
    /// (virtual machine 5).
    /// </summary>
    public const string StringWhereNumberIsRequired = "VM902";

    /// <summary>
    /// VM903: a SYS_ name the configuration does not map, or whose state is unknown, read in INTERPRETED mode (virtual
    /// machine 2.7, 3.6, D51).
    /// </summary>
    public const string SystemVariableUnknown = "VM903";

    /// <summary>
    /// VM904: a function called with a number of arguments it does not take, SIN(1, 2), ATAN2(1) (language 4.12).
    /// </summary>
    public const string FunctionArgumentCount = "VM904";

    /// <summary>
    /// VM905: an operation without a real result, SQRT(-1), LN(0), ASIN(2), TAN(90), ATAN2(0, 0), a negative number to
    /// a fractional power (language 4.12).
    /// </summary>
    public const string ResultUndefined = "VM905";

    /// <summary>
    /// VM906: a number or a result beyond the range of the decimal arithmetic the evaluator computes with, about
    /// 7.9E28 either way (language 4.12).
    /// </summary>
    public const string ResultOutOfRange = "VM906";

    /// <summary>
    /// VM907: an index on a variable that is not a SYS_ name, $Q1[2]; the index selects a register or table row of a
    /// system variable (language 4.12, D51).
    /// </summary>
    public const string IndexNeedsSystemVariable = "VM907";

    /// <summary>
    /// VM908: an index that selects no register or table row, because it is not a whole number (language 4.12, D51).
    /// </summary>
    public const string IndexSelectsNoRegister = "VM908";
}
