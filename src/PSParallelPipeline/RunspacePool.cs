using System;
using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal sealed class RunspacePool : IDisposable
{
    internal PSOutputStreams PSOutputStreams { get => _worker.OutputStreams; }

    private CancellationToken Token { get => _worker.Token; }

    private InitialSessionState InitialSessionState { get => _settings.InitialSessionState; }

    private bool UseNewRunspace { get => _settings.UseNewRunspace; }

    private Dictionary<string, object?> UsingStatements { get => _settings.UsingStatements; }

    private readonly Queue<Runspace> _pool;

    private readonly PoolSettings _settings;

    private readonly Worker _worker;

    private readonly List<Runspace> _createdRunspaces;

    private readonly TaskManager _manager;

    internal RunspacePool(PoolSettings settings, Worker worker)
    {
        _settings = settings;
        _worker = worker;
        _pool = new Queue<Runspace>(_settings.MaxRunspaces);
        _createdRunspaces = new List<Runspace>(_settings.MaxRunspaces);
        _manager = new(_settings.MaxRunspaces);
    }

    private Runspace CreateRunspace()
    {
        Runspace rs = RunspaceFactory.CreateRunspace(InitialSessionState);
        rs.Open();
        _createdRunspaces.Add(rs);
        return rs;
    }

    private async Task<Runspace> GetRunspaceAsync()
    {
        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
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
            Runspace runspace = _manager.Dequeue(awaiter);

            if (UseNewRunspace)
            {
                runspace.Dispose();
                runspace = CreateRunspace();
            }

            _pool.Enqueue(runspace);
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
        foreach (Runspace runspace in _createdRunspaces)
        {
            runspace.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
