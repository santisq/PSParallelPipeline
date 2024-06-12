using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Management.Automation;
using System.Collections.Concurrent;

namespace PSParallelPipeline;

[Cmdlet(VerbsLifecycle.Invoke, "Parallel")]
public sealed class TestCommand : PSCmdlet, IDisposable
{
    [Parameter(Position = 0)]
    public ScriptBlock ScriptBlock { get; set; } = null!;

    [Parameter(ValueFromPipeline = true)]
    public object InputObject { get; set; } = null!;

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int ThrottleLimit { get; set; } = 5;

    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int TimeOutSeconds { get; set; }

    private readonly BlockingCollection<PSOutputData> _outputPipe = new();

    private readonly CancellationTokenSource _cts = new();

    private readonly PSDataCollection<PSObject> _taskOutput = new();

    private readonly BlockingCollection<PowerShell> _taskQueue = new();

    private Task? _worker;

    private Dictionary<string, object> _params = new()
    {
        { "Name", "_" },
    };

    protected override void BeginProcessing()
    {
        if (TimeOutSeconds > 0)
        {
            _cts.CancelAfter(TimeSpan.FromSeconds(TimeOutSeconds));
        }

        _worker = Task.Run(async () =>
        {
            List<Task> tasks = new();

            while (!_taskQueue.IsCompleted)
            {
                if (!_taskQueue.TryTake(out PowerShell ps, 50, _cts.Token))
                {
                    continue;
                }

                if (tasks.Count == ThrottleLimit)
                {
                    await ProcessTask(tasks);
                }

                tasks.Add(InvokeAsync(ps, _taskOutput, _cts.Token));
            }

            while (tasks is { Count: > 0 })
            {
                await ProcessTask(tasks);
            }

            _outputPipe.CompleteAdding();
        });
    }

    private async Task ProcessTask(List<Task> tasks)
    {
        try
        {
            Task task = await Task.WhenAny(tasks);
            tasks.Remove(task);
            await task;
        }
        catch (Exception exception) when (exception is TaskCanceledException or OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _outputPipe.Add(new PSOutputData
            {
                Type = Type.Error,
                Output = new ErrorRecord(
                    exception,
                    "ProcessingTask",
                    ErrorCategory.NotSpecified,
                    this)
            });
        }
    }

    protected override void ProcessRecord()
    {
        _params["Value"] = InputObject;

        PowerShell ps = PowerShell
            .Create(RunspaceMode.NewRunspace)
            .AddCommand("Set-Variable", useLocalScope: true)
            .AddParameters(new Dictionary<string, object>
            {
                { "Name", "_" },
                { "Value", InputObject }
            })
            .AddScript(ScriptBlock.ToString(), useLocalScope: true);

        _taskOutput.DataAdded += (s, e) =>
        {
            foreach (var data in _taskOutput.ReadAll())
            {
                _outputPipe.Add(new PSOutputData { Type = Type.Success, Output = data });
            }
        };

        PSDataStreams streams = ps.Streams;
        streams.Error.DataAdded += (s, e) =>
        {
            foreach (ErrorRecord error in streams.Error.ReadAll())
            {
                _outputPipe.Add(new PSOutputData { Type = Type.Error, Output = error });
            }
        };

        _taskQueue.Add(ps);

        try
        {
            while (_outputPipe.TryTake(out PSOutputData data, 0, _cts.Token))
            {
                ProcessOutput(data.Type, data.Output);
            }
        }
        catch (OperationCanceledException)
        {
            WriteError(new ErrorRecord(
                new TimeoutException("Timeout has been reached."),
                "TimeOutReached",
                ErrorCategory.OperationTimeout,
                this));
        }
        catch (Exception exception)
        {
            WriteError(new ErrorRecord(
                exception,
                "ProcessingOutput",
                ErrorCategory.NotSpecified,
                this));
        }
    }

    protected override void EndProcessing()
    {
        _taskQueue.CompleteAdding();

        try
        {
            foreach ((Type type, object i) in _outputPipe.GetConsumingEnumerable(_cts.Token))
            {
                ProcessOutput(type, i);
            }

            _worker?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            WriteError(new ErrorRecord(
                new TimeoutException("Timeout has been reached."),
                "TimeOutReached",
                ErrorCategory.OperationTimeout,
                this));
        }
        catch (Exception exception)
        {
            WriteError(new ErrorRecord(
                exception,
                "EndProcessingOutput",
                ErrorCategory.NotSpecified,
                this));
        }
    }

    private void ProcessOutput(Type type, object i)
    {
        switch (type)
        {
            case Type.Success:
                WriteObject(i);
                break;

            case Type.Error:
                WriteError((ErrorRecord)i);
                break;
        }
    }

    protected override void StopProcessing() => _cts.Cancel();

    private Task InvokePowerShellAsync(
        PowerShell powerShell,
        PSDataCollection<PSObject> output) =>
        Task.Factory.FromAsync(
            powerShell.BeginInvoke<PSObject, PSObject>(null, output),
            powerShell.EndInvoke);

    private async Task InvokeAsync(
        PowerShell powerShell,
        PSDataCollection<PSObject> output,
        CancellationToken cancellationToken)
    {
        using CancellationTokenRegistration _ = cancellationToken.Register(() =>
            powerShell.BeginStop(powerShell.EndStop, null));
        await InvokePowerShellAsync(powerShell, output);
        powerShell.Dispose();
    }

    public void Dispose()
    {
        _outputPipe.Dispose();
        _taskQueue.Dispose();
        _cts.Dispose();
        _worker?.Dispose();
        _taskOutput.Dispose();
    }
}
