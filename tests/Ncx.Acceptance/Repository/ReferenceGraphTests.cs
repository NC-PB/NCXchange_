using System.Text;
using System.Xml.Linq;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Repository;

/// <summary>
/// Reads the project files and checks them against the dependency diagram of architecture 3 and the package rule of
/// code-guidelines 9, so that a wrong reference fails the build the day it is added (phase 0, P0-01).
/// </summary>
public sealed class ReferenceGraphTests
{
    // Dependencies point inward: Ncx.Core depends on nothing but the .NET base library, Ncx.Config on Ncx.Core,
    // readers and compilers on both, Ncx.Analytics on Ncx.Core, Ncx.Plugins on Ncx.Core, Ncx.Readers and
    // Ncx.Compilers so that a plugin references Ncx.Plugins alone, the CLI on everything (architecture 3, D106).
    // The machine model lives in Ncx.Core, so nothing in Core needs Ncx.Config (D107).
    private static readonly Dictionary<string, string[]> s_sourceProjectReferences = new()
    {
        ["Ncx.Core"] = [],
        ["Ncx.Config"] = ["Ncx.Core"],
        ["Ncx.Readers"] = ["Ncx.Core", "Ncx.Config"],
        ["Ncx.Compilers"] = ["Ncx.Core", "Ncx.Config"],
        ["Ncx.Analytics"] = ["Ncx.Core"],
        ["Ncx.Plugins"] = ["Ncx.Core", "Ncx.Readers", "Ncx.Compilers"],
        ["Ncx.Cli"] = ["Ncx.Core", "Ncx.Config", "Ncx.Readers", "Ncx.Compilers", "Ncx.Analytics", "Ncx.Plugins"],
    };

    // Each test project references what it tests; Ncx.Analytics, Ncx.Plugins and Ncx.Cli are tested from
    // Ncx.Acceptance until they deserve a test project of their own (implementation 00-method 4).
    private static readonly Dictionary<string, string[]> s_testProjectReferences = new()
    {
        ["Ncx.Core.Tests"] = ["Ncx.Core"],
        ["Ncx.Config.Tests"] = ["Ncx.Config"],
        ["Ncx.Readers.Tests"] = ["Ncx.Readers"],
        ["Ncx.Compilers.Tests"] = ["Ncx.Compilers"],
        ["Ncx.Acceptance"] = ["Ncx.Analytics", "Ncx.Plugins", "Ncx.Cli"],
    };

    // The packages are Tomlyn for the TOML parser of Ncx.Config, System.CommandLine for the CLI, and xUnit with its
    // runner and the test SDK in the test projects; nothing else without a decision in the log (architecture 3,
    // code-guidelines 4 and 9).
    private static readonly string[] s_testPackages = ["Microsoft.NET.Test.Sdk", "xunit", "xunit.runner.visualstudio"];

    private static readonly Dictionary<string, string[]> s_packageReferences = new()
    {
        ["Ncx.Core"] = [],
        ["Ncx.Config"] = ["Tomlyn"],
        ["Ncx.Readers"] = [],
        ["Ncx.Compilers"] = [],
        ["Ncx.Analytics"] = [],
        ["Ncx.Plugins"] = [],
        ["Ncx.Cli"] = ["System.CommandLine"],
        ["Ncx.Core.Tests"] = s_testPackages,
        ["Ncx.Config.Tests"] = s_testPackages,
        ["Ncx.Readers.Tests"] = s_testPackages,
        ["Ncx.Compilers.Tests"] = s_testPackages,
        ["Ncx.Acceptance"] = s_testPackages,
    };

    [Fact]
    public void DependencyDiagram_SourceProjects_ReferenceExactlyTheAllowedProjects()
    {
        Dictionary<string, string[]> actual = ReadProjects("ProjectReference", "src");

        Assert.Equal(Render(s_sourceProjectReferences), Render(actual));
    }

    [Fact]
    public void DependencyDiagram_TestProjects_ReferenceWhatTheyTest()
    {
        Dictionary<string, string[]> actual = ReadProjects("ProjectReference", "tests");

        Assert.Equal(Render(s_testProjectReferences), Render(actual));
    }

    [Fact]
    public void PackageRule_EveryProject_ReferencesOnlyItsAllowedPackages()
    {
        Dictionary<string, string[]> actual = ReadProjects("PackageReference", "src", "tests");

        Assert.Equal(Render(s_packageReferences), Render(actual));
    }

    [Fact]
    public void PackageRule_CentralVersions_PinOnlyTheFivePackages()
    {
        string packagesFile = Path.Combine(Fixture.RepositoryRoot(), "Directory.Packages.props");
        var pinned = new List<string>();
        foreach (XElement packageVersion in XDocument.Load(packagesFile).Descendants("PackageVersion"))
        {
            pinned.Add(packageVersion.Attribute("Include")?.Value ?? "");
        }

        pinned.Sort(StringComparer.Ordinal);
        Assert.Equal(["Microsoft.NET.Test.Sdk", "System.CommandLine", "Tomlyn", "xunit", "xunit.runner.visualstudio"],
            pinned);
    }

    // A project of the solution is a project file directly inside a folder of src/ or tests/ (architecture 3); what
    // it references is the Include of each item of the given type, a project by its file name.
    private static Dictionary<string, string[]> ReadProjects(string itemType, params string[] folders)
    {
        var projects = new Dictionary<string, string[]>();
        foreach (string folder in folders)
        {
            foreach (string projectFolder in Directory.GetDirectories(Path.Combine(Fixture.RepositoryRoot(), folder)))
            {
                foreach (string projectFile in Directory.GetFiles(projectFolder, "*.csproj"))
                {
                    var references = new List<string>();
                    foreach (XElement item in XDocument.Load(projectFile).Descendants(itemType))
                    {
                        string include = (item.Attribute("Include")?.Value ?? "").Replace('\\', '/');
                        if (itemType == "ProjectReference")
                        {
                            include = Path.GetFileNameWithoutExtension(include);
                        }

                        references.Add(include);
                    }

                    projects[Path.GetFileNameWithoutExtension(projectFile)] = [.. references];
                }
            }
        }

        return projects;
    }

    // One line per project, "Ncx.Readers: Ncx.Config, Ncx.Core", in ordinal order, so that a failing comparison
    // prints both graphs as text.
    private static string Render(Dictionary<string, string[]> projects)
    {
        var names = new List<string>(projects.Keys);
        names.Sort(StringComparer.Ordinal);
        var text = new StringBuilder();
        foreach (string name in names)
        {
            var references = new List<string>(projects[name]);
            references.Sort(StringComparer.Ordinal);
            text.Append(name).Append(": ").AppendJoin(", ", references).Append('\n');
        }

        return text.ToString();
    }
}
