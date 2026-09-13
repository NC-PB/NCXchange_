using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.Expressions;

// The seventeen functions of language 4.12 and the power: the part of the evaluator that leaves plain decimal
// arithmetic. SIN to ATAN2, SQRT, LN, EXP and a fractional power are computed in double and come back as decimal.
internal sealed partial class Evaluator
{
    // A quarter, a half, three quarters of a turn and the full turn, in degrees: SIN, COS and TAN are exact there.
    private const decimal QuarterTurn = 90;
    private const decimal HalfTurn = 180;
    private const decimal ThreeQuarterTurn = 270;
    private const decimal FullTurn = 360;

    // function "(" expr { "," expr } ")": the arguments are evaluated left to right, each is a number, and the
    // function computes its value from them (language 4.12).
    private ExprResult? EvaluateCall(CallNode call)
    {
        if (!ArgumentCountFits(call))
        {
            return null;
        }

        var arguments = new List<decimal>();
        bool unknown = false;
        foreach (ExprNode argument in call.Arguments)
        {
            ExprResult? value = EvaluateNode(argument);
            if (value is null || !RequireNumber(value, call))
            {
                return null;
            }

            if (value.Number is decimal number)
            {
                arguments.Add(number);
            }
            else
            {
                unknown = true;
            }
        }

        // A function of UNKNOWN is UNKNOWN (virtual machine 1).
        if (unknown)
        {
            return ExprResult.Unknown;
        }

        decimal first = arguments[0];
        return call.Function switch
        {
            ExprFunction.Sin => Sine(call, first),
            ExprFunction.Cos => Cosine(call, first),
            ExprFunction.Tan => Tangent(call, first),
            ExprFunction.Asin => ArcSine(call, first),
            ExprFunction.Acos => ArcCosine(call, first),
            ExprFunction.Atan => FromDouble(call, Degrees(Math.Atan((double)first))),
            ExprFunction.Atan2 => ArcTangent2(call, first, arguments[1]),
            ExprFunction.Sqrt => SquareRoot(call, first),
            ExprFunction.Abs => ExprResult.Of(Math.Abs(first)),

            // INT truncates toward zero (language 4.12): INT(-2.7) is -2.
            ExprFunction.Int => ExprResult.Of(decimal.Truncate(first)),

            // FRAC is the part after the decimal point (language 4.12).
            // TODO(question): language 4.12 does not define FRAC of a negative number; FRAC(-2.7) is -0.7, with the
            // sign of the number, so that INT(x) + FRAC(x) is x, until that is settled.
            ExprFunction.Frac => ExprResult.Of(first - decimal.Truncate(first)),

            // ROUND rounds half away from zero (language 4.12): ROUND(2.5) is 3, ROUND(-2.5) is -3.
            ExprFunction.Round => ExprResult.Of(decimal.Round(first, MidpointRounding.AwayFromZero)),

            ExprFunction.Sgn => ExprResult.Of(Math.Sign(first)),
            ExprFunction.Ln => Logarithm(call, first),
            ExprFunction.Exp => FromDouble(call, Math.Exp((double)first)),
            ExprFunction.Min => ExprResult.Of(arguments.Min()),
            ExprFunction.Max => ExprResult.Of(arguments.Max()),
            _ => throw new ArgumentOutOfRangeException(
                nameof(call), call.Function, "Not a function of language 4.12."),
        };
    }

    // ATAN2 takes two arguments, MIN and MAX one or more, every other function one (language 4.12, function). False
    // after the ERROR.
    // TODO(question): language 4.12 gives no number of arguments per function, and the parser accepts the grammar's
    // one or more (P0-05); these are the counts the mathematics of each function needs, ROUND without a number of
    // decimals, until that is settled.
    private bool ArgumentCountFits(CallNode call)
    {
        if (call.Function is ExprFunction.Min or ExprFunction.Max)
        {
            return true;
        }

        int expected = call.Function == ExprFunction.Atan2 ? 2 : 1;
        if (call.Arguments.Count == expected)
        {
            return true;
        }

        string name = ExprSymbols.Of(call.Function);
        string takes = expected == 1 ? "one argument" : "two arguments";
        Report(DiagnosticCodes.FunctionArgumentCount,
            $"{call} gives {name} {call.Arguments.Count.ToString(CultureInfo.InvariantCulture)} arguments, and {name} "
            + $"takes {takes} (language 4.12).");
        return false;
    }

