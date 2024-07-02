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

    private int MaxRunspaces { get => _settings.MaxRunspaces; }

    private bool UseNewRunspace { get => _settings.UseNewRunspace; }

    private Dictionary<string, object?> UsingStatements { get => _settings.UsingStatements; }

    private readonly Queue<Runspace> _pool;

    private readonly List<Task<PSTask>> _tasks;

    private readonly Dictionary<int, Runspace> _assignedRunspaces;

    private readonly PoolSettings _settings;

    private readonly Worker _worker;

    private readonly List<Runspace> _createdRunspaces;

    internal RunspacePool(PoolSettings settings, Worker worker)
    {
        _settings = settings;
        _worker = worker;
        _pool = new Queue<Runspace>(MaxRunspaces);
        _tasks = new List<Task<PSTask>>(MaxRunspaces);
        _createdRunspaces = new List<Runspace>(MaxRunspaces);
        _assignedRunspaces = new Dictionary<int, Runspace>(MaxRunspaces);
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

        if (_tasks.Count == MaxRunspaces)
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

    internal async Task EnqueueAsync(PSTask psTask)
    {
        if (UsingStatements is { Count: > 0 })
        {
            psTask.AddUsingStatements(UsingStatements);
        }

        Runspace runspace = await GetRunspaceAsync();
        psTask.Runspace = runspace;
        Task<PSTask> task = psTask.InvokeAsync();
        _assignedRunspaces[task.Id] = runspace;
        _tasks.Add(task);
    }

    private async Task ProcessTaskAsync()
    {
        PSTask? pSTask = null;

        try
        {
            Task<PSTask> awaiter = await Task.WhenAny(_tasks);
            Runspace runspace = _assignedRunspaces[awaiter.Id];
            _assignedRunspaces.Remove(awaiter.Id);
            _tasks.Remove(awaiter);

            if (UseNewRunspace)
            {
                runspace.Dispose();
                runspace = CreateRunspace();
            }

            _pool.Enqueue(runspace);
            pSTask = await awaiter;
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
