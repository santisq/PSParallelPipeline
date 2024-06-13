using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Management.Automation;

namespace PSParallelPipeline;

internal sealed class PSTask : IDisposable
{
    private readonly PowerShell _powershell;

    private readonly PSDataStreams _streams;

    private PSOutputStreams _outputStreams;

    [ThreadStatic]
    private static Dictionary<string, object?>? _input;

    private PSTask(PSOutputStreams outputStreams)
    {
        _powershell = PowerShell.Create();
        _streams = _powershell.Streams;
        _outputStreams = outputStreams;
    }

    static internal PSTask Create(PSOutputStreams outputStreams)
    {
        PSTask task = new(outputStreams);
        task.HookStreams(outputStreams);
        return task;
    }

    private void HookStreams(PSOutputStreams outputStreams)
    {
        _outputStreams = outputStreams;
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


    internal async Task InvokeAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenRegistration _ = cancellationToken.Register(CancelCallback(this));
        await InvokePowerShellAsync(_powershell, _outputStreams.Success);
        Dispose();
    }

    private static Action CancelCallback(PSTask task) => delegate
    {
        task._powershell.BeginStop(task._powershell.EndStop, null);
    };

    public void Dispose()
    {
        _powershell.Dispose();
        GC.SuppressFinalize(this);
    }
}
