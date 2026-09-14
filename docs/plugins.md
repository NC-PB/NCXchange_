# Plugins

Status: 2026-09-14, written with task P7-02. How an NC programmer with a little C# writes a plugin for `ncx`: a rule the machine file cannot express, in one C# file from the plugin template (`architecture/code-guidelines.md`, sections 10 and 11). This page grows with the questions people ask.

A plugin acts in four places, and none of them is the state of the virtual machine (D61): it reads source blocks in a reader, rewrites blocks in the expander before the virtual machine runs them, reads the events of the virtual machine, and edits the output lines of a compiler (`spec/ncx-virtual-machine.md`, section 7; `architecture/architecture.md`, section 9).

## First look at the machine file

Most of what a machine needs is configuration, and a TOML rule needs no compiler and no build (code-guidelines 10.1). A coolant that needs a standing spindle, a retract before the tool change, a mode M code before a cycle are expansion rules of the machine file (`spec/machine-config.md`, section 5a); the speed and feed limits and the travel limits are configuration too, and the expander applies them itself. The coolant clutch of the template is such a rule:

```toml
[coolant]
THROUGH = { ON = "M51", OFF = "M9", requires = { SPINDLE = "OFF" }, restore = ["SPINDLE"] }
```

A plugin is for what the machine file cannot say: a limit that depends on the tool, a sequence that depends on two states, a changed layout of the output.

## Six steps

The template in `../templates/ncx-plugin/` is a plugin that builds and runs as it is. Its README has the same steps.

1. Install the .NET SDK, one download from `https://dotnet.microsoft.com/download`. `ncx plugin build` and `ncx plugin test` run its command `dotnet`, which must be on the path; they say so when it is not.
2. Make the plugin in the folder of your NC programs, the folder of `ncx.toml`:

   ```sh
   ncx plugin new MyShopRules
   ```

   The command copies the template into `MyShopRules/`, renames it and names every file it writes. The name becomes the folder, the namespace and the DLL of the plugin, and the name of the plugin in `ncx.toml` and in every diagnostic about it.
3. Open `MyShopRules/CoolantClutchRule.cs` and change the words in it.
4. Build it, in the same folder:

   ```sh
   ncx plugin build
   ```

   With more than one plugin in the folder, name the one to build: `ncx plugin build MyShopRules`.
5. Copy nothing: the command has put the DLL into `plugins/` and added the line to `ncx.toml`, and says so:

   ```text
   plugins/MyShopRules.dll
   ncx.toml: plugins = ["MyShopRules.dll"]
   ```

6. Run the program through `ncx` and read what the plugin did, with `ncx compile` or with `ncx check`:

   ```text
   $ ncx check part.ncx --machine mill
   part.ncx(12): INFO PLG001: plugin MyShopRules: inserted 3 blocks at line 12
   ```

7. Optional: `ncx plugin test` runs the test of the plugin, `MyShopRules/MyShopRules.Tests/CoolantClutchRuleTests.cs`, an NCX program in and the program the virtual machine runs out. Change it together with the rule.

`ncx plugin check plugins/MyShopRules.dll` loads the plugin as `ncx` loads it and lists what it implements, with the version of the `Ncx` assemblies it was built against:

```text
plugins/MyShopRules.dll: plugin MyShopRules
  built against Ncx.Core 1.0.0.0
  IProgramRewriter: 1 class
```

The plugin references `Ncx.Plugins` in the folder of `ncx`, with `Ncx.Core`, `Ncx.Readers` and `Ncx.Compilers`, which `Ncx.Plugins` brings with it, and no package (D106). `ncx plugin build` builds against the assemblies of the `ncx` that runs it, because a plugin shares them with `ncx` when it is loaded; a plugin built for an older `ncx` loads until it calls something that has changed, so build it again after an update.

## The four interfaces

Each interface has one method, and a plugin implements only what it needs. Every public class of the DLL that implements one of them is made once per run, with a constructor without parameters.

| Interface | Where it acts | What it may do |
|---|---|---|
| `ISourceRule` | in a reader, per source block, with the source-side state | decide what a machine-specific M code or sequence of source blocks means on this machine and write the NCX words for it |
| `IProgramRewriter` | in the expander, before the virtual machine | rewrite the words of a block, insert generated blocks before or after it |
| `IVmListener` | on every event of the virtual machine, with `Before` and `After` | observe |
| `IBlockWriter` | on `BLOCK_WRITE` in the compiler | edit the output lines of a block |

The template brings the namespaces of `ncx` with it, so a `.cs` file of a plugin needs no `using` line.

