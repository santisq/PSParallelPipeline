using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal sealed class RunspacePool : IDisposable
{
    private CancellationToken Token { get; }

    private InitialSessionState InitialSessionState { get => _settings.InitialSessionState; }

    internal int MaxRunspaces { get => _settings.MaxRunspaces; }

    private readonly ConcurrentQueue<Runspace> _pool = [];

    private readonly PoolSettings _settings;

    // private readonly List<Task> _tasks;

    internal bool UseNewRunspace { get => _settings.UseNewRunspace; }

    internal PSOutputStreams Streams { get; }

    private readonly SemaphoreSlim _semaphore;

    internal RunspacePool(
        PoolSettings settings,
        PSOutputStreams streams,
        CancellationToken token)
    {
        Streams = streams;
        Token = token;
        _settings = settings;
        // _tasks = new List<Task>(MaxRunspaces);
        _semaphore = new SemaphoreSlim(MaxRunspaces, MaxRunspaces);
    }

    internal void Release() => _semaphore.Release();

    internal void PushRunspace(Runspace runspace) => _pool.Enqueue(runspace);

    // internal async Task EnqueueAsync(PSTask psTask)
    // {
    //     psTask.Runspace = await GetRunspaceAsync();
    //     _tasks.Add(psTask.InvokeAsync());
    // }

    // internal async Task ProcessAllAsync()
    // {
    //     while (_tasks.Count > 0)
    //     {
    //         await ProcessAnyAsync();
    //     }
    // }

    internal CancellationTokenRegistration RegisterCancellation(Action callback) =>
        Token.Register(callback);

    // internal void WaitOnCancel() => Task.WhenAll(_tasks).GetAwaiter().GetResult();

    // private async Task ProcessAnyAsync()
    // {
    //     Task task = await Task.WhenAny(_tasks);
    //     _tasks.Remove(task);
    //     await task;
    // }

    private Runspace CreateRunspace()
    {
        Runspace rs = RunspaceFactory.CreateRunspace(InitialSessionState);
        rs.Open();
        return rs;
    }

    internal async Task<Runspace> GetRunspaceAsync()
    {
        await _semaphore.WaitAsync(Token);
        if (_pool.TryDequeue(out Runspace runspace))
        {
            return runspace;
        }

        return CreateRunspace();
    }

    public void Dispose()
    {
        foreach (Runspace runspace in _pool)
        {
            runspace.Dispose();
        }

        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
