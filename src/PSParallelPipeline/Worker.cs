using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PSParallelPipeline;

internal sealed class Worker
{
    private readonly Task _worker;

    private readonly PoolSettings _poolSettings;

    private readonly TaskSettings _taskSettings;

    private readonly BlockingCollection<object?> _input = [];

    private readonly BlockingCollection<PSOutputData> _output = [];

    private readonly CancellationToken _token;

    internal Worker(
        PoolSettings poolSettings,
        TaskSettings taskSettings,
        CancellationToken token)
    {
        _token = token;
        _poolSettings = poolSettings;
        _taskSettings = taskSettings;
        _worker = Task.Run(Start, cancellationToken: _token);
    }

    internal void WaitForCompletion() => _worker.GetAwaiter().GetResult();

    internal void Enqueue(object? input) => _input.Add(input, _token);

    internal bool TryTake(out PSOutputData output) => _output.TryTake(out output, 0, _token);

    internal void CompleteInputAdding() => _input.CompleteAdding();

    internal IEnumerable<PSOutputData> GetConsumingEnumerable() => _output.GetConsumingEnumerable(_token);

    private async Task Start()
    {
        int max = _poolSettings.MaxRunspaces;
        using PSOutputStreams streams = new(_output);
        using RunspacePool pool = new(_poolSettings, streams, _token);
        List<Task> tasks = new(max);

        try
        {
            Task task;
            foreach (object? input in _input.GetConsumingEnumerable(_token))
            {
                if (tasks.Count == max)
                {
                    task = await Task.WhenAny(tasks).NoContext();
                    tasks.Remove(task);
                    await task.NoContext();
                }

                tasks.Add(pool.InvokePowerShellAsync(input, _taskSettings));
            }
        }
        catch (OperationCanceledException)
        { }
        finally
        {
            if (tasks.Count > 0)
                await Task.WhenAll(tasks).NoContext();

            _output.CompleteAdding();
        }
    }

    public void Dispose()
    {
        _input.Dispose();
        _output.Dispose();
        GC.SuppressFinalize(this);
    }
}