    // SIN of an angle in degrees (language 4.12); on the axes the value is exact, SIN(180) is 0 and not 1.2E-16.
    private ExprResult? Sine(CallNode call, decimal degrees)
    {
        decimal angle = FullCircle(degrees);
        if (angle == 0 || angle == HalfTurn)
        {
            return ExprResult.Of(0m);
        }

        if (angle == QuarterTurn)
        {
            return ExprResult.Of(1m);
        }

        if (angle == ThreeQuarterTurn)
        {
            return ExprResult.Of(-1m);
        }

        return FromDouble(call, Math.Sin(Radians(angle)));
    }

    // COS of an angle in degrees (language 4.12); on the axes the value is exact, COS(90) is 0 and not 6.1E-17.
    private ExprResult? Cosine(CallNode call, decimal degrees)
    {
        decimal angle = FullCircle(degrees);
        if (angle == QuarterTurn || angle == ThreeQuarterTurn)
        {
            return ExprResult.Of(0m);
        }

        if (angle == 0)
        {
            return ExprResult.Of(1m);
        }

        if (angle == HalfTurn)
        {
            return ExprResult.Of(-1m);
        }

        return FromDouble(call, Math.Cos(Radians(angle)));
    }

    // TAN of an angle in degrees (language 4.12): exactly 0 at 0 and 180 degrees, and no value at 90 and 270, where
    // the cosine is 0.
    private ExprResult? Tangent(CallNode call, decimal degrees)
    {
        decimal angle = FullCircle(degrees);
        if (angle == 0 || angle == HalfTurn)
        {
            return ExprResult.Of(0m);
        }

        if (angle == QuarterTurn || angle == ThreeQuarterTurn)
        {
            ReportUndefined(call, "the tangent of 90 and 270 degrees is not defined");
            return null;
        }

        return FromDouble(call, Math.Tan(Radians(angle)));
    }

    // ASIN of a sine from -1 to 1: an angle from -90 to 90 degrees (language 4.12).
    private ExprResult? ArcSine(CallNode call, decimal sine)
    {
        if (sine < -1 || sine > 1)
        {
            ReportUndefined(call, "a sine lies between -1 and 1");
            return null;
        }

        return FromDouble(call, Degrees(Math.Asin((double)sine)));
    }

    // ACOS of a cosine from -1 to 1: an angle from 0 to 180 degrees (language 4.12).
    private ExprResult? ArcCosine(CallNode call, decimal cosine)
    {
        if (cosine < -1 || cosine > 1)
        {
            ReportUndefined(call, "a cosine lies between -1 and 1");
            return null;
        }

        return FromDouble(call, Degrees(Math.Acos((double)cosine)));
    }

    // ATAN2(y, x): the angle of the point x, y in degrees (language 4.12).
    // TODO(question): language 4.12 names ATAN2 without the order of its arguments, the range of its value or its
    // value at 0, 0; it takes (y, x) and gives -180 to 180, as Siemens ATAN2 and the C library do, and ATAN2(0, 0) is
    // an ERROR, until that is settled.
    private ExprResult? ArcTangent2(CallNode call, decimal y, decimal x)
    {
        if (y == 0 && x == 0)
        {
            ReportUndefined(call, "the point 0, 0 has no angle");
            return null;
        }

        return FromDouble(call, Degrees(Math.Atan2((double)y, (double)x)));
    }

