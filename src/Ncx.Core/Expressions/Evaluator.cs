using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Expressions;

/// <summary>
/// Evaluates the tree of an expression (language 4.12) in INTERPRETED mode (virtual machine 3.6): decimal arithmetic,
/// the seventeen functions with angles in degrees, comparisons and AND, OR, NOT yielding 1 or 0, and the variables of
/// the channel read through its variable store. What a program can get wrong, a division by zero, an unassigned
/// variable, a string where a number is required, is one VM ERROR on the line of its block and gives no value
/// (virtual machine 5, code-guidelines 6). One method per node type, and per function in Evaluator.Functions.cs
/// (code-guidelines 5, Interpreter).
/// </summary>
internal sealed partial class Evaluator
{
    private readonly VariableStore _vars;
    private readonly UnassignedVariable _unassigned;
    private readonly Block _block;
    private readonly Diagnostics _diagnostics;

    private Evaluator(VariableStore vars, UnassignedVariable unassigned, Block block, Diagnostics diagnostics)
    {
        _vars = vars;
        _unassigned = unassigned;
        _block = block;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Evaluates an expression of a block (language 4.12). The evaluator stops at the first problem in reading order,
    /// so an expression gives at most one ERROR.
    /// </summary>
    /// <param name="expression">The tree of the expression, as the expression parser built it.</param>
    /// <param name="vars">
    /// The variables of the channel: the locals V1 to V33 of the call that runs, every other variable, the SYS_ names
    /// (virtual machine 2.7).
    /// </param>
    /// <param name="unassigned">
    /// [variables] unassigned of the machine configuration: what reading an unassigned variable does (machine-config 7,
    /// D38).
    /// </param>
    /// <param name="block">The block the expression stands in, whose line the ERROR carries (D98).</param>
    /// <param name="diagnostics">The diagnostics of the run; the ERROR is added to them.</param>
    /// <returns>A number, the string a variable holds, or UNKNOWN; null after the ERROR.</returns>
    public static ExprResult? Evaluate(
        ExprNode expression, VariableStore vars, UnassignedVariable unassigned, Block block, Diagnostics diagnostics)
    {
        var evaluator = new Evaluator(vars, unassigned, block, diagnostics);
        return evaluator.EvaluateNode(expression);
    }

    // Every production of the grammar has its node and every node its value; parentheses group and add nothing to the
    // value (language 4.12).
    private ExprResult? EvaluateNode(ExprNode node)
    {
        return node switch
        {
            NumberNode number => EvaluateNumber(number),
            VariableNode variable => EvaluateVariable(variable),
            CallNode call => EvaluateCall(call),
            UnaryNode unary => EvaluateUnary(unary),
            BinaryNode binary => EvaluateBinary(binary),
            ParenthesesNode parentheses => EvaluateNode(parentheses.Inner),
            _ => throw new ArgumentException($"{node.GetType().Name} is not a node of language 4.12.", nameof(node)),
        };
    }

    // A number is its value as written, an integer or a decimal of language 3; a number beyond the range of decimal is
    // an ERROR (language 3, 4.12).
    private ExprResult? EvaluateNumber(NumberNode number)
    {
        if (!decimal.TryParse(
                number.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
        {
            ReportOutOfRange(number);
            return null;
        }

        return ExprResult.Of(value);
    }

    // variable = "$" addr [ "[" expr "]" ]: a SYS_ name reads the state of the channel, every other name the variable
    // VAR or ARG assigned (language 4.9, 4.12, D51).
    private ExprResult? EvaluateVariable(VariableNode variable)
    {
        if (VariableStore.IsSystem(variable.Name))
        {
            return EvaluateSystemVariable(variable);
        }

        // TODO(question): language 4.12 gives an index a meaning for the SYS_ names only (it "selects a register or
        // table row", D51), while the grammar allows it on every variable; an index on any other variable, $Q1[2], is
        // an ERROR until that is settled.
        if (variable.Index is not null)
        {
            Report(DiagnosticCodes.IndexNeedsSystemVariable,
                $"{variable} has an index, and only a SYS_ name has registers or table rows for it to select "
                + "(language 4.12, D51).");
            return null;
        }

        // $Q1 reads the variable: V1 to V33 the locals of the call that runs, every other name the value it holds for
        // the program's lifetime (language 4.9, virtual machine 3.6, 4).
        VariableValue? value = _vars.Get(variable.Name);
        if (value is null)
        {
            return ReadUnassigned(variable);
        }

        return ResultOf(value);
    }

    // Reading an unassigned variable is an ERROR unless the configuration sets unassigned = 0 (virtual machine 3.6,
    // D38).
    // TODO: the variable store applies [variables] unassigned of the machine it was built with as well and reports an
    // unassigned variable only under "error"; P1-02 makes the two one setting when it wires VmOptions.Unassigned here.
    private ExprResult? ReadUnassigned(VariableNode variable)
    {
        if (_unassigned == UnassignedVariable.Zero)
        {
            return ExprResult.Of(0m);
        }

        Report(DiagnosticCodes.UnassignedVariableRead,
            $"{variable} is read before anything assigned it; reading an unassigned variable is an ERROR unless the "
            + "machine configuration sets unassigned = 0 (virtual machine 3.6, D38).");
        return null;
    }

    // $SYS_* names are read from the state of the channel through the mapping of the configuration, the index
    // selecting a register or table row; a name the configuration does not map, or a state that is unknown, is an
    // ERROR in INTERPRETED mode (language 4.12, virtual machine 2.7, 3.6, D51).
    private ExprResult? EvaluateSystemVariable(VariableNode variable)
    {
        int? register = null;
        if (variable.Index is ExprNode index)
        {
            // The index may itself be an expression (language 4.12).
            ExprResult? indexValue = EvaluateNode(index);
            if (indexValue is null || !RequireNumber(indexValue, variable))
            {
                return null;
            }

            // The register an unknown index selects is unknown (virtual machine 1).
            if (indexValue.Number is not decimal indexNumber)
            {
                return ExprResult.Unknown;
            }

            register = RegisterOf(variable, indexNumber);
            if (register is null)
            {
                return null;
            }
        }

        VariableValue value = _vars.GetSystem(variable.Name, register);
        if (value.IsUnknown)
        {
            // TODO: a register the virtual machine does not hold is read from <file>.vars.toml before it is an ERROR
            // (virtual machine 2.7); that comes with the vars files of P4-01 part two, and the message then names the
            // vars file as the way out.
            Report(DiagnosticCodes.SystemVariableUnknown,
                $"{variable} is not known: the machine configuration does not map {variable.Name} in "
                + "[system_variables], or the state it reads is unknown, and INTERPRETED mode needs its value "
                + "(virtual machine 2.7, 3.6).");
            return null;
        }

        return ResultOf(value);
    }

    // The index selects a register or table row (language 4.12): a whole number; null after the ERROR.
    // TODO(question): language 4.12 does not say what an index that is not a whole number selects; it is an ERROR
    // until that is settled.
    private int? RegisterOf(VariableNode variable, decimal index)
    {
        if (index != decimal.Truncate(index) || index < int.MinValue || index > int.MaxValue)
        {
            Report(DiagnosticCodes.IndexSelectsNoRegister,
                $"The index of {variable} is {index.ToString(CultureInfo.InvariantCulture)}, which selects no "
                + "register or table row: an index is a whole number (language 4.12, D51).");
            return null;
        }

        return (int)index;
    }

    // A variable holds a number or a string as VAR, ARG or the vars file gave it, or UNKNOWN (language 4.9, virtual
    // machine 1, 2.7).
    private static ExprResult ResultOf(VariableValue value)
    {
        return value.Value switch
        {
            null => ExprResult.Unknown,
            IntegerValue integer => ExprResult.Of(integer.Number),
            DecimalValue number => ExprResult.Of(number.Number),
            StringValue text => ExprResult.Of(text.Content),
            _ => throw new ArgumentException($"A variable holds a number or a string, not {value}.", nameof(value)),
        };
    }

    // unary = [ "-" ] power: the minus negates the power behind it; notexpr = [ "NOT" ] cmpexpr: NOT yields 1 for 0
    // and 0 for anything else (language 4.12; implementation 14, P4-01).
    private ExprResult? EvaluateUnary(UnaryNode unary)
    {
        ExprResult? operand = EvaluateNode(unary.Operand);
        if (operand is null || !RequireNumber(operand, unary))
        {
            return null;
        }

        // An operation on UNKNOWN is UNKNOWN (virtual machine 1).
        if (operand.Number is not decimal number)
        {
            return ExprResult.Unknown;
        }

        return unary.Operator switch
        {
            UnaryOperator.Minus => ExprResult.Of(-number),
            UnaryOperator.Not => Truth(number == 0),
            _ => throw new ArgumentOutOfRangeException(
                nameof(unary), unary.Operator, "Not an operator of language 4.12."),
        };
    }

    // The operators with two operands (language 4.12, orexpr to power), the left operand evaluated before the right.
    // TODO(question): language 4.12 does not say whether AND and OR skip their right operand when the left one decides
    // the value; both are evaluated, as for every other operator, so 0 AND 1 / 0 is the division by zero.
    // TODO(question): language 4.12 does not say whether == and != compare strings; every operator takes numbers, and
    // a string is the ERROR of virtual machine 5, until that is settled.
    private ExprResult? EvaluateBinary(BinaryNode binary)
    {
        ExprResult? left = EvaluateNode(binary.Left);
        if (left is null || !RequireNumber(left, binary))
        {
            return null;
        }

        ExprResult? right = EvaluateNode(binary.Right);
        if (right is null || !RequireNumber(right, binary))
        {
            return null;
        }

        // An operation on UNKNOWN is UNKNOWN (virtual machine 1).
        if (left.Number is not decimal leftNumber || right.Number is not decimal rightNumber)
        {
            return ExprResult.Unknown;
        }

        // Comparisons yield 1 or 0 (language 4.12); AND and OR as well, a value that is not 0 counting as true, as IF
        // reads it (language 4.9; implementation 14, P4-01).
        return binary.Operator switch
        {
            BinaryOperator.Or => Truth(leftNumber != 0 || rightNumber != 0),
            BinaryOperator.And => Truth(leftNumber != 0 && rightNumber != 0),
            BinaryOperator.Equal => Truth(leftNumber == rightNumber),
            BinaryOperator.NotEqual => Truth(leftNumber != rightNumber),
            BinaryOperator.Less => Truth(leftNumber < rightNumber),
            BinaryOperator.LessOrEqual => Truth(leftNumber <= rightNumber),
            BinaryOperator.Greater => Truth(leftNumber > rightNumber),
            BinaryOperator.GreaterOrEqual => Truth(leftNumber >= rightNumber),
            BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply
                => Arithmetic(binary, leftNumber, rightNumber),
            BinaryOperator.Divide or BinaryOperator.Mod => Divide(binary, leftNumber, rightNumber),
            BinaryOperator.Power => Power(binary, leftNumber, rightNumber),
            _ => throw new ArgumentOutOfRangeException(
                nameof(binary), binary.Operator, "Not an operator of language 4.12."),
        };
    }

    // sum and product: +, - and * in decimal (language 4.12; implementation 14, P4-01).
    private ExprResult? Arithmetic(BinaryNode binary, decimal left, decimal right)
    {
        try
        {
            decimal value = binary.Operator switch
            {
                BinaryOperator.Add => left + right,
                BinaryOperator.Subtract => left - right,
                BinaryOperator.Multiply => left * right,
                _ => throw new ArgumentOutOfRangeException(nameof(binary), binary.Operator, "Not +, - or *."),
            };
            return ExprResult.Of(value);
        }
        catch (OverflowException)
        {
            ReportOutOfRange(binary);
            return null;
        }
    }

    // Division by zero is an ERROR (language 4.12). MOD is the remainder of the division and keeps the sign of the
    // dividend (language 4.12), as the remainder of decimal does: -7 MOD 3 is -1, 7 MOD -3 is 1.
    private ExprResult? Divide(BinaryNode binary, decimal dividend, decimal divisor)
    {
        if (divisor == 0)
        {
            Report(DiagnosticCodes.DivisionByZero,
                $"The divisor of {binary} is 0; division by zero is an ERROR (language 4.12).");
            return null;
        }

        try
        {
            decimal value = binary.Operator == BinaryOperator.Mod ? dividend % divisor : dividend / divisor;
            return ExprResult.Of(value);
        }
        catch (OverflowException)
        {
            ReportOutOfRange(binary);
            return null;
        }
    }

    // Comparisons yield 1 or 0 (language 4.12).
    private static ExprResult Truth(bool condition)
    {
        return ExprResult.Of(condition ? 1m : 0m);
    }

    // A string where a number is required is an ERROR (virtual machine 5): an expression computes with numbers only,
    // and a string passes through only as the whole value, {$QS1}. False after the ERROR.
    private bool RequireNumber(ExprResult value, ExprNode usedIn)
    {
        if (value.Content is not string content)
        {
            return true;
        }

        Report(DiagnosticCodes.StringWhereNumberIsRequired,
            $"{usedIn} needs a number where the string \"{content}\" stands (virtual machine 5).");
        return false;
    }

    // The evaluator computes in decimal, about 7.9E28 either way; a value beyond it is an ERROR (language 4.12;
    // implementation 14, P4-01).
    private void ReportOutOfRange(ExprNode node)
    {
        Report(DiagnosticCodes.ResultOutOfRange,
            $"The value of {node} is beyond the range of the decimal numbers the evaluator computes with, about "
            + "7.9E28 either way (language 4.12).");
    }

    // Every problem is an ERROR on the block the expression stands in, and on a generated block it also names the line
    // of the block it was generated for (code-guidelines 6, D98).
    private void Report(string code, string message)
    {
        _diagnostics.Error(_block, code, message);
    }
}
