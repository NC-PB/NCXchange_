using Ncx.Compilers.Tests.Fakes;
using Ncx.Core.Machine;

namespace Ncx.Compilers.Tests;

/// <summary>
/// The compilers by controller family, chosen by the controller of the machine file (code-guidelines 5, Strategy and
/// Registry; architecture 8).
/// </summary>
public sealed class CompilerRegistryTests
{
    // Code-guidelines 5: the registered line creates a new compiler for every compile.
    [Fact]
    public void Create_RegisteredFamily_GivesANewCompilerEachTime()
    {
        var compilers = new CompilerRegistry();
        compilers.Register(Controller.Heidenhain, () => new FakeCompiler(Controller.Heidenhain));

        ICompiler? first = compilers.Create(Controller.Heidenhain);
        ICompiler? second = compilers.Create(Controller.Heidenhain);

        Assert.NotNull(first);
        Assert.Equal(Controller.Heidenhain, first.Controller);
        Assert.NotSame(first, second);
    }

    // Architecture 8: a family without a compiler has none, which the caller reports.
    [Fact]
    public void Create_FamilyWithoutCompiler_IsNull()
    {
        Assert.Null(new CompilerRegistry().Create(Controller.Siemens));
    }

    // Code-guidelines 6: a second compiler for one family is a programmer error.
    [Fact]
    public void Register_SecondCompilerOfAFamily_Throws()
    {
        var compilers = new CompilerRegistry();
        compilers.Register(Controller.Fanuc, () => new FakeCompiler(Controller.Fanuc));

        Assert.Throws<InvalidOperationException>(
            () => compilers.Register(Controller.Fanuc, () => new FakeCompiler(Controller.Fanuc)));
    }
}
