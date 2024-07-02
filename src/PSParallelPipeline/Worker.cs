using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation;

namespace PSParallelPipeline;

internal sealed class Worker : IDisposable
{
    private readonly BlockingCollection<PSTask> _inputQueue = [];

    internal BlockingCollection<PSOutputData> OutputPipe { get; } = [];

    internal CancellationToken Token { get => _cts.Token; }

    internal PSOutputStreams OutputStreams { get; }

    private readonly CancellationTokenSource _cts;

    private readonly RunspacePool _runspacePool;

    private Task? _worker;

    private readonly Dictionary<string, object?> _inputObject = new()
    {
        ["Name"] = "_"
    };

    internal Worker(PoolSettings settings)
    {
        _cts = new CancellationTokenSource();
        OutputStreams = new PSOutputStreams(this);
        _runspacePool = new RunspacePool(settings, this);
    }

    internal void Wait() => _worker?.GetAwaiter().GetResult();

    internal void Stop() => _cts.Cancel();

    internal void CancelAfter(TimeSpan span) => _cts.CancelAfter(span);

    internal void Enqueue(object? input, ScriptBlock script)
    {
        _inputObject["Value"] = input;
        _inputQueue.Add(
            item: PSTask
                .Create(_runspacePool)
                .AddInputObject(_inputObject)
                .AddScript(script),
            cancellationToken: Token);
    }

    internal bool TryTake(out PSOutputData output) =>
        OutputPipe.TryTake(out output, 0, Token);

    internal void CompleteInputAdding() => _inputQueue.CompleteAdding();

    internal IEnumerable<PSOutputData> GetConsumingEnumerable() =>
        OutputPipe.GetConsumingEnumerable(Token);

    internal void Start() => _worker = Task.Run(async () =>
    {
        while (!_inputQueue.IsCompleted)
        {
            if (_inputQueue.TryTake(out PSTask ps, 200, Token))
            {
                await _runspacePool.EnqueueAsync(ps);
            }
        }

        await _runspacePool.ProcessTasksAsync();
        OutputPipe.CompleteAdding();
    }, cancellationToken: Token);

    public void Dispose()
    {
        _inputQueue.Dispose();
        OutputStreams.Dispose();
        _runspacePool.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
