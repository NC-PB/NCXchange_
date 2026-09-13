using System.Reflection;
using System.Text;

namespace Ncx.Tests.Fixtures;

/// <summary>
/// The example files of the specification as every test project embeds them, and the repository root for the few
/// tests that read or write files of the repository itself (tests/README.md).
/// </summary>
internal static class Fixture
{
    // Every embedded example carries its path from the repository root as its logical name (tests/Fixtures.props).
    private const string ExamplesFolder = "docs/spec/examples/";

    // The repository root is the folder that holds the solution.
    private const string SolutionFile = "NCXchange.sln";

    // This file is compiled into every test project, so the assembly that holds this class is the test assembly
    // whose embedded examples are asked for.
    private static Assembly TestAssembly => typeof(Fixture).Assembly;

    /// <summary>
    /// Reads an example as text the way <see cref="File.ReadAllText(string)"/> reads the original: UTF-8
    /// (language 3), the line endings exactly as they are in the file.
    /// </summary>
    /// <param name="examplePath">The path relative to docs/spec/examples, for example "sources/BOHREN.h".</param>
    public static string ReadText(string examplePath)
    {
        using Stream stream = OpenExample(examplePath);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads an example byte for byte.
    /// </summary>
    /// <param name="examplePath">The path relative to docs/spec/examples, for example "2.5D_FRAESEN.ncx".</param>
    public static byte[] ReadBytes(string examplePath)
    {
        using Stream stream = OpenExample(examplePath);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>
    /// Lists the embedded examples by their path relative to docs/spec/examples in ordinal order, for example
    /// "2.5D_FRAESEN.ncx", "machines/millturn1.toml", "sources/BOHREN.h".
    /// </summary>
    public static IReadOnlyList<string> List()
    {
        var examples = new List<string>();
        foreach (string resourceName in TestAssembly.GetManifestResourceNames())
        {
            if (resourceName.StartsWith(ExamplesFolder, StringComparison.Ordinal))
            {
                examples.Add(resourceName.Substring(ExamplesFolder.Length));
            }
        }

        examples.Sort(StringComparer.Ordinal);
        return examples;
    }

    /// <summary>
    /// Finds the repository root: the nearest folder above the test assembly that holds NCXchange.sln.
    /// </summary>
    public static string RepositoryRoot()
    {
        // Tests are independent of the working directory; the walk starts at the test assembly (code-guidelines 8).
        string? folder = Path.GetDirectoryName(TestAssembly.Location);
        while (folder is not null)
        {
            if (File.Exists(Path.Combine(folder, SolutionFile)))
            {
                return folder;
            }

            folder = Path.GetDirectoryName(folder);
        }

        throw new InvalidOperationException($"No folder above {TestAssembly.Location} holds {SolutionFile}.");
    }

    // An example that is not embedded is a mistake in the test, reported with the path it asked for.
    private static Stream OpenExample(string examplePath)
    {
        Stream? stream = TestAssembly.GetManifestResourceStream(ExamplesFolder + examplePath);
        if (stream is null)
        {
            throw new ArgumentException(
                $"{examplePath} is not an embedded example; the path is relative to docs/spec/examples.",
                nameof(examplePath));
        }

        return stream;
    }
}
