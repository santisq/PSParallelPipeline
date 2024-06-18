using System;
using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

[Cmdlet(VerbsLifecycle.Invoke, "Parallel")]
[Alias("parallel")]
public sealed class InvokeParallelCommand : PSCmdlet, IDisposable
{
    [Parameter(Position = 0, Mandatory = true)]
    public ScriptBlock ScriptBlock { get; set; } = null!;

    [Parameter(ValueFromPipeline = true)]
    public object? InputObject { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    [Alias("tl")]
    public int ThrottleLimit { get; set; } = 5;

    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    [Alias("to")]
    public int TimeOutSeconds { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    [Alias("vars")]
    public Hashtable? Variables { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    [ArgumentCompleter(typeof(CommandCompleter))]
    [Alias("funcs")]
    public string[]? Functions { get; set; }

    [Parameter]
    [Alias("unr")]
    public SwitchParameter UseNewRunspace { get; set; }

    private Worker? _worker;

    protected override void BeginProcessing()
    {
        InitialSessionState iss = InitialSessionState.CreateDefault2();

        if (Functions is not null)
        {
            iss.AddFunctions(Functions, this);
        }

        if (Variables is not null)
        {
            iss.AddVariables(Variables, this);
        }

        PoolSettings poolSettings = new()
        {
            MaxRunspaces = ThrottleLimit,
            UseNewRunspace = UseNewRunspace,
            InitialSessionState = iss,
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

        this.ThrowIfInputObjectIsScriptBlock(InputObject);

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

            case Type.Debug:
                WriteDebug((string)data.Output);
                break;

            case Type.Information:
                WriteInformation((InformationRecord)data.Output);
                break;

            case Type.Progress:
                WriteProgress((ProgressRecord)data.Output);
                break;

            case Type.Verbose:
                WriteVerbose((string)data.Output);
                break;

            case Type.Warning:
                WriteWarning((string)data.Output);
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
