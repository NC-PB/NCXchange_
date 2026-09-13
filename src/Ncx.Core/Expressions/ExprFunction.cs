namespace Ncx.Core.Expressions;

// The members are the function names of language 4.12 (code-guidelines 3.1: the specification names the thing);
// CA1720 reads INT as a .NET type name.
#pragma warning disable CA1720

/// <summary>
/// The seventeen functions of an expression (language 4.12, function). Angles are in degrees, INT truncates toward
/// zero and ROUND rounds half away from zero; the evaluator computes them (P4-01), the parser only names them.
/// </summary>
public enum ExprFunction
{
    /// <summary>
    /// SIN, the sine of an angle in degrees.
    /// </summary>
    Sin,

    /// <summary>
    /// COS, the cosine of an angle in degrees.
    /// </summary>
    Cos,

    /// <summary>
    /// TAN, the tangent of an angle in degrees.
    /// </summary>
    Tan,

    /// <summary>
    /// ASIN, the arc sine, an angle in degrees.
    /// </summary>
    Asin,

    /// <summary>
    /// ACOS, the arc cosine, an angle in degrees.
    /// </summary>
    Acos,

    /// <summary>
    /// ATAN, the arc tangent, an angle in degrees.
    /// </summary>
    Atan,

    /// <summary>
    /// ATAN2, the arc tangent of two arguments, an angle in degrees.
    /// </summary>
    Atan2,

    /// <summary>
    /// SQRT, the square root.
    /// </summary>
    Sqrt,

    /// <summary>
    /// ABS, the absolute value.
    /// </summary>
    Abs,

    /// <summary>
    /// INT, truncates toward zero (language 4.12).
    /// </summary>
    Int,

    /// <summary>
    /// FRAC, the part after the decimal point.
    /// </summary>
    Frac,

    /// <summary>
    /// ROUND, rounds half away from zero (language 4.12).
    /// </summary>
    Round,

    /// <summary>
    /// SGN, the sign.
    /// </summary>
    Sgn,

    /// <summary>
    /// LN, the natural logarithm.
    /// </summary>
    Ln,

    /// <summary>
    /// EXP, the exponential function.
    /// </summary>
    Exp,

    /// <summary>
    /// MIN, the smallest of the arguments.
    /// </summary>
    Min,

    /// <summary>
    /// MAX, the largest of the arguments.
    /// </summary>
    Max,
}

#pragma warning restore CA1720
