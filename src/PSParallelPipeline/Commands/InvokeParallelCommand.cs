using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

[Cmdlet(VerbsLifecycle.Invoke, "Parallel")]
public sealed class InvokeParallelCommand : PSCmdlet, IDisposable
{
    [Parameter(Position = 0, Mandatory = true)]
    public ScriptBlock ScriptBlock { get; set; } = null!;

    [Parameter(ValueFromPipeline = true)]
    public object? InputObject { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int ThrottleLimit { get; set; } = 5;

    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int TimeOutSeconds { get; set; }

    [Parameter]
    public SwitchParameter UseNewRunspace { get; set; }

    private Worker? _worker;

    protected override void BeginProcessing()
    {
        PoolSettings poolSettings = new()
        {
            MaxRunspaces = ThrottleLimit,
            UseNewRunspace = UseNewRunspace,
            InitialSessionState = InitialSessionState.CreateDefault2(),
            UsingStatements = ScriptBlock.GetUsingParameters(this)
        };

        _worker = new Worker(poolSettings);

        if (TimeOutSeconds > 0)
        {
            _worker.CancelAfter(TimeSpan.FromSeconds(TimeOutSeconds));
        }

        _worker.Start();
    }

    protected override void ProcessRecord()
    {
        if (_worker is null)
        {
            return;
        }

        this.ThrowIfInputObjectIsScriptblock(InputObject);

        try
        {
            _worker.Enqueue(InputObject, ScriptBlock);
            while (_worker.TryTake(out PSOutputData data))
            {
                ProcessOutput(data);
            }
        }
        catch (Exception _) when (_ is PipelineStoppedException or FlowControlException)
        {
            _worker.Stop();
            throw;
        }
        catch (OperationCanceledException exception)
        {
            exception.WriteTimeoutError(this);
        }
        catch (Exception exception)
        {
            exception.WriteProcessOutputError(this);
        }
    }

    protected override void EndProcessing()
    {
        if (_worker is null)
        {
            return;
        }

        try
        {
            _worker.CompleteInputAdding();
            foreach (PSOutputData data in _worker.GetConsumingEnumerable())
            {
                ProcessOutput(data);
            }

            _worker.Wait();
        }
        catch (Exception _) when (_ is PipelineStoppedException or FlowControlException)
        {
            _worker.Stop();
            throw;
        }
        catch (OperationCanceledException exception)
        {
            exception.WriteTimeoutError(this);
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

    protected override void StopProcessing() => _worker?.Stop();

    public void Dispose()
    {
        _worker?.Dispose();
        GC.SuppressFinalize(this);
    }
}
