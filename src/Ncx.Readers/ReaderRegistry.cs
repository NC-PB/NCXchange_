using Ncx.Core.Machine;

namespace Ncx.Readers;

/// <summary>
/// The readers by controller family, each registered at start-up with a line that creates it, so that the controller
/// of the machine file chooses the reader (code-guidelines 5, Strategy and Registry; architecture 7).
/// </summary>
public sealed class ReaderRegistry
{
    private readonly Dictionary<Controller, Func<IReader>> _factories = [];

    /// <summary>
    /// Registers the reader of a controller family. A family has one reader; registering a second one is a
    /// programmer error.
    /// </summary>
    /// <param name="controller">The controller family, the controller of the machine file.</param>
    /// <param name="factory">Creates a reader for one file.</param>
    public void Register(Controller controller, Func<IReader> factory)
    {
        if (!_factories.TryAdd(controller, factory))
        {
            throw new InvalidOperationException($"A reader for {controller} is registered already.");
        }
    }

    /// <summary>
    /// Creates the reader of a controller family.
    /// </summary>
    /// <param name="controller">The controller of the machine file (machine-config 1).</param>
    /// <returns>A new reader; null when no reader is registered for the family, which the caller reports.</returns>
    public IReader? Create(Controller controller)
    {
        return _factories.TryGetValue(controller, out Func<IReader>? factory) ? factory() : null;
    }
}
