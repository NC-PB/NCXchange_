using Ncx.Core.Machine;
using Ncx.Readers.Tests.Fakes;

namespace Ncx.Readers.Tests;

/// <summary>
/// The controller of the machine file chooses the reader through a registry from name to factory (code-guidelines 5,
/// Strategy and Registry).
/// </summary>
public sealed class ReaderRegistryTests
{
    [Fact]
    public void Create_RegisteredController_CreatesANewReaderEachTime()
    {
        var registry = new ReaderRegistry();
        registry.Register(Controller.Fanuc, () => new FakeReader());

        IReader? first = registry.Create(Controller.Fanuc);
        IReader? second = registry.Create(Controller.Fanuc);

        Assert.IsType<FakeReader>(first);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Create_ControllerWithoutReader_IsNull()
    {
        Assert.Null(new ReaderRegistry().Create(Controller.Siemens));
    }

    // A controller family has one reader; a second registration is a programmer error (code-guidelines 6).
    [Fact]
    public void Register_SecondReaderForAController_Throws()
    {
        var registry = new ReaderRegistry();
        registry.Register(Controller.Fanuc, () => new FakeReader());

        Assert.Throws<InvalidOperationException>(() => registry.Register(Controller.Fanuc, () => new FakeReader()));
    }
}
