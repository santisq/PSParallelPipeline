using System;
using System.Collections.Concurrent;
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

    private int MaxRunspaces { get => _settings.MaxRunspaces; }

    private bool UseNewRunspace { get => _settings.UseNewRunspace; }

    private Dictionary<string, object?> UsingStatements { get => _settings.UsingStatements; }

    private readonly Queue<Runspace> _pool;

    private readonly List<Task<PSTask>> _tasks;

    private readonly Dictionary<Guid, PSTask> _psTasks;

    private readonly PoolSettings _settings;

    private readonly Worker _worker;

    // private readonly List<Runspace> _createdRunspaces;

    internal RunspacePool(PoolSettings settings, Worker worker)
    {
        _settings = settings;
        _worker = worker;
        _pool = new Queue<Runspace>(MaxRunspaces);
        _tasks = new List<Task<PSTask>>(MaxRunspaces);
        // _createdRunspaces = new List<Runspace>(MaxRunspaces);
        _psTasks = new Dictionary<Guid, PSTask>(MaxRunspaces);
    }

    internal void AddTask(PSTask task) => _psTasks[task.Id] = task;

    internal void RemoveTask(PSTask task) => _psTasks.Remove(task.Id);

    private Runspace CreateRunspace()
    {
        Runspace rs = RunspaceFactory.CreateRunspace(InitialSessionState);
        rs.Open();
        // _createdRunspaces.Add(rs);
        return rs;
    }

    private async Task<Runspace> GetRunspaceAsync()
    {
        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
        }

        if (_psTasks.Count >= MaxRunspaces)
        {
            await ProcessTaskAsync();
            return _pool.Dequeue();
        }

        return CreateRunspace();
    }

    internal async Task ProcessTasksAsync()
    {
        while (_tasks.Count > 0)
        {
            await ProcessTaskAsync();
        }
    }

    internal async Task EnqueueAsync(PSTask task)
    {
        if (UsingStatements is { Count: > 0 })
        {
            task.AddUsingStatements(UsingStatements);
        }

        task.Runspace = await GetRunspaceAsync();
        _tasks.Add(task.InvokeAsync());
    }

    private async Task ProcessTaskAsync()
    {
        PSTask? ps = null;

        try
        {
            Task<PSTask> awaiter = await Task.WhenAny(_tasks);
            _tasks.Remove(awaiter);
            ps = await awaiter;
            Runspace runspace = ps.Runspace;

            if (UseNewRunspace)
            {
                ps.Runspace.Dispose();
                runspace = CreateRunspace();
            }

            _pool.Enqueue(runspace);
        }
        catch (Exception _) when (_ is TaskCanceledException or OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            PSOutputStreams.AddOutput(exception.CreateProcessingTaskError(this));
        }
        finally
        {
            if (ps is not null)
            {
                ps.Dispose();
                RemoveTask(ps);
            }
        }
    }

    internal CancellationTokenRegistration RegisterCancellation(Action callback) =>
        Token.Register(callback);

    public void Dispose()
    {
        foreach ((Guid _, PSTask task) in _psTasks)
        {
            task.Dispose();
            task.Runspace.Dispose();
        }

        foreach (Runspace runspace in _pool)
        {
            runspace.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
