using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Management.Automation;
using System.Collections.Concurrent;
using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

[Cmdlet(VerbsLifecycle.Invoke, "Parallel")]
public sealed class TestCommand : PSCmdlet, IDisposable
{
    [Parameter(Position = 0)]
    public ScriptBlock ScriptBlock { get; set; } = null!;

    [Parameter(ValueFromPipeline = true)]
    public object? InputObject { get; set; } = null!;

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int ThrottleLimit { get; set; } = 5;

    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int TimeOutSeconds { get; set; }

    private readonly BlockingCollection<PSOutputData> _outputPipe = new();

    private readonly CancellationTokenSource _cts = new();

    private readonly BlockingCollection<PSTask> _taskQueue = new();

    private PSOutputStreams? _outputStreams;

    private Task? _worker;

    protected override void BeginProcessing()
    {
        if (TimeOutSeconds > 0)
        {
            _cts.CancelAfter(TimeSpan.FromSeconds(TimeOutSeconds));
        }

        _outputStreams = new PSOutputStreams(_outputPipe);

        _worker = Task.Run(async () =>
        {
            List<Task> tasks = new();

            while (!_taskQueue.IsCompleted)
            {
                if (!_taskQueue.TryTake(out PSTask ps, 50, _cts.Token))
                {
                    continue;
                }

                if (tasks.Count == ThrottleLimit)
                {
                    await ProcessTask(tasks);
                }

                tasks.Add(ps.InvokeAsync(_cts.Token));
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
        if (_outputStreams is null)
        {
            return;
        }

        Runspace runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();

        PSTask _task = PSTask
            .Create(runspace, _outputStreams)
            .AddInputObject(InputObject)
            .AddScript(ScriptBlock);

        _taskQueue.Add(_task);

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

    public void Dispose()
    {
        _outputPipe.Dispose();
        _taskQueue.Dispose();
        _cts.Dispose();
        _outputStreams?.Dispose();
    }
}
