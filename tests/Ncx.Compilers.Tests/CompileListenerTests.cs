using Ncx.Compilers.Tests.Fakes;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Compilers.Tests;

/// <summary>
/// The listeners of the options, the plugins' ones, read every event of the STATIC run of a compile, as a listener
/// reads every event of any run of the virtual machine (virtual machine 7; architecture 8, 9; code-guidelines 5,
/// Observer).
/// </summary>
public sealed class CompileListenerTests
{
    // Virtual machine 7: FILE_BEGIN first and FILE_END last, between them the events of every block with its line, the
    // MOTION of the RAPID on line 4 among them; the compile writes the same file as without the listener.
    [Fact]
    public void IVmListener_ListenerOfTheOptions_ReceivesEveryEventOfTheStaticRun()
    {
        var listener = new RecordingListener();
        string program = FakeCompile.Program("RAPID X=0 Y=0 Z=5");

        CompileResult result = FakeCompile.Run(program, FakeMachines.Mill(),
            options: new CompileOptions { Listeners = [listener] });

        Assert.Equal(FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill())), FakeCompile.TextOf(result));
        Assert.Equal("FILE_BEGIN", listener.Events[0].Kind);
        Assert.Equal("FILE_END", listener.Events[^1].Kind);
        Assert.Contains(listener.Events, vmEvent => vmEvent is MotionEvent && vmEvent.Block.Line == 4);
    }

    // A listener that records every event it receives.
    private sealed class RecordingListener : IVmListener
    {
        public List<VmEvent> Events { get; } = [];

        public void On(VmEvent vmEvent)
        {
            Events.Add(vmEvent);
        }
    }
}
