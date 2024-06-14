using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

internal sealed class PSTask : IDisposable
{
    private PSOutputStreams OutputStreams { get => _pool.PSOutputStreams; }

    internal Runspace Runspace
    {
        get => _powershell.Runspace;
        set => _powershell.Runspace = value;
    }

    private readonly PowerShell _powershell;

    private readonly PSDataStreams _streams;

    private readonly RunspacePool _pool;

    [ThreadStatic]
    private static Dictionary<string, object?>? _input;

    private PSTask(RunspacePool runspacePool)
    {
        _powershell = PowerShell.Create();
        _streams = _powershell.Streams;
        _pool = runspacePool;
    }

    static internal PSTask Create(RunspacePool runspacePool)
    {
        PSTask ps = new(runspacePool);
        ps.HookStreams(runspacePool.PSOutputStreams);
        return ps;
    }

    private void HookStreams(PSOutputStreams outputStreams)
    {
        _streams.Error = outputStreams.Error;
    }

    private static Task InvokePowerShellAsync(
        PowerShell powerShell,
        PSDataCollection<PSObject> output) =>
        Task.Factory.FromAsync(
            powerShell.BeginInvoke<PSObject, PSObject>(null, output),
            powerShell.EndInvoke);

    internal PSTask AddInputObject(object? inputObject)
    {
        _input ??= new Dictionary<string, object?>
        {
            { "Name", "_" },
        };

        _input["Value"] = inputObject;
        _powershell
            .AddCommand("Set-Variable", useLocalScope: true)
            .AddParameters(_input);
        return this;
    }

    internal PSTask AddScript(ScriptBlock script)
    {
        _powershell.AddScript(script.ToString(), useLocalScope: true);
        return this;
    }

    internal async Task<PSTask> InvokeAsync()
    {
        using CancellationTokenRegistration _ = _pool.RegisterCancellation(CancelCallback(this));
        await InvokePowerShellAsync(_powershell, OutputStreams.Success);
        return this;
    }

    private static Action CancelCallback(PSTask task) => delegate
    {
        task._powershell.BeginStop((e) =>
        {
            task._powershell.EndStop(e);
            task.Runspace.Dispose();
            task.Dispose();
        }, null);
    };

    public void Dispose()
    {
        _powershell.Dispose();
        GC.SuppressFinalize(this);
    }
}
