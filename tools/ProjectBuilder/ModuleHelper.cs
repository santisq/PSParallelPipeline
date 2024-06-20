using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace ProjectBuilder;

public record struct ModuleDownload(string Module, Version Version)
{
    public static explicit operator ModuleDownload(DictionaryEntry entry) =>
        new(entry.Key.ToString(), LanguagePrimitives.ConvertTo<Version>(entry.Value));
};

public sealed class ModuleHelper
{
    public string ModulePath { get; }

    private UriBuilder _builder = new(_base);

    private const string _base = "https://www.powershellgallery.com";

    private const string _path = "api/v2/package/{0}/{1}";

    public ModuleHelper(string path)
    {
        ModulePath = Path.Combine(path, "tools", "Modules");
    }

    public string[] DownloadModules(ModuleDownload[] modules)
    {
        List<Task<string>> tasks = new(modules.Length);
        List<string> completed = new(modules.Length);
        foreach ((string module, Version version) in modules)
        {
            string destination = GetDestination(module);
            string modulePath = GetModulePath(module);
            if (Directory.Exists(modulePath))
            {
                Console.WriteLine($"Module '{module}' already downloaded. Skipping.");
                completed.Add(modulePath);
                continue;
            }

            Console.WriteLine($"Installing build pre-req '{module}'");
            _builder.Path = string.Format(_path, module, version);
            Task<string> task = DownloadAsync(_builder.Uri.ToString(), destination);
            tasks.Add(task);
        }

        completed.AddRange(WaitTaskAsync(tasks).GetAwaiter().GetResult());
        ExpandArchives([.. completed]);
        return [.. completed];
    }

    private void ExpandArchives(string[] archives)
    {
        foreach (string zip in archives)
        {
            ZipFile.ExtractToDirectory(
                zip,
                Path.GetFileNameWithoutExtension(zip));
        }
    }

    private async Task<string[]> WaitTaskAsync(
        List<Task<string>> tasks)
    {
        List<string> completed = new(tasks.Count);
        while (tasks.Count > 0)
        {
            Task<string> awaiter = await Task.WhenAny(tasks);
            tasks.Remove(awaiter);
            string module = await awaiter;
            completed.Add(module);
        }

        return [.. completed];
    }

    private string GetDestination(string module) =>
        Path.Combine(ModulePath, Path.ChangeExtension(module, "zip"));

    private string GetModulePath(string module) =>
        Path.Combine(ModulePath, module);

    private async Task<string> DownloadAsync(string uri, string destination)
    {
        using FileStream fs = File.Create(destination);
        using HttpClient client = new();
        using Stream stream = await client.GetStreamAsync(uri);
        await stream.CopyToAsync(fs);
        return destination;
    }
}
