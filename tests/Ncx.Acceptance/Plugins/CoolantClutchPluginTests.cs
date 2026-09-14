using Ncx.Acceptance.Cli;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;
using Ncx.Plugins;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// The first "Done when" of P7-01: the CoolantClutchRule of code-guidelines 11, loaded as a plugin, produces the same
/// generated blocks as the configuration rule of phase 1 (machine-config 5a, P1-06) and the same state afterwards
/// (virtual machine 3.10), and says what it inserted (D98).
/// </summary>
public sealed class CoolantClutchPluginTests : IDisposable
{
    private static readonly string s_program = CliHarness.OneProgram(
        "UNITS=MM",
        "SPINDLE:MAIN=CW RPM:MAIN=1500",
        "COOLANT:THROUGH=ON");

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("ncx-plugins-");
    private readonly Diagnostics _diagnostics = new("part.ncx");

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    // Code-guidelines 11, machine-config 5a: @SAVE=SPINDLE:MAIN, SPINDLE:MAIN=OFF, the coolant block,
    // @RESTORE=SPINDLE:MAIN, whether the machine file or the plugin asks for them; the blocks of the plugin name it
    // with its reason (architecture 9, virtual machine 3.10).
    [Fact]
    public void CoolantClutchRule_AsAPlugin_GivesTheGeneratedBlocksOfTheConfigurationRule()
    {
        PluginSet plugins = TestPlugins.Load(_folder.FullName, _diagnostics, TestPlugins.ShopRules);

        NcxProgram byPlugin = PluginMachines.Expand(s_program, PluginMachines.PlainMill, plugins.Rewriters);
        NcxProgram byRule = PluginMachines.Expand(s_program, PluginMachines.ClutchMill, []);

        Assert.Equal(
            CliHarness.OneProgram(
                "UNITS=MM",
                "SPINDLE:MAIN=CW RPM:MAIN=1500",
                "@SAVE=SPINDLE:MAIN",
                "SPINDLE:MAIN=OFF",
                "COOLANT:THROUGH=ON",
                "@RESTORE=SPINDLE:MAIN"),
            PluginMachines.WriteWithGenerated(byRule));
        Assert.Equal(PluginMachines.WriteWithGenerated(byRule), PluginMachines.WriteWithGenerated(byPlugin));
        Block stop = PluginMachines.Find(byPlugin, "SPINDLE:MAIN=OFF");
        Assert.Equal(TestPlugins.ShopRules, stop.Generated?.Source);
        Assert.Equal("the coolant clutch needs a standing spindle", stop.Generated?.Reason);
    }

    // Virtual machine 3.10: the VM turns the restore into SPINDLE:MAIN=CW RPM:MAIN=1500 from its own snapshot, so both
    // runs change the same variables on the same lines and leave the spindle running with the coolant on.
    [Fact]
    public void CoolantClutchRule_AsAPlugin_LeavesTheStateOfTheConfigurationRule()
    {
        PluginSet plugins = TestPlugins.Load(_folder.FullName, _diagnostics, TestPlugins.ShopRules);

        StateRecorder byPlugin = Run(
            PluginMachines.Expand(s_program, PluginMachines.PlainMill, plugins.Rewriters), PluginMachines.PlainMill);
        StateRecorder byRule = Run(
            PluginMachines.Expand(s_program, PluginMachines.ClutchMill, []), PluginMachines.ClutchMill);

        Assert.NotEmpty(byRule.Changes);
        Assert.Equal(byRule.Changes, byPlugin.Changes);
        ChannelSnapshot pluginEnd = byPlugin.BeforeProgramEnd ?? throw new InvalidOperationException("No end.");
        ChannelSnapshot ruleEnd = byRule.BeforeProgramEnd ?? throw new InvalidOperationException("No end.");
        Assert.Equal(ruleEnd.Spindles["S1"], pluginEnd.Spindles["S1"]);
        Assert.Equal(SpindleDirection.Clockwise, pluginEnd.Spindles["S1"].Direction);
        Assert.Equal(1500m, pluginEnd.Spindles["S1"].Rpm);
        Assert.True(pluginEnd.Coolant["THROUGH"]);
        Assert.Empty(pluginEnd.Flow.RestoreStack);
        Assert.Empty(ruleEnd.Flow.RestoreStack);
    }

    // D98, code-guidelines 11: the INFO line names the plugin, the blocks it inserted and the line.
    // TODO(question): see PluginRewriter; the clutch rule inserts three blocks, where code-guidelines 11 and P7-02
    // print "inserted 2 blocks".
    [Fact]
    public void CoolantClutchRule_AsAPlugin_SaysWhatItInsertedAsInfo()
    {
        PluginSet plugins = TestPlugins.Load(_folder.FullName, _diagnostics, TestPlugins.ShopRules);

        PluginMachines.Expand(s_program, PluginMachines.PlainMill, plugins.Rewriters);

        Assert.Equal("part.ncx(5): INFO PLG001: plugin ShopRules: inserted 3 blocks at line 5\n",
            _diagnostics.ToText());
    }

    // A STATIC run of a program with the recorder subscribed; the run reports no diagnostic.
    private static StateRecorder Run(NcxProgram program, string machineToml)
    {
        MachineConfig machine = PluginMachines.Load(machineToml);
        var recorder = new StateRecorder();
        var vm = new VirtualMachine(machine, VmOptions.ForMachine(machine), program.Diagnostics);
        vm.Subscribe(recorder);
        vm.Run(program);
        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
        return recorder;
    }

    // Every STATE_CHANGE with its line, variable, old and new value, and the state before PROGRAM=END, which resets
    // the spindle and the coolant (virtual machine 4).
    private sealed class StateRecorder : IVmListener
    {
        public List<string> Changes { get; } = [];

        public ChannelSnapshot? BeforeProgramEnd { get; private set; }

        public void On(VmEvent vmEvent)
        {
            if (vmEvent is StateChangeEvent)
            {
                Changes.Add(vmEvent.ToString());
            }
            else if (vmEvent is ProgramEvent { Phase: EventPhase.End })
            {
                BeforeProgramEnd = vmEvent.Before;
            }
        }
    }
}
