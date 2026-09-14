using Ncx.Core.Machine;

namespace Ncx.Compilers;

/// <summary>
/// The compilers by controller family, each registered at start-up with a line that creates it, so that the controller
/// of the machine file chooses the compiler (code-guidelines 5, Strategy and Registry; architecture 8).
/// </summary>
public sealed class CompilerRegistry
{
    private readonly Dictionary<Controller, Func<ICompiler>> _factories = [];

    /// <summary>
    /// Registers the compiler of a controller family. A family has one compiler; registering a second one is a
    /// programmer error.
    /// </summary>
    /// <param name="controller">The controller family, the controller of the machine file.</param>
    /// <param name="factory">Creates a compiler for one compile.</param>
    public void Register(Controller controller, Func<ICompiler> factory)
    {
        if (!_factories.TryAdd(controller, factory))
        {
            throw new InvalidOperationException($"A compiler for {controller} is registered already.");
        }
    }

    /// <summary>
    /// Creates the compiler of a controller family.
    /// </summary>
    /// <param name="controller">The controller of the machine file (machine-config 1).</param>
    /// <returns>A new compiler; null when no compiler is registered for the family, which the caller reports.</returns>
    public ICompiler? Create(Controller controller)
    {
        return _factories.TryGetValue(controller, out Func<ICompiler>? factory) ? factory() : null;
    }
}
