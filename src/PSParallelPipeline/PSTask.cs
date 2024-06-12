using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

internal sealed class PSTask : IDisposable
{
    internal PowerShell _powershell;

    private readonly PSDataStreams _streams;

    internal PSTask(
        Runspace runspace,
        PSOutputStreams outputStreams)
    {
        _powershell = PowerShell.Create();
        _powershell.Runspace = runspace;
        _streams = _powershell.Streams;
        HookStreams(outputStreams);
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

    internal async Task InvokeAsync(
        PSOutputStreams outputStreams,
        CancellationToken cancellationToken,
        ScriptBlock script,
        Dictionary<string, object> parameters)
    {
        _powershell
            .AddCommand("Set-Variable", useLocalScope: true)
            .AddParameters(parameters)
            .AddScript(script.ToString(), useLocalScope: true);

        using CancellationTokenRegistration _ = cancellationToken.Register(CancelCallback(this));
        await InvokePowerShellAsync(_powershell, outputStreams.Success);
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
