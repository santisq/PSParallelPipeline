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

    private readonly CancellationToken _token;

    private readonly Dictionary<string, object?> _usingParams;

    private readonly PSOutputStreams _streams;

    internal Worker(
        PoolSettings poolSettings,
        WorkerSettings workerSettings)
    {
        (_usingParams, _token) = workerSettings;
        _streams = new PSOutputStreams(_output);
        _pool = new RunspacePool(poolSettings, _streams, _token);
    }

    internal void Wait() => _worker?.GetAwaiter().GetResult();

    internal void Enqueue(object? input, ScriptBlock script)
    {
        _input.Add(
            item: PSTask
                .Create(_pool)
                .AddInput(input)
                .AddScript(script)
                .AddUsingStatements(_usingParams),
            cancellationToken: _token);
    }

    internal bool TryTake(out PSOutputData output) =>
        _output.TryTake(out output, 0, _token);

    internal void CompleteInputAdding() => _input.CompleteAdding();

    internal IEnumerable<PSOutputData> GetConsumingEnumerable() =>
        _output.GetConsumingEnumerable(_token);

    internal void Run() => _worker = Task.Run(Start, cancellationToken: _token);

    private async Task Start()
    {
        List<Task> tasks = new(_pool.MaxRunspaces);

        try
        {
            while (!_input.IsCompleted)
            {
                if (tasks.Count == tasks.Capacity)
                {
                    await ProcessAnyAsync(tasks);
                }

                if (_input.TryTake(out PSTask ps, 0, _token))
                {
                    tasks.Add(ps.InvokeAsync());
                }
            }

            while (tasks.Count > 0)
            {
                await ProcessAnyAsync(tasks);
            }
        }
        catch
        {
            await Task.WhenAll(tasks);
        }
        finally
        {
            _output.CompleteAdding();
        }
    }

    private static async Task ProcessAnyAsync(List<Task> tasks)
    {
        Task task = await Task.WhenAny(tasks);
        tasks.Remove(task);
        await task;
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
