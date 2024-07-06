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

    // private readonly List<Runspace> _createdRunspaces;

    private readonly TaskManager _manager;

    private bool UseNewRunspace { get => _settings.UseNewRunspace; }

    internal PSOutputStreams PSOutputStreams { get => _worker.OutputStreams; }

    internal RunspacePool(PoolSettings settings, Worker worker)
    {
        _settings = settings;
        _worker = worker;
        _pool = new ConcurrentQueue<Runspace>();
        // _createdRunspaces = new List<Runspace>(_settings.MaxRunspaces);
        _manager = new TaskManager(_settings.MaxRunspaces);
    }

    private Runspace CreateRunspace()
    {
        Runspace rs = RunspaceFactory.CreateRunspace(InitialSessionState);
        rs.Open();
        // _createdRunspaces.Add(rs);
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

    private async Task<Runspace> GetRunspaceAsync()
    {
        if (_pool.TryDequeue(out Runspace runspace))
        {
            return runspace;
        }

        if (_manager.ShouldProcess)
        {
            await ProcessTaskAsync();
            return _pool.Dequeue();
        }

        return CreateRunspace();
    }

    internal async Task ProcessTasksAsync()
    {
        while (_manager.HasMoreTasks)
        {
            await ProcessTaskAsync();
        }
    }

    internal async Task EnqueueAsync(PSTask psTask)
    {
        if (UsingStatements is { Count: > 0 })
        {
            psTask.AddUsingStatements(UsingStatements);
        }

        psTask.Runspace = await GetRunspaceAsync();
        _manager.Enqueue(psTask);
    }

    private async Task ProcessTaskAsync()
    {
        PSTask? pSTask = null;

        try
        {
            Task<PSTask> awaiter = await _manager.WhenAny();
            _manager.Remove(awaiter);
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
