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

    private readonly SemaphoreSlim _semaphoreSlim;

    internal RunspacePool(PoolSettings settings, Worker worker)
    {
        _settings = settings;
        _worker = worker;
        _pool = new ConcurrentQueue<Runspace>();
        _tasks = new List<Task>(MaxRunspaces);
        _semaphoreSlim = new SemaphoreSlim(MaxRunspaces, MaxRunspaces);
    }

    internal void Release() => _semaphoreSlim.Release();

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
        await _semaphoreSlim.WaitAsync(Token);
        if (_pool.TryDequeue(out Runspace runspace))
        {
            return runspace;
        }
        return CreateRunspace();
    }

    internal async Task EnqueueAsync(PSTask psTask)
    {
        if (UsingStatements.Count > 0)
        {
            psTask.AddUsingStatements(UsingStatements);
        }

        psTask.Runspace = await GetRunspaceAsync();
        _tasks.Add(psTask.InvokeAsync());
    }

    internal async Task ProcessAllAsync()
    {
        while (_tasks.Count > 0)
        {
            Token.ThrowIfCancellationRequested();
            await ProcessAnyAsync();
        }
    }

    private async Task ProcessAnyAsync()
    {
        try
        {
            Task task = await Task.WhenAny(_tasks);
            _tasks.Remove(task);
            await task;
        }
        catch (Exception exception)
        {
            PSOutputStreams.AddOutput(exception.CreateProcessingTaskError(this));
        }
    }

    internal CancellationTokenRegistration RegisterCancellation(Action callback) =>
        Token.Register(callback);

    public void Dispose()
    {
        foreach (Runspace runspace in _pool)
        {
            runspace.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