### IProgramRewriter: the coolant clutch

The expander asks every block of the program once, before the virtual machine runs it. The answer is `RewriteResult.Unchanged`, `RewriteResult.Replace(text, reason)` with the new text of the block, or `RewriteResult.Surround(before, after, reason)` with blocks before and after it. `Block.Has(key)`, `Block.Has(key, addr, value)` and `Block.Find(...)` are the lookups a rule needs. The blocks are NCX text, which the expander parses like a line of an `.ncx` file; they may carry `@SAVE` and `@RESTORE`, which bring back a state the rule had to change, so the rule does not need to know the value (virtual machine 3.10). `context.MachineName`, `context.Channel`, `context.Line` and `context.Settings` are what a rule knows of the run.

```csharp
public sealed class CoolantClutchRule : IProgramRewriter
{
    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        if (!block.Has(key: "COOLANT", addr: "THROUGH", value: "ON"))
        {
            return RewriteResult.Unchanged;
        }

        return RewriteResult.Surround(
            before: ["@SAVE=SPINDLE:MAIN", "SPINDLE:MAIN=OFF"],
            after: ["@RESTORE=SPINDLE:MAIN"],
            reason: "the coolant clutch needs a standing spindle");
    }
}
```

The whole rule, with its comments, is `../templates/ncx-plugin/CoolantClutchRule.cs`, and `../samples/plugins/CoolantClutch/` is the same rule as a finished plugin with its tests. A block that does not parse rejects the whole answer with `PLG004` on the line of the block; the blocks a rewriter inserted are the INFO `PLG001`.

### ISourceRule: an M code the machine file does not name

A reader offers a rule the source blocks that the tables of the machine file leave undecided, one block at a time, with the source-side state read only. A rule that claims a block writes its NCX blocks with the builder and returns `true`; the reader then reads nothing more of it and keeps its comment. A rule that returns `false` leaves the block to the reader.

```csharp
public sealed class ShopCoolantCode : ISourceRule
{
    public bool Read(SourceBlock block, SourceState state, NcxBuilder builder)
    {
        if (block.Find("M")?.Number != 456m)
        {
            return false;
        }

        builder.Begin(block.Line).Word("COOLANT", null, new IdentValue("ON")).End();
        return true;
    }
}
```

`../samples/plugins/ThroughCoolantSourceRule/` folds the three blocks `M5`, `M51`, `M3 S1500` that a machine with a coolant clutch writes back into `COOLANT:THROUGH=ON` (D66). It cannot do so in a reader yet: the tables of the machine file are tried first and name all three codes (D231), and a rule sees the one block it is offered, none after it (D232). Its test shows the fold with the three blocks.

### IVmListener: reading the state around a block

A listener receives every event of the virtual machine, `TOOL_BEGIN`, `MOTION`, `STATE_CHANGE` and the others of virtual machine 7, each with `Before` and `After`, the state of the channel around the block. Both are snapshots that nothing can change.

```csharp
public sealed class SpindleWatch : IVmListener
{
    public void On(VmEvent vmEvent)
    {
        foreach (KeyValuePair<string, SpindleSnapshot> spindle in vmEvent.After.Spindles)
        {
            if (vmEvent.Before.Spindles.TryGetValue(spindle.Key, out SpindleSnapshot? before)
                && before.Direction != spindle.Value.Direction)
            {
                Console.Error.WriteLine(
                    $"{vmEvent.Kind}({vmEvent.Block.Line}): {spindle.Key} {before.Direction} -> {spindle.Value.Direction}");
            }
        }
    }
}
```

A listener has no way to change the run; what it finds, it writes where the shop reads it.

### IBlockWriter: Z on a line of its own

A block writer receives `BLOCK_WRITE` for every block the compiler writes, with the lines of the target controller before they reach the NC file, and edits `OutputLines` in place: changes, removes or inserts lines. `Words` are the words of the block, a copy to read; `Before` and `After` are the state around it.

```csharp
public sealed class ZOnItsOwnLine : IBlockWriter
{
    public void Write(BlockWriteEvent blockWrite)
    {
        // L X+10 Z-5 becomes L X+10 and L Z-5; Z goes down after the other axes, up before them.
    }
}
```

The whole writer is `../samples/plugins/ZOnItsOwnLine/ZOnItsOwnLine.cs`, with its tests; the template carries it commented out in `ZOnItsOwnLine.cs`.

## What a plugin may not do

