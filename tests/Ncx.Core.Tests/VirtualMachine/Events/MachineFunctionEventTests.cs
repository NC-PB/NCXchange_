using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Tests.VirtualMachine.Events;

/// <summary>
/// The events F18 added to virtual machine 7: DWELL, STOP and FUNCTION for FUNC, MFUNC and COOLANT (language 4.1, 4.6;
/// virtual machine 2.5, 8).
/// </summary>
public sealed class MachineFunctionEventTests
{
    // VM 7 as amended by F18, row DWELL: the seconds of DWELL, which the runtime estimate charges (VM 8).
    [Fact]
    public void Dwell_DwellWord_CarriesTheSeconds()
    {
        FakeListener listener = EventRuns.Execute("DWELL=1.5");

        DwellEvent dwell = Assert.Single(listener.Of<DwellEvent>());
        Assert.Equal(1.5m, dwell.Seconds);
        Assert.Equal("DWELL(1): seconds 1.5", dwell.ToString());
    }

    // VM 7 as amended by F18, row STOP: the program stop and the optional stop.
    [Fact]
    public void Stop_StopWords_TellTheProgramStopFromTheOptionalStop()
    {
        FakeListener listener = EventRuns.Execute("STOP=PROGRAM", "STOP=OPTIONAL");

        Assert.Equal(["STOP(1): PROGRAM", "STOP(2): OPTIONAL"], listener.LinesOf("STOP"));
        Assert.True(listener.Of<StopEvent>()[1].Optional);
    }

    // VM 7 as amended by F18, row FUNCTION; VM 2.5, F29: a bare COOLANT addresses the default channel STANDARD.
    [Fact]
    public void Function_BareCoolant_NamesTheDefaultChannel()
    {
        FakeListener listener = EventRuns.Execute("COOLANT=ON");

        FunctionEvent coolant = Assert.Single(listener.Of<FunctionEvent>());
        Assert.Equal("STANDARD", coolant.Name);
        Assert.Equal(
            ["FUNCTION(1): COOLANT=ON on STANDARD", "STATE_CHANGE(1): COOLANT:STANDARD OFF -> ON"],
            listener.Lines);
    }

    // VM 2.5: MFUNC sets no state; the event is what the compiler passes through.
    [Fact]
    public void Function_Mfunc_IsAnEventWithoutState()
    {
        FakeListener listener = EventRuns.Execute("MFUNC=136");

        Assert.Equal(["FUNCTION(1): MFUNC=136"], listener.Lines);
        Assert.Null(listener.Of<FunctionEvent>()[0].Name);
    }

    // VM 2.5, 7: FUNC with the function it addresses, and the new state as a STATE_CHANGE.
    [Fact]
    public void Function_Func_NamesTheFunctionAndChangesItsState()
    {
        FakeListener listener = EventRuns.Execute(VmMachines.MillTurn(), null, "FUNC:SUB_CHUCK=OPEN");

        Assert.Equal(
            ["FUNCTION(1): FUNC:SUB_CHUCK=OPEN on SUB_CHUCK", "STATE_CHANGE(1): FUNC:SUB_CHUCK ? -> OPEN"],
            listener.Lines);
    }
}
