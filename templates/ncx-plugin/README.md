# MyShopRules

A plugin for `ncx`, made by `ncx plugin new` from the plugin template of NCXchange, `templates/ncx-plugin/` (code-guidelines 11). `CoolantClutchRule.cs` is the plugin: one rule with one method to change, which stops the spindle before the through-spindle coolant is switched on and starts it again afterwards. `ZOnItsOwnLine.cs` is a second example, a block writer that puts `Z` on a line of its own, commented out. `MyShopRules.Tests/CoolantClutchRuleTests.cs` is one test to copy from. What a plugin can do, and what it may not do, is in `docs/plugins.md` of NCXchange.

The coolant clutch rule could be written as an expansion rule of the machine file just as well, without any code (machine-config 5a):

```toml
[coolant]
THROUGH = { ON = "M51", OFF = "M9", requires = { SPINDLE = "OFF" }, restore = ["SPINDLE"] }
```

The template uses it anyway, because every NC programmer has met a machine like it, and the mechanism is easiest to follow with a case one knows. Before you write a rule as a plugin, look whether the machine file can say it.

## Six steps

1. Install the .NET SDK, one download from `https://dotnet.microsoft.com/download`. `ncx plugin build` and `ncx plugin test` run its command `dotnet`, which must be on the path.
2. Make the plugin in the folder of your NC programs, the folder of `ncx.toml`:

   ```sh
   ncx plugin new MyShopRules
   ```

3. Open `MyShopRules/CoolantClutchRule.cs` and change the words in it: the three words of `block.Has(...)` to the coolant of your machine file, the blocks of `Surround(...)` to what your machine needs, and the reason, which `ncx trace` shows next to the blocks.
4. Build it, in the same folder:

   ```sh
   ncx plugin build
   ```

   With more than one plugin in the folder, name the one to build: `ncx plugin build MyShopRules`.
5. Copy nothing: the command has put `MyShopRules.dll` into `plugins/` and added the line to `ncx.toml`:

   ```toml
   plugins = ["MyShopRules.dll"]
   ```

6. Compile a program and read what the plugin did:

   ```sh
   ncx compile part.ncx --machine dmu50
   ```

   ```text
   part.ncx(12): INFO PLG001: plugin MyShopRules: inserted 3 blocks at line 12
   ```

   <!-- TODO(question): code-guidelines 11 and D98 print "inserted 2 blocks" for this rule, which inserts three. -->

7. Optional: run the test of the plugin, and change it together with the rule:

   ```sh
   ncx plugin test
   ```

`ncx plugin check plugins/MyShopRules.dll` loads the plugin as `ncx` does and lists the interfaces it implements.
