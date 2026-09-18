using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The channel rules of virtual machine 5: SYNC in a single-channel job (VM 3.7). The deadlock at SYNC and two
/// channels on one spindle between marks need the job scheduler of a multi-channel job, which raises them.
/// </summary>
public sealed class ChannelValidationTests
{
    // VM 3.7, 5: SYNC in a single-channel job is a WARNING, no wait.
    [Fact]
    public void Sync_InASingleChannelJob_Warns()
    {
        VmHarness vm = VmHarness.Run(VmHarness.File("SYNC=100", "PROGRAM=END"), VmMachines.Default());

        RuleAssert.Only(vm, DiagnosticCodes.SyncInSingleChannelJob);
    }

    // VM 3.7, implementation 16 (P6-02): the run of one channel program of a job with two channels, which the job
    // compiler writes, is no single-channel job, so its SYNC does not warn.
    [Fact]
    public void Sync_InTheRunOfAChannelOfATwoChannelJob_DoesNotWarn()
    {
        var options = VmOptions.ForMachine(VmMachines.Default()) with { JobChannels = 2 };

        VmHarness.Run(VmHarness.File("SYNC=100", "PROGRAM=END"), VmMachines.Default(), options)
            .AssertNoDiagnostics();
    }

    // VM 3.7: a file whose programs run on two channels is a job for the scheduler, and its SYNC marks pair there.
    [Fact]
    public void Sync_InAFileOfTwoChannels_IsLeftToTheJobScheduler()
    {
        string text = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="ONE" CHANNEL=1
            SYNC=100
            PROGRAM=END
            PROGRAM=BEGIN NAME="TWO" CHANNEL=2
            SYNC=100
            PROGRAM=END
            FILE=END
            """;

        VmHarness.Run(text, VmMachines.Default()).AssertNoDiagnostics();
    }
}
