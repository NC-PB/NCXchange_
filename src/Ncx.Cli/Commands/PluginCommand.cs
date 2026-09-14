using System.CommandLine;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx plugin new &lt;name&gt;, build [&lt;folder&gt;], check &lt;dll&gt; and test [&lt;folder&gt;]: a runnable plugin
/// without knowing dotnet new (architecture 10; code-guidelines 11; implementation 17, P7-02). new copies the template
/// next to ncx and renames it, build runs dotnet build and puts the DLL into plugins/ with its line in ncx.toml, check
/// loads a DLL as a run does and lists its interfaces, test runs dotnet test on the tests of the plugin. Each takes
/// --strict (D97); the diagnostics go to the standard error (D98).
/// </summary>
internal static class PluginCommand
{
    /// <summary>
    /// The plugin command of the ncx root command, with its four commands (architecture 10).
    /// </summary>
    /// <param name="output">The standard output: the files a command wrote, the listing of check, the tests.</param>
    /// <param name="error">The standard error, for the diagnostics.</param>
    public static Command Create(TextWriter output, TextWriter error)
    {
        return new Command("plugin", "Make, build, check and test a plugin, one C# file from the plugin template.")
        {
            NewCommand(output, error),
            BuildCommand(output, error),
            CheckCommand(output, error),
            TestCommand(output, error),
        };
    }

    // ncx plugin new <name>: copies the template into ./<name>/ and renames it (implementation 17, P7-02).
    private static Command NewCommand(TextWriter output, TextWriter error)
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "The name of the plugin, of its folder, its namespace and its DLL: MyShopRules.",
        };
        var strictOption = RunOptions.StrictOption();
        var command = new Command("new", "Copy the plugin template into a new folder of this name and rename it.")
        {
            nameArgument,
            strictOption,
        };
        command.SetAction(parseResult => PluginNew.Run(
            parseResult.GetRequiredValue(nameArgument), Settings(parseResult, strictOption), output, error));
        return command;
    }

    // ncx plugin build [<folder>]: dotnet build, the DLL into plugins/, its line into ncx.toml (implementation 17,
    // P7-02).
    private static Command BuildCommand(TextWriter output, TextWriter error)
    {
        Argument<string> folderArgument = FolderArgument();
        var strictOption = RunOptions.StrictOption();
        var command = new Command(
            "build",
            "Build the plugin with dotnet build, put its DLL into plugins/ and add its line to ncx.toml.")
        {
            folderArgument,
            strictOption,
        };
        command.SetAction(parseResult => PluginBuild.Run(
            parseResult.GetValue(folderArgument), Settings(parseResult, strictOption), output, error));
        return command;
    }

    // ncx plugin check <dll>: loads the DLL in isolation and lists its interfaces (implementation 17, P7-02).
    private static Command CheckCommand(TextWriter output, TextWriter error)
    {
        var dllArgument = new Argument<string>("dll") { Description = "The plugin DLL: plugins/MyShopRules.dll." };
        var strictOption = RunOptions.StrictOption();
        var command = new Command("check", "Load a plugin DLL as ncx does and list the interfaces it implements.")
        {
            dllArgument,
            strictOption,
        };
        command.SetAction(parseResult => PluginCheck.Run(
            parseResult.GetRequiredValue(dllArgument), Settings(parseResult, strictOption), output, error));
        return command;
    }

    // ncx plugin test [<folder>]: dotnet test on the tests of the plugin (implementation 17, P7-02).
    private static Command TestCommand(TextWriter output, TextWriter error)
    {
        Argument<string> folderArgument = FolderArgument();
        var strictOption = RunOptions.StrictOption();
        var command = new Command("test", "Run the tests of the plugin with dotnet test.")
        {
            folderArgument,
            strictOption,
        };
        command.SetAction(parseResult => PluginTest.Run(
            parseResult.GetValue(folderArgument), Settings(parseResult, strictOption), output, error));
        return command;
    }

    // The folder of the plugin, which build and test may leave out (implementation 17, P7-02; PluginFolder).
    private static Argument<string> FolderArgument()
    {
        return new Argument<string>("folder")
        {
            Description = "The folder of the plugin. Without it: the working directory when it holds the project of "
                + "a plugin, else the one plugin that ncx plugin new made in it.",
            Arity = ArgumentArity.ZeroOrOne,
        };
    }

    private static PluginCommandSettings Settings(ParseResult parseResult, Option<bool> strictOption)
    {
        return new PluginCommandSettings { Strict = parseResult.GetValue(strictOption) };
    }
}
