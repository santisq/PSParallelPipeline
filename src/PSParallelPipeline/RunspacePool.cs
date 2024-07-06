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

    private readonly List<Task<PSTask>> _tasks;

    private bool UseNewRunspace { get => _settings.UseNewRunspace; }

    internal PSOutputStreams PSOutputStreams { get => _worker.OutputStreams; }

    internal RunspacePool(PoolSettings settings, Worker worker)
    {
        _settings = settings;
        _worker = worker;
        _pool = new ConcurrentQueue<Runspace>();
        _tasks = new List<Task<PSTask>>(MaxRunspaces);
    }

    private Runspace CreateRunspace()
    {
        Runspace rs = RunspaceFactory.CreateRunspace(InitialSessionState);
        rs.Open();
        return rs;
    }

    internal void PushRunspace(Runspace runspace)
    {
        if (UseNewRunspace)
        {
            runspace.Dispose();
            return;
        }

        _pool.Enqueue(runspace);
    }

    private Runspace GetRunspace()
    {
        if (_pool.TryDequeue(out Runspace runspace))
        {
            return runspace;
        }

        // if (_tasks.Count == MaxRunspaces)
        // {
        //     await ProcessTaskAsync();
        //     return _pool.Dequeue();
        // }

        return CreateRunspace();
    }

    internal async Task ProcessTasksAsync()
    {
        while (_tasks.Count > 0)
        {
            await ProcessTaskAsync();
        }
    }

    internal async Task EnqueueAsync(PSTask psTask)
    {
        if (_tasks.Count == MaxRunspaces)
        {
            await ProcessTaskAsync();
        }

        if (UsingStatements is { Count: > 0 })
        {
            psTask.AddUsingStatements(UsingStatements);
        }

        psTask.Runspace = GetRunspace();
        _tasks.Add(psTask.InvokeAsync());
    }

    private async Task ProcessTaskAsync()
    {
        PSTask? pSTask = null;

        try
        {
            Task<PSTask> awaiter = await Task.WhenAny(_tasks);
            _tasks.Remove(awaiter);
            pSTask = await awaiter;
        }
        catch (Exception exception)
        {
            PSOutputStreams.WriteError(exception.CreateProcessingTaskError(this));
        }
        finally
        {
            pSTask?.Dispose();
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
