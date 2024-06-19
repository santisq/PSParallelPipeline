using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Xml;

public sealed class ProjectManifest
{
    public DirectoryInfo Project { get; }

    public DirectoryInfo Module { get; }

    public FileInfo? Manifest { get; internal set; }

    public Version? ManifestVersion { get; internal set; }

    public DirectoryInfo Source { get; }

    public string BuildPath { get; }

    public string? ReleasePath { get; internal set; }

    public string ModuleName { get; }

    public string[]? TargetFrameworks { get; internal set; }

    public string TestFramework { get => TargetFrameworks.FirstOrDefault(); }

    private ProjectManifest(string path)
    {
        Project = AssertDirectory(path);
        BuildPath = GetBuildPath(path);
        ModuleName = Path.GetFileNameWithoutExtension(path);
        Module = AssertDirectory(GetModulePath(path));
        Source = AssertDirectory(GetSourcePath(path, ModuleName));
    }

    public static ProjectManifest Create(string path)
    {
        ProjectManifest builder = new(path);
        builder.Manifest = GetManifest(builder);
        builder.ManifestVersion = GetManifestVersion(builder);
        builder.ReleasePath = GetReleasePath(
            builder.BuildPath,
            builder.ModuleName,
            builder.ManifestVersion!);
        builder.TargetFrameworks = GetTargetFrameworks(GetProjectFile(builder));

        return builder;
    }

    private static string[] GetTargetFrameworks(string path)
    {
        XmlDocument xmlDocument = new();
        xmlDocument.Load(path);
        return xmlDocument
            .SelectSingleNode("Project/PropertyGroup/TargetFrameworks")
            .InnerText
            .Split(new []{ ';' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string GetBuildPath(string path) =>
        Path.Combine(path, "output");

    private static string GetSourcePath(string path, string moduleName) =>
        Path.Combine(path, "src", moduleName);

    private static string GetModulePath(string path) =>
        Path.Combine(path, "module");

    private static string GetReleasePath(
        string buildPath,
        string moduleName,
        Version version) => Path.Combine(
            buildPath,
            moduleName,
            LanguagePrimitives.ConvertTo<string>(version));

    private static DirectoryInfo AssertDirectory(string path)
    {
        DirectoryInfo directory = new(path);
        return directory.Exists ? directory
            : throw new ArgumentException(
                $"Path '{path}' could not be found or is not a Directory.",
                nameof(path));
    }

    private static FileInfo GetManifest(ProjectManifest builder) =>
        builder.Module.EnumerateFiles("*.psd1").FirstOrDefault()
            ?? throw new FileNotFoundException(
                $"Manifest file could not be found in '{builder.Project.FullName}'");

    private static string GetProjectFile(ProjectManifest builder) =>
        builder.Source.EnumerateFiles("*.csproj").FirstOrDefault()?.FullName
            ?? throw new FileNotFoundException(
                $"Project file could not be found in ''{builder.Source.FullName}'");

    private static Version? GetManifestVersion(ProjectManifest builder)
    {
        using PowerShell powershell = PowerShell.Create(RunspaceMode.CurrentRunspace);
        Hashtable moduleInfo = powershell
            .AddCommand("Import-PowerShellDataFile")
            .AddArgument(builder.Manifest?.FullName)
            .Invoke<Hashtable>()
            .FirstOrDefault();

        return powershell.HadErrors
            ? throw powershell.Streams.Error.First().Exception
            : LanguagePrimitives.ConvertTo<Version>(moduleInfo?["ModuleVersion"]);
    }
}
