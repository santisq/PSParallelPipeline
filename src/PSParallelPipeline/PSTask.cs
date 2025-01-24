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

    static internal async Task<PSTask> CreateAsync(
        object? input,
        RunspacePool runspacePool,
        TaskSettings settings)
    {
        PSTask ps = new(runspacePool);
        HookStreams(ps._internalStreams, runspacePool.Streams);
        ps.Runspace = await runspacePool.GetRunspaceAsync();
        ps.AddInput(input);
        ps.AddScript(settings.Script);
        ps.AddUsingStatements(settings.UsingStatements);
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

    private void AddInput(object? inputObject)
    {
        if (inputObject is not null)
        {
            _powershell
                .AddCommand("Set-Variable", useLocalScope: true)
                .AddArgument("_")
                .AddArgument(inputObject);
        }
    }

    private void AddScript(string script) =>
        _powershell.AddScript(script, useLocalScope: true);

    private void AddUsingStatements(Dictionary<string, object?> usingParams)
    {
        if (usingParams.Count > 0)
        {
            _powershell.AddParameter("--%", usingParams);
        }
    }

    internal async Task InvokeAsync()
    {
        try
        {
            using CancellationTokenRegistration _ = _pool.RegisterCancellation(_powershell.Stop);
            await InvokePowerShellAsync(_powershell, OutputStreams.Success);
        }
        catch (PipelineStoppedException)
        { }
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