    // SQRT of a number of 0 or more (language 4.12).
    private ExprResult? SquareRoot(CallNode call, decimal number)
    {
        if (number < 0)
        {
            ReportUndefined(call, "a negative number has no square root");
            return null;
        }

        return FromDouble(call, Math.Sqrt((double)number));
    }

    // LN, the natural logarithm of a number greater than 0 (language 4.12).
    private ExprResult? Logarithm(CallNode call, decimal number)
    {
        if (number <= 0)
        {
            ReportUndefined(call, "only a number greater than 0 has a logarithm");
            return null;
        }

        return FromDouble(call, Math.Log((double)number));
    }

    // power = primary [ "^" unary ] (language 4.12). A whole-number exponent is the repeated product in decimal, so
    // $A ^ 2 is $A * $A to the last digit, and a negative one its reciprocal, so 0 to a negative power divides by
    // zero. A fractional exponent is computed in double, and a negative number has no real power with one.
    private ExprResult? Power(BinaryNode binary, decimal number, decimal exponent)
    {
        if (exponent != decimal.Truncate(exponent))
        {
            if (number < 0)
            {
                ReportUndefined(binary, "a negative number has no real power with a fractional exponent");
                return null;
            }

            return FromDouble(binary, Math.Pow((double)number, (double)exponent));
        }

        if (number == 0 && exponent < 0)
        {
            Report(DiagnosticCodes.DivisionByZero,
                $"{binary} divides by zero: 0 to a negative power is 1 / 0 (language 4.12).");
            return null;
        }

        try
        {
            decimal power = WholePower(number, Math.Abs(exponent));
            if (exponent >= 0)
            {
                return ExprResult.Of(power);
            }

            // The reciprocal of a power too small for decimal, which shows as 0, is too large for it.
            if (power == 0)
            {
                ReportOutOfRange(binary);
                return null;
            }

            return ExprResult.Of(1 / power);
        }
        catch (OverflowException)
        {
            ReportOutOfRange(binary);
            return null;
        }
    }

    // number ^ exponent for a whole exponent of 0 or more, by squaring: the number, its square, the square of that and
    // so on, each multiplied into the power where the exponent has a 1 in binary, so that 1 ^ 1000000000 takes thirty
    // steps and not a billion. An exponent of 0 gives 1, the empty product.
    private static decimal WholePower(decimal number, decimal exponent)
    {
        decimal power = 1;
        decimal square = number;
        decimal remaining = exponent;
        while (remaining > 0)
        {
            if (remaining % 2 == 1)
            {
                power *= square;
            }

            remaining = decimal.Truncate(remaining / 2);
            if (remaining > 0)
            {
                square *= square;
            }
        }

        return power;
    }

    // The angle on the circle, from 0 up to 360 degrees, reduced in decimal so that the axes stay exact whatever the
    // number of turns.
    private static decimal FullCircle(decimal degrees)
    {
        decimal angle = degrees % FullTurn;
        return angle < 0 ? angle + FullTurn : angle;
    }

    // Angles in degrees (language 4.12); double computes in radians.
    private static double Radians(decimal degrees)
    {
        return (double)degrees * Math.PI / 180;
    }

    private static double Degrees(double radians)
    {
        return radians * 180 / Math.PI;
    }

    // A value computed in double comes back as decimal to 15 significant digits, the digits a double carries (the
    // conversion of .NET rounds to them), which is also what makes SIN(30) 0.5 and not 0.49999999999999994. Infinity
    // and a value beyond the range of decimal are the ERROR (language 4.12).
    private ExprResult? FromDouble(ExprNode node, double value)
    {
        try
        {
            return ExprResult.Of((decimal)value);
        }
        catch (OverflowException)
        {
            ReportOutOfRange(node);
            return null;
        }
    }

    // A function or a power without a real value is an ERROR, never a number that is not one (language 4.12).
    private void ReportUndefined(ExprNode node, string reason)
    {
        Report(DiagnosticCodes.ResultUndefined, $"{node} has no value: {reason} (language 4.12).");
    }
}
