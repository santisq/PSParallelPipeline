using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Management.Automation;
using System.Collections.Concurrent;
using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

[Cmdlet(VerbsLifecycle.Invoke, "Parallel")]
public sealed class InvokeParallelCommand : PSCmdlet, IDisposable
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

    [Parameter]
    public SwitchParameter UseNewRunspace { get; set; }

    private readonly BlockingCollection<PSOutputData> _outputPipe = new();

    private readonly CancellationTokenSource _cts = new();

    private readonly BlockingCollection<PSTask> _taskQueue = new();

    private PSOutputStreams? _outputStreams;

    private Task? _worker;

    private RunspacePool? _rspool;

    protected override void BeginProcessing()
    {
        if (TimeOutSeconds > 0)
        {
            _cts.CancelAfter(TimeSpan.FromSeconds(TimeOutSeconds));
        }

        _outputStreams = new PSOutputStreams(_outputPipe);

        _worker = Task.Run(async () =>
        {
            InitialSessionState iss = InitialSessionState.CreateDefault2();

            _rspool = new(
                maxRunspaces: ThrottleLimit,
                useNewRunspace: UseNewRunspace,
                initialSessionState: iss,
                outputPipe: _outputPipe);

            while (!_taskQueue.IsCompleted)
            {
                if (!_taskQueue.TryTake(out PSTask ps, 50, _cts.Token))
                {
                    continue;
                }

                Runspace rs = await _rspool.GetRunspaceAsync();
                ps.SetRunspace(rs);
                _rspool.Enqueue(ps.InvokeAsync(_cts.Token));
            }

            await _rspool.ProcessTasksAsync();
            _outputPipe.CompleteAdding();
        });
    }

    protected override void ProcessRecord()
    {
        if (_outputStreams is null)
        {
            return;
        }

        PSTask _task = PSTask
            .Create(_outputStreams)
            .AddInputObject(InputObject)
            .AddScript(ScriptBlock);

        _taskQueue.Add(_task);

        try
        {
            while (_outputPipe.TryTake(out PSOutputData data, 0, _cts.Token))
            {
                ProcessOutput(data);
            }
        }
        catch (OperationCanceledException exception)
        {
            exception.WriteTimeoutError(this);
        }
        catch (Exception _) when (_ is PipelineStoppedException or FlowControlException)
        {
            throw;
        }
        catch (Exception exception)
        {
            exception.WriteProcessOutputError(this);
        }
    }

    protected override void EndProcessing()
    {
        _taskQueue.CompleteAdding();

        try
        {
            foreach (PSOutputData data in _outputPipe.GetConsumingEnumerable(_cts.Token))
            {
                ProcessOutput(data);
            }

            _worker?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException exception)
        {
            exception.WriteTimeoutError(this);
        }
        catch (Exception _) when (_ is PipelineStoppedException or FlowControlException)
        {
            throw;
        }
        catch (Exception exception)
        {
            exception.WriteEndProcessingError(this);
        }
    }

    private void ProcessOutput(PSOutputData data)
    {
        switch (data.Type)
        {
            case Type.Success:
                WriteObject(data.Output);
                break;

            case Type.Error:
                WriteError((ErrorRecord)data.Output);
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
        _rspool?.Dispose();
    }
}
