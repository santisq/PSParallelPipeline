using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal sealed class RunspacePool : IDisposable
{
    private CancellationToken Token { get => _worker.Token; }

    private InitialSessionState InitialSessionState { get => _settings.InitialSessionState; }

    private Dictionary<string, object?> UsingStatements { get => _settings.UsingStatements; }

    private int MaxRunspaces { get => _settings.MaxRunspaces; }

    private readonly ConcurrentQueue<Runspace> _pool;

    private readonly PoolSettings _settings;

    private readonly Worker _worker;

    private readonly List<Task> _tasks;

    private bool UseNewRunspace { get => _settings.UseNewRunspace; }

    internal PSOutputStreams PSOutputStreams { get => _worker.OutputStreams; }

    private readonly SemaphoreSlim _semaphore;

    internal RunspacePool(PoolSettings settings, Worker worker)
    {
        _settings = settings;
        _worker = worker;
        _pool = new ConcurrentQueue<Runspace>();
        _tasks = new List<Task>(MaxRunspaces);
        _semaphore = new SemaphoreSlim(MaxRunspaces, MaxRunspaces);
    }

    internal void Release() => _semaphore.Release();

    private Runspace CreateRunspace()
    {
        Runspace rs = RunspaceFactory.CreateRunspace(InitialSessionState);
        rs.Open();
        return rs;
    }

    internal void CompleteTask(PSTask psTask)
    {
        psTask.Dispose();

        if (UseNewRunspace)
        {
            psTask.Runspace.Dispose();
            return;
        }

        _pool.Enqueue(psTask.Runspace);
    }

    private async Task<Runspace> GetRunspaceAsync()
    {
        await _semaphore.WaitAsync(Token);
        if (_pool.TryDequeue(out Runspace runspace))
        {
            return runspace;
        }

        return CreateRunspace();
    }

    internal async Task EnqueueAsync(PSTask psTask)
    {
        psTask.AddUsingStatements(UsingStatements);
        psTask.Runspace = await GetRunspaceAsync();
        _tasks.Add(psTask.InvokeAsync());
    }

    internal async Task ProcessAllAsync()
    {
        while (_tasks.Count > 0)
        {
            await ProcessAnyAsync();
        }
    }

    private async Task ProcessAnyAsync()
    {
        Task task = await Task.WhenAny(_tasks);
        _tasks.Remove(task);
        await task;
    }

    internal CancellationTokenRegistration RegisterCancellation(Action callback) =>
        Token.Register(callback);

    internal async Task WaitOnCancelAsync() => await Task.WhenAll(_tasks);

    public void Dispose()
    {
        while (_pool.TryDequeue(out Runspace runspace))
        {
            runspace.Dispose();
        }

        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
