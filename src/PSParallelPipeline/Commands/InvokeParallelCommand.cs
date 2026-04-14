using System;
using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;

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
    [ValidateNotNullOrEmpty]
    [ArgumentCompleter(typeof(ModuleCompleter))]
    [Alias("mn")]
    public string[]? ModuleNames { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    [Alias("mp")]
    public string[]? ModulePaths { get; set; }

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
            .AddVariables(Variables, this)
            .ImportModules(ModuleNames)
            .ImportModulesFromPath(ModulePaths, this);

        PoolSettings poolSettings = new(
            ThrottleLimit, UseNewRunspace, iss);

        TaskSettings workerSettings = new(
            ScriptBlock.ToString(),
            ScriptBlock.GetUsingParameters(this));

        _worker = new Worker(poolSettings, workerSettings, _cts.Token);
    }

    protected override void ProcessRecord()
    {
        if (_worker is null)
        {
            return;
        }

        InputObject.ThrowIfInputObjectIsScriptBlock(this);

        try
        {
            _worker.Enqueue(InputObject);
            while (_worker.TryTake(out PSOutputData data))
            {
                data.WriteToPipeline(this);
            }
        }
        catch (Exception _) when (_ is PipelineStoppedException or FlowControlException)
        {
            CancelAndWait();
            throw;
        }
        catch (OperationCanceledException exception)
        {
            _worker.WaitForCompletion();
            exception.WriteTimeoutError(this);
        }
    }

    protected override void EndProcessing()
    {
        if (_worker is null) return;

        try
        {
            _worker.CompleteInputAdding();
            foreach (PSOutputData data in _worker.GetConsumingEnumerable())
            {
                data.WriteToPipeline(this);
            }

            _worker.WaitForCompletion();
        }
        catch (Exception _) when (_ is PipelineStoppedException or FlowControlException)
        {
            CancelAndWait();
            throw;
        }
        catch (OperationCanceledException exception)
        {
            _worker.WaitForCompletion();
            exception.WriteTimeoutError(this);
        }
    }

    private void CancelAndWait()
    {
        _cts.Cancel();
        _worker?.WaitForCompletion();
    }

    protected override void StopProcessing() => CancelAndWait();

    public void Dispose()
    {
        _worker?.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