- Write the state of the virtual machine (D61). A rewriter that clamps a speed changes the `RPM` word, the virtual machine runs the changed block, and `trace` shows the change with the name of the plugin. There is no way into the virtual machine from a plugin: the context of a rewriter offers the machine name, the channel, the line and the plugin's own settings, and nothing else (D80, D106).
- Change what a listener reads. `Before` and `After` are records that cannot be changed; a listener observes.
- Reach the state through the output. A block writer changes the lines of the NC file and nothing else; what it does to `Words` is not written.
- Do what the machine file does. Limits of the machine (`rpm_max`, `max_feed`, travel limits) are configuration, applied by the expander.

A plugin that fails is reported with its name and left out for the rest of the run, which goes on without it: a DLL that does not load is the ERROR `PLG002`, a class that throws in its method the ERROR `PLG003`, a text that does not parse the ERROR `PLG004`, a DLL without a class of the four interfaces the WARNING `PLG005`. `ncx compile` writes no NC file after an ERROR of a plugin, because a program the plugin did not see must not reach the machine.

## Settings

A plugin reads its own section of `ncx.toml` through the context of its rewriters, as strings, and nothing else of the file (D80):

```toml
[plugins.MyShopRules]
before_tool = "HOME Z"
```

```csharp
if (context.Settings.TryGetValue("before_tool", out string? before))
{
    return RewriteResult.Surround(before: [before], after: [], reason: "before_tool of the shop");
}
```

A file cannot hold the list `plugins = [...]` beside such a section (D238, open). A plugin with a section of its own is found in `plugins/`, where every DLL is loaded anyway; `ncx plugin build` leaves an `ncx.toml` with sections as it is and says so (`CLI557`).

## Where ncx finds plugins

Every DLL of `plugins/` of the working directory is a plugin, and so is every DLL that the list `plugins = [...]` of `ncx.toml` names, by its path from the working directory or by its name in `plugins/` (`spec/machine-config.md`, section 10). The list comes first, then the DLLs of `plugins/` in the order of their names; the rewriters of several plugins act on a block in that order. A plugin is named after its DLL, `MyShopRules.dll` is `MyShopRules`. Each DLL loads into a context of its own, which shares the `Ncx` assemblies with `ncx` and keeps everything else the plugin brings apart from `ncx` and from the other plugins (D106).

## Finding out what a plugin did

- `ncx check part.ncx` reports what the plugins did and what went wrong, with the `PLG` codes above and the name of the plugin in every message.
- `ncx trace part.ncx` writes a row for every changed state variable; a block a plugin inserted is its line followed by the name of the plugin and its reason, a block it rewrote says `rewritten by`:

  ```text
  channel  block                                                                      variable         old  new
  1        12 generated by MyShopRules (the coolant clutch needs a standing spindle)  SPINDLE:S1       CW   OFF
  1        12                                                                         COOLANT:THROUGH  OFF  ON
  1        12 generated by MyShopRules (the coolant clutch needs a standing spindle)  SPINDLE:S1       OFF  CW
  ```

- `ncx annotate part.ncx` writes the program with the values before each block, and the blocks of the plugins as comment lines where they ran.
- `ncx plugin check plugins/MyShopRules.dll` shows whether the DLL loads and what it implements; `ncx plugin test` runs its tests.

## The commands

| Command | What it does | Exit code |
|---|---|---|
| `ncx plugin new <name>` | copies the template next to `ncx` into `./<name>/` and renames it | 2 for a name that is no name of a plugin (`CLI550`), a folder that is there (`CLI551`), a missing template (`CLI552`) |
| `ncx plugin build [<folder>]` | runs `dotnet build`, puts the DLL into `plugins/`, adds its line to `ncx.toml` | 1 when dotnet cannot be started (`CLI554`) or the build fails (`CLI555`, `CLI556`); 2 when there is no plugin (`CLI553`) |
| `ncx plugin check <dll>` | loads the DLL as a run does and lists its interfaces | 1 when it does not load (`PLG002`); 2 when the file is not there |
| `ncx plugin test [<folder>]` | runs `dotnet test` on `<name>.Tests` of the plugin | 1 when a test fails (`CLI559`); 2 without a test project (`CLI558`) |

Each takes `--strict`, under which a WARNING exits with 1 (D97). Without a folder, `build` and `test` take the working directory when it holds the project of a plugin, else the one plugin that `ncx plugin new` made in it.

## Samples

`../samples/plugins/` holds a finished plugin for each of three things plugin authors ask for first, each with its tests: the coolant clutch rule, the Z writer and the source rule of D66.
