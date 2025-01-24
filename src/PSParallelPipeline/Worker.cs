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
        List<Task> tasks = new(_pool.MaxRunspaces);

        try
        {
            while (!_input.IsCompleted)
            {
                if (tasks.Count == tasks.Capacity)
                {
                    await ProcessAnyAsync(tasks);
                }

                if (_input.TryTake(out PSTask ps, 0, Token))
                {
                    ps.Runspace = await _pool.GetRunspaceAsync();
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

    private async Task ProcessAnyAsync(List<Task> tasks)
    {
        Task task = await Task.WhenAny(tasks);
        tasks.Remove(task);
        await task;
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
