using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// The files of templates/ncx-plugin/ and of the sample plugins against code-guidelines 11 and against each other, so
/// that the template, the samples and the guidelines cannot drift apart (implementation 17, P7-02).
/// </summary>
public sealed class TemplateFilesTests
{
    // What the build writes into bin/ and obj/ of a project is no file of the template.
    private static readonly string[] s_buildOutputFolders = ["bin", "obj"];

    // Code-guidelines 11: the template holds the project, the rule, the second example commented out, the README and
    // the test; the test is a project of its own, whose project file the listing of the guidelines leaves out.
    [Fact]
    public void Template_Files_AreTheFilesOfCodeGuidelines11()
    {
        List<string> files = FilesOf(Path.Combine(Fixture.RepositoryRoot(), "templates", "ncx-plugin"));

        Assert.Equal(
            [
                "CoolantClutchRule.cs",
                "MyShopRules.Tests/CoolantClutchRuleTests.cs",
                "MyShopRules.Tests/MyShopRules.Tests.csproj",
                "MyShopRules.csproj",
                "README.md",
                "ZOnItsOwnLine.cs",
            ],
            files);
    }

    // Code-guidelines 11: CoolantClutchRule.cs is the rule as the guidelines print it.
    [Fact]
    public void CoolantClutchRule_OfTheTemplate_IsThePrintOfCodeGuidelines11()
    {
        Assert.Equal(PrintedRule(), ReadRepositoryFile("templates", "ncx-plugin", "CoolantClutchRule.cs"));
    }

    // The coolant clutch sample is the plugin the template makes, in the namespace of its own name.
    [Fact]
    public void CoolantClutchRule_OfTheSample_IsTheRuleOfTheTemplate()
    {
        string template = ReadRepositoryFile("templates", "ncx-plugin", "CoolantClutchRule.cs");

        Assert.Equal(template.Replace("namespace MyShopRules;", "namespace CoolantClutch;", StringComparison.Ordinal),
            ReadRepositoryFile("samples", "plugins", "CoolantClutch", "CoolantClutchRule.cs"));
    }

    // Code-guidelines 11: ZOnItsOwnLine.cs of the template is the Z writer of the sample, commented out, in the
    // namespace of the template, after the lines that say how to use it.
    [Fact]
    public void ZOnItsOwnLine_OfTheTemplate_IsTheSampleCommentedOut()
    {
        string sample = ReadRepositoryFile("samples", "plugins", "ZOnItsOwnLine", "ZOnItsOwnLine.cs")
            .Replace("namespace ZOnItsOwnLine;", "namespace MyShopRules;", StringComparison.Ordinal);
        string template = ReadRepositoryFile("templates", "ncx-plugin", "ZOnItsOwnLine.cs");

        string commentedOut = CommentedOut(sample);
        Assert.EndsWith(commentedOut, template, StringComparison.Ordinal);
        Assert.StartsWith("// ", template, StringComparison.Ordinal);
    }

    // The one C# block of code-guidelines section 11, the rule of the template as printed there.
    private static string PrintedRule()
    {
        string guidelines = ReadRepositoryFile("docs", "architecture", "code-guidelines.md");
        int section = guidelines.IndexOf("## 11. Plugin template", StringComparison.Ordinal);
        int start = guidelines.IndexOf("```csharp\n", section, StringComparison.Ordinal) + "```csharp\n".Length;
        int end = guidelines.IndexOf("```", start, StringComparison.Ordinal);
        return guidelines.Substring(start, end - start);
    }

    // Every line with two slashes and a space in front; an empty line with the two slashes alone.
    private static string CommentedOut(string code)
    {
        var lines = new List<string>();
        foreach (string line in code.TrimEnd('\n').Split('\n'))
        {
            lines.Add(line.Length == 0 ? "//" : "// " + line);
        }

        return string.Join('\n', lines) + "\n";
    }

    // The files of a folder and of its subfolders but bin/ and obj/, named from the folder with forward slashes, in
    // ordinal order.
    private static List<string> FilesOf(string folder)
    {
        var files = new List<string>();
        AddFiles(folder, folder, files);
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static void AddFiles(string root, string folder, List<string> files)
    {
        foreach (string file in Directory.GetFiles(folder))
        {
            files.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
        }

        foreach (string subfolder in Directory.GetDirectories(folder))
        {
            if (!s_buildOutputFolders.Contains(Path.GetFileName(subfolder)))
            {
                AddFiles(root, subfolder, files);
            }
        }
    }

    // A text file of the repository with LF line endings.
    private static string ReadRepositoryFile(params string[] parts)
    {
        return File.ReadAllText(Path.Combine([Fixture.RepositoryRoot(), .. parts])).ReplaceLineEndings("\n");
    }
}
