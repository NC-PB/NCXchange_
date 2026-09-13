using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Handlers;

/// <summary>
/// The variable word VAR of language 4.9: the vars of virtual machine 2.7. ARG belongs to the CALL of its block and is
/// assigned when the walk enters the subprogram; the flow words are step 6.
/// </summary>
internal static class VariableHandlers
{
    /// <summary>
    /// Registers the variable word.
    /// </summary>
    public static void Register(Dictionary<string, Action<Word, BlockContext>> handlers)
    {
        handlers["VAR"] = ApplyVar;
    }

    // VAR:name=value assigns a variable and creates it if needed (language 4.9); a SYS_ variable is never assigned by
    // the program, an ERROR (virtual machine 2.7, 5).
    private static void ApplyVar(Word word, BlockContext context)
    {
        if (word.Addr is not string name)
        {
            return;
        }

        if (VariableStore.IsSystem(name))
        {
            context.Diagnostics.Error(context.Block, DiagnosticCodes.SystemVariableAssigned,
                $"VAR:{name} assigns a system variable, which the program never assigns (virtual machine 2.7).");
            return;
        }

        context.State.Vars.Set(name, ValueOf(word.Value));
    }

    /// <summary>
    /// The value a variable takes from VAR or ARG: a number or a string as written; UNKNOWN from an expression, which
    /// STATIC mode does not evaluate (virtual machine 1). INTERPRETED mode resolves the expression before the block
    /// executes, so the value arrives as the number or string it gave (ExpressionResolver, virtual machine 3.6).
    /// </summary>
    public static VariableValue ValueOf(Value value)
    {
        return value is IntegerValue or DecimalValue or StringValue ? VariableValue.Of(value) : VariableValue.Unknown;
    }
}
