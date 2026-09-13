using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Handlers;

/// <summary>
/// Coolant and the named machine functions of language 4.6: the rows of virtual machine 2.5. MFUNC sets no state; it
/// is an event the compiler passes through (virtual machine 2.5, 7).
/// </summary>
internal static class FunctionHandlers
{
    /// <summary>
    /// Registers the coolant and function words.
    /// </summary>
    public static void Register(Dictionary<string, Action<Word, BlockContext>> handlers)
    {
        handlers["COOLANT"] = ApplyCoolant;
        handlers["FUNC"] = ApplyFunction;
    }

    // COOLANT=ON or OFF on the channel of its address, STANDARD without one, the default channel (language 4.6,
    // virtual machine 2.5, F29).
    private static void ApplyCoolant(Word word, BlockContext context)
    {
        if (context.ResourceOf.TryGetValue(word, out string? channel))
        {
            context.State.Coolant[channel] = BlockContext.IdentOf(word) == "ON";
        }
    }

    // FUNC:name=state: the named machine function in one of the states the configuration names for it (language 4.6,
    // virtual machine 2.5, machine-config 5).
    private static void ApplyFunction(Word word, BlockContext context)
    {
        if (!context.ResourceOf.TryGetValue(word, out string? function) || BlockContext.IdentOf(word) is not string value)
        {
            return;
        }

        if (context.Resources.AcceptsFunctionState(function, value, context.Block, context.Diagnostics))
        {
            context.State.Functions[function] = value;
        }
    }
}
