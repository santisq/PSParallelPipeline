using System;
using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using PSParallelPipeline.Poly;

namespace PSParallelPipeline.Commands;

[Cmdlet(VerbsLifecycle.Invoke, "Parallel")]
[Alias("parallel", "asparallel")]
[OutputType(typeof(object))]
public sealed class InvokeParallelCommand : PSCmdlet, IDisposable
{
    private Worker? _worker;

    private readonly CancellationTokenSource _cts = new();

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
    public int TimeoutSeconds { get; set; }

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

    protected override void BeginProcessing()
    {
        if (TimeoutSeconds > 0)
        {
            _cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        }

        InitialSessionState iss = InitialSessionState
            .CreateDefault2()
            .AddFunctions(Functions, this)
            .AddVariables(Variables, this);

        PoolSettings poolSettings = new()
        {
            MaxRunspaces = ThrottleLimit,
            UseNewRunspace = UseNewRunspace,
            InitialSessionState = iss
        };

        TaskSettings workerSettings = new()
        {
            Script = ScriptBlock.ToString(),
            UsingStatements = ScriptBlock.GetUsingParameters(this)
        };

        _worker = new Worker(poolSettings, workerSettings, _cts.Token);
        _worker.Run();
    }

    protected override void ProcessRecord()
    {
        Dbg.Assert(_worker is not null);
        this.ThrowIfInputObjectIsScriptBlock(InputObject);

        try
        {
            _worker.Enqueue(InputObject);
            while (_worker.TryTake(out PSOutputData data))
            {
                ProcessOutput(data);
            }
        }
        catch (Exception _) when (_ is PipelineStoppedException or FlowControlException)
        {
            CancelAndWait();
            throw;
        }
        catch (OperationCanceledException exception)
        {
            _worker.Wait();
            exception.WriteTimeoutError(this);
        }
    }

    protected override void EndProcessing()
    {
        Dbg.Assert(_worker is not null);

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
            CancelAndWait();
            throw;
        }
        catch (OperationCanceledException exception)
        {
            _worker.Wait();
            exception.WriteTimeoutError(this);
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
                DebugRecord debug = (DebugRecord)data.Output;
                WriteDebug(debug.Message);
                break;

            case Type.Information:
                WriteInformation((InformationRecord)data.Output);
                break;

            case Type.Progress:
                WriteProgress((ProgressRecord)data.Output);
                break;

            case Type.Verbose:
                VerboseRecord verbose = (VerboseRecord)data.Output;
                WriteVerbose(verbose.Message);
                break;

            case Type.Warning:
                WarningRecord warning = (WarningRecord)data.Output;
                WriteWarning(warning.Message);
                break;
        }
    }

    private void CancelAndWait()
    {
        _cts.Cancel();
        _worker?.Wait();
    }

    protected override void StopProcessing() => CancelAndWait();

    public void Dispose()
    {
        _worker?.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
