using System.IO;
using System.Linq;

namespace ProjectBuilder;

public sealed class Project
{
    public DirectoryInfo Source { get; }

    public string Build { get; }

    public string? Release { get; internal set; }

    public string[]? TargetFrameworks { get; internal set; }

    public string? TestFramework { get => TargetFrameworks.FirstOrDefault(); }

    internal Project(DirectoryInfo source, string build)
    {
        Source = source;
        Build = build;
    }
}
