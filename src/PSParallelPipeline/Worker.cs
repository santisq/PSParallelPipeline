using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation;

namespace PSParallelPipeline;

internal sealed class Worker
{
    private Task? _worker;

    private readonly BlockingCollection<PSTask> _input = [];

    private readonly BlockingCollection<PSOutputData> _output = [];

    private readonly RunspacePool _pool;

    private readonly WorkerSettings _settings;

    private readonly Dictionary<string, object?> _inputObject = new()
    {
        ["Name"] = "_"
    };

    internal CancellationToken Token { get => _settings.Token; }

    internal PSOutputStreams Streams { get; }

    internal Worker(
        PoolSettings poolSettings,
        WorkerSettings workerSettings)
    {
        Streams = new PSOutputStreams(_output);
        _settings = workerSettings;
        _pool = new RunspacePool(poolSettings, Streams, Token);
    }

    internal void Wait() => _worker?.GetAwaiter().GetResult();

    internal void Enqueue(object? input, ScriptBlock script)
    {
        _inputObject["Value"] = input;
        _input.Add(
            item: PSTask
                .Create(_pool)
                .AddInputObject(_inputObject)
                .AddScript(script)
                .AddUsingStatements(_settings.UsingStatements),
            cancellationToken: Token);
    }

    internal bool TryTake(out PSOutputData output) =>
        _output.TryTake(out output, 0, Token);

    internal void CompleteInputAdding() => _input.CompleteAdding();

    internal IEnumerable<PSOutputData> GetConsumingEnumerable() =>
        _output.GetConsumingEnumerable(Token);

    internal void Run() => _worker = Task.Run(Start, cancellationToken: Token);

    private async Task Start()
    {
        try
        {
            while (!_input.IsCompleted)
            {
                if (_input.TryTake(out PSTask ps, 0, Token))
                {
                    await _pool.EnqueueAsync(ps);
                }
            }

            await _pool.ProcessAllAsync();
            _output.CompleteAdding();
        }
        catch
        {
            _pool.WaitOnCancel();
        }
    }

    public void Dispose()
    {
        _pool.Dispose();
        _input.Dispose();
        Streams.Dispose();
        _output.Dispose();
        GC.SuppressFinalize(this);
    }
}
