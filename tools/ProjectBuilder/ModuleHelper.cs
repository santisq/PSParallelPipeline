using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace ProjectBuilder;

public record struct ModuleDownload(string Module, Version Version)
{
    public static explicit operator ModuleDownload(DictionaryEntry entry) =>
        new(entry.Key.ToString(), LanguagePrimitives.ConvertTo<Version>(entry.Value));
};

internal record struct ModuleDownloadReference(string Zip, string Path);

public sealed class ModuleHelper
{
    public string ModulePath { get; }

    private readonly UriBuilder _builder = new(_base);

    private const string _base = "https://www.powershellgallery.com";

    private const string _path = "api/v2/package/{0}/{1}";

    private List<Task<ModuleDownloadReference>>? _tasks;

    private List<ModuleDownloadReference>? _completedTasks;

    public ModuleHelper(string path)
    {
        ModulePath = Path.Combine(path, "tools", "Modules");
        if (!Directory.Exists(ModulePath))
        {
            Directory.CreateDirectory(ModulePath);
        }
    }

    public string[] DownloadModules(ModuleDownload[] modules)
    {
        _tasks ??= new(modules.Length);
        _completedTasks ??= new(modules.Length);
        List<string> output = new(modules.Length);

        foreach ((string module, Version version) in modules)
        {
            string destination = GetDestination(module);
            string modulePath = GetModulePath(module);
            if (Directory.Exists(modulePath))
            {
                Console.WriteLine($"Module '{module}' already downloaded. Skipping.");
                output.Add(modulePath);
                continue;
            }

            Console.WriteLine($"Installing build pre-req '{module}'");
            _builder.Path = string.Format(_path, module, version);
            Task<ModuleDownloadReference> task = DownloadAsync(
                uri: _builder.Uri.ToString(),
                destination: destination,
                expandPath: modulePath);
            _tasks.Add(task);
        }

        WaitTaskAsync().GetAwaiter().GetResult();
        ExpandArchives([.. _completedTasks]);
        output.AddRange(_completedTasks.Select(e => e.Path));
        return [.. output];
    }

    private static void ExpandArchives(ModuleDownloadReference[] archives)
    {
        foreach ((string zip, string expandPath) in archives)
        {
            ZipFile.ExtractToDirectory(zip, expandPath);
        }
    }

    private async Task WaitTaskAsync()
    {
        while (_tasks?.Count > 0)
        {
            Task<ModuleDownloadReference> awaiter = await Task.WhenAny(_tasks);
            _tasks.Remove(awaiter);
            ModuleDownloadReference module = await awaiter;
            _completedTasks?.Add(module);
        }
    }

    private string GetDestination(string module) =>
        Path.Combine(ModulePath, Path.ChangeExtension(module, "zip"));

    private string GetModulePath(string module) =>
        Path.Combine(ModulePath, module);

    private static async Task<ModuleDownloadReference> DownloadAsync(
        string uri,
        string destination,
        string expandPath)
    {
        using FileStream fs = File.Create(destination);
        using HttpClient client = new();
        using Stream stream = await client.GetStreamAsync(uri);
        await stream.CopyToAsync(fs);
        return new ModuleDownloadReference(destination, expandPath);
    }
}
