using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PSParallelPipeline;

internal sealed class Worker
{
    private Task? _worker;

    private readonly TaskSettings _taskSettings;

    private readonly BlockingCollection<object?> _input = [];

    private readonly BlockingCollection<PSOutputData> _output = [];

    private readonly RunspacePool _pool;

    private readonly CancellationToken _token;

    private readonly PSOutputStreams _streams;

    internal Worker(
        PoolSettings poolSettings,
        TaskSettings taskSettings,
        CancellationToken token)
    {
        _token = token;
        _taskSettings = taskSettings;
        _streams = new PSOutputStreams(_output);
        _pool = new RunspacePool(poolSettings, _streams, _token);
    }

    internal void WaitForCompletion() => _worker?.GetAwaiter().GetResult();

    internal void Enqueue(object? input) => _input.Add(input, _token);

    internal bool TryTake(out PSOutputData output) => _output.TryTake(out output, 0, _token);

    internal void CompleteInputAdding() => _input.CompleteAdding();

    internal IEnumerable<PSOutputData> GetConsumingEnumerable() => _output.GetConsumingEnumerable(_token);

    internal void Run() => _worker = Task.Run(Start, cancellationToken: _token);

    private async Task Start()
    {
        List<Task> tasks = new(_pool.MaxRunspaces);

        try
        {
            foreach (object? input in _input.GetConsumingEnumerable(_token))
            {
                if (tasks.Count == tasks.Capacity)
                {
                    Task task = await Task
                        .WhenAny(tasks)
                        .NoContext();

                    tasks.Remove(task);
                    await task.NoContext();
                }

                tasks.Add(PSTask
                    .Create(input, _pool, _taskSettings)
                    .InvokeAsync());
            }
        }
        catch (OperationCanceledException)
        { }
        finally
        {
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks).NoContext();
            }

            _output.CompleteAdding();
        }
    }

    public void Dispose()
    {
        _pool.Dispose();
        _input.Dispose();
        _streams.Dispose();
        _output.Dispose();
        GC.SuppressFinalize(this);
    }
}
