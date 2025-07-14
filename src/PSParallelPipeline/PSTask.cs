using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

internal sealed class PSTask
{
    private const string SetVariableCommand = "Set-Variable";

    private const string DollarUnderbar = "_";

    private const string StopParsingOp = "--%";

    private bool _canceled;

    private readonly PowerShell _powershell;

    private readonly PSDataStreams _internalStreams;

    private Runspace? _runspace;

    private readonly PSOutputStreams _outputStreams;

    private readonly CancellationToken _token;

    private readonly RunspacePool _pool;

    private PSTask(RunspacePool pool)
    {
        _powershell = PowerShell.Create();
        _internalStreams = _powershell.Streams;
        _outputStreams = pool.Streams;
        _token = pool.Token;
        _pool = pool;
    }

    internal static PSTask Create(
        object? input,
        RunspacePool runspacePool,
        TaskSettings settings)
    {
        PSTask ps = new(runspacePool);
        SetStreams(ps._internalStreams, runspacePool.Streams);

        return ps
            .AddInput(input)
            .AddScript(settings.Script)
            .AddUsingStatements(settings.UsingStatements);
    }

    internal async Task InvokeAsync()
    {
        try
        {
            using CancellationTokenRegistration _ = _token.Register(Cancel);
            _runspace = await _pool.GetRunspaceAsync().NoContext();
            _powershell.Runspace = _runspace;
            await _powershell.InvokePowerShellAsync(_outputStreams.Success).NoContext();
        }
        catch (Exception exception)
        {
            _outputStreams.AddOutput(exception.CreateProcessingTaskError(this));
        }
        finally
        {
            CompleteTask();
        }
    }

    private static void SetStreams(
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

    private PSTask AddInput(object? inputObject)
    {
        if (inputObject is not null)
        {
            _powershell
                .AddCommand(SetVariableCommand, useLocalScope: true)
                .AddArgument(DollarUnderbar)
                .AddArgument(inputObject);
        }

        return this;
    }

    private PSTask AddScript(string script)
    {
        _powershell.AddScript(script, useLocalScope: true);
        return this;
    }

    private PSTask AddUsingStatements(Dictionary<string, object?> usingParams)
    {
        if (usingParams.Count > 0)
        {
            _powershell.AddParameter(StopParsingOp, usingParams);
        }

        return this;
    }

    private void CompleteTask()
    {
        if (_canceled)
        {
            _runspace?.Dispose();
            return;
        }

        _powershell.Dispose();
        if (_runspace is not null)
        {
            _pool.PushRunspace(_runspace);
        }
    }

    internal void Cancel()
    {
        _powershell.Dispose();
        _canceled = true;
    }
}
