using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

internal sealed class PSTask
{
    private readonly PowerShell _powershell;

    private readonly PSDataStreams _internalStreams;

    private readonly RunspacePool _pool;

    private PSOutputStreams OutputStreams { get => _pool.Streams; }

    private Runspace Runspace
    {
        get => _powershell.Runspace;
        set => _powershell.Runspace = value;
    }

    private PSTask(RunspacePool pool)
    {
        _powershell = PowerShell.Create();
        _internalStreams = _powershell.Streams;
        _pool = pool;
    }

    static internal PSTask Create(RunspacePool runspacePool)
    {
        PSTask ps = new(runspacePool);
        HookStreams(ps._internalStreams, runspacePool.Streams);
        return ps;
    }

    private static void HookStreams(
        PSDataStreams streams,
        PSOutputStreams outputStreams)
    {
        streams.Error = outputStreams.Error;
        streams.Debug = outputStreams.Debug;
        streams.Information = outputStreams.Information;
        streams.Progress = outputStreams.Progress;
        streams.Verbose = outputStreams.Verbose;
        streams.Warning = outputStreams.Warning;
    }

    private static Task InvokePowerShellAsync(
        PowerShell powerShell,
        PSDataCollection<PSObject> output) =>
        Task.Factory.FromAsync(
            powerShell.BeginInvoke<PSObject, PSObject>(null, output),
            powerShell.EndInvoke);

    internal PSTask AddInput(object? inputObject)
    {
        if (inputObject is not null)
        {
            _powershell
                .AddCommand("Set-Variable", useLocalScope: true)
                .AddArgument("_")
                .AddArgument(inputObject);
        }

        return this;
    }

    internal PSTask AddScript(ScriptBlock script)
    {
        _powershell.AddScript(script.ToString(), useLocalScope: true);
        return this;
    }

    internal PSTask AddUsingStatements(Dictionary<string, object?> usingParams)
    {
        if (usingParams.Count > 0)
        {
            _powershell.AddParameters(new Dictionary<string, Dictionary<string, object?>>
            {
                ["--%"] = usingParams
            });
        }

        return this;
    }

    internal async Task InvokeAsync()
    {
        try
        {
            using CancellationTokenRegistration _ = _pool.RegisterCancellation(_powershell.Dispose);
            Runspace = await _pool.GetRunspaceAsync();
            await InvokePowerShellAsync(_powershell, OutputStreams.Success);
        }
        catch (Exception exception)
        {
            OutputStreams.AddOutput(exception.CreateProcessingTaskError(this));
        }
        finally
        {
            _powershell.Dispose();
            _pool.PushRunspace(Runspace);
        }
    }
}
