using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ncx.Acceptance.Cli;
using Ncx.Compilers;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;
using Ncx.Plugins;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// A listener plugin reads every event with Before and After and cannot change them: the snapshot types are immutable
/// records, which the compiler holds, so a plugin cannot reach into the virtual machine even by accident (virtual
/// machine 7; architecture 9; D61, D106; implementation 17, P7-01).
/// </summary>
public sealed class ListenerPluginTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("ncx-plugins-");

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    // Virtual machine 7: every event carries Before and After; the EventRecorder of ShopRules reads the spindle of
    // both, the start at line 4 and the stop of PROGRAM=END at line 5 (virtual machine 4).
    [Fact]
    public void IVmListener_ListenerOfAPlugin_ReadsBeforeAndAfterOfEveryEvent()
    {
        var diagnostics = new Diagnostics("part.ncx");
        PluginSet plugins = TestPlugins.Load(_folder.FullName, diagnostics, TestPlugins.ShopRules);
        MachineConfig machine = PluginMachines.Load(PluginMachines.PlainMill);
        NcxProgram program = Parser.Parse(CliHarness.OneProgram("UNITS=MM", "SPINDLE:MAIN=CW RPM:MAIN=1500"),
            "part.ncx", new ParserOptions());
        var vm = new VirtualMachine(machine, VmOptions.ForMachine(machine), program.Diagnostics);
        foreach (IVmListener listener in plugins.Listeners)
        {
            vm.Subscribe(listener);
        }

        vm.Run(program);

        string[] recorded = (TestPlugins.Inner(Assert.Single(plugins.Listeners)).ToString() ?? "").Split('\n');
        Assert.Contains("STATE_CHANGE(4): S1 Off -> Clockwise", recorded);
        Assert.Contains("PROGRAM_END(5): S1 Clockwise -> Off", recorded);
        Assert.Empty(diagnostics.Items);
    }

    // Virtual machine 7, architecture 8: a compile runs the virtual machine STATIC with the listeners of its options
    // subscribed after the compiler, so the EventRecorder of ShopRules reads the same changes as in a run of its own.
    [Fact]
    public void IVmListener_ListenerOfAPluginInACompile_ReadsTheEventsOfTheStaticRun()
    {
        var diagnostics = new Diagnostics("part.ncx");
        PluginSet plugins = TestPlugins.Load(_folder.FullName, diagnostics, TestPlugins.ShopRules);
        NcxProgram program = Parser.Parse(CliHarness.OneProgram("UNITS=MM", "SPINDLE:MAIN=CW RPM:MAIN=1500"),
            "part.ncx", new ParserOptions());

        CompileResult result = new EchoCompiler().Compile(program, PluginMachines.Load(PluginMachines.PlainMill),
            new CompileOptions { Listeners = plugins.Listeners });

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        string[] recorded = (TestPlugins.Inner(Assert.Single(plugins.Listeners)).ToString() ?? "").Split('\n');
        Assert.Contains("STATE_CHANGE(4): S1 Off -> Clockwise", recorded);
        Assert.Contains("PROGRAM_END(5): S1 Clockwise -> Off", recorded);
        Assert.Empty(diagnostics.Items);
    }

    // D61, D106: Before and After are set once, when the event is made, and every type they hold, down to the last
    // value, has no setter but init and no collection that can be changed.
    [Fact]
    public void IVmListener_BeforeAndAfter_AreImmutableRecords()
    {
        var mutable = new List<string>();
        foreach (string name in new[] { nameof(VmEvent.Before), nameof(VmEvent.After) })
        {
            PropertyInfo property = typeof(VmEvent).GetProperty(name) ?? throw new InvalidOperationException(name);
            Assert.Equal(typeof(ChannelSnapshot), property.PropertyType);
            if (property.SetMethod is MethodInfo setter && !IsInitOnly(setter))
            {
                mutable.Add($"VmEvent.{name} has a setter");
            }
        }

        CollectMutable(typeof(ChannelSnapshot), [], mutable);

        Assert.True(mutable.Count == 0, string.Join('\n', mutable));
    }

    // The properties of a type and of every Ncx type they hold that can be changed after the object is made.
    private static void CollectMutable(Type type, HashSet<Type> seen, List<string> mutable)
    {
        if (!seen.Add(type))
        {
            return;
        }

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.SetMethod is MethodInfo setter && setter.IsPublic && !IsInitOnly(setter))
            {
                mutable.Add($"{type.Name}.{property.Name} has a setter");
            }

            if (IsChangeableCollection(property.PropertyType))
            {
                mutable.Add($"{type.Name}.{property.Name} is a collection that can be changed");
            }

            foreach (Type held in HeldTypes(property.PropertyType))
            {
                if (held.Namespace?.StartsWith("Ncx.", StringComparison.Ordinal) == true)
                {
                    CollectMutable(held, seen, mutable);
                }
            }
        }
    }

    // An init accessor carries the modifier IsExternalInit on its return value.
    private static bool IsInitOnly(MethodInfo setter)
    {
        return setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));
    }

    // A list, a dictionary, an array or any collection with Add can be changed; the read-only interfaces cannot.
    private static bool IsChangeableCollection(Type type)
    {
        if (type.IsArray || typeof(IList).IsAssignableFrom(type) || typeof(IDictionary).IsAssignableFrom(type))
        {
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>))
        {
            return true;
        }

        foreach (Type face in type.GetInterfaces())
        {
            if (face.IsGenericType && face.GetGenericTypeDefinition() == typeof(ICollection<>))
            {
                return true;
            }
        }

        return false;
    }

    // The type itself, or the types a generic type holds: IReadOnlyDictionary<string, SpindleSnapshot> holds
    // SpindleSnapshot.
    private static List<Type> HeldTypes(Type type)
    {
        Type plain = Nullable.GetUnderlyingType(type) ?? type;
        return plain.IsGenericType ? [.. plain.GetGenericArguments()] : [plain];
    }
}
