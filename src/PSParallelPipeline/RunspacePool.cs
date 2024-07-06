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

    private readonly ConcurrentDictionary<Guid, Task> _tasks;

    internal bool UseNewRunspace { get => _settings.UseNewRunspace; }

    internal PSOutputStreams PSOutputStreams { get => _worker.OutputStreams; }

    internal RunspacePool(PoolSettings settings, Worker worker)
    {
        _settings = settings;
        _worker = worker;
        _pool = new ConcurrentQueue<Runspace>();
        _tasks = new ConcurrentDictionary<Guid, Task>(MaxRunspaces, MaxRunspaces);
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

    internal void RemoveTask(PSTask psTask) => _tasks.TryRemove(psTask.Id, out _);

    private Runspace GetRunspace()
    {
        if (_pool.TryDequeue(out Runspace runspace))
        {
            return runspace;
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
        if (_tasks.Count == MaxRunspaces)
        {
            await ProcessTaskAsync();
        }

        if (UsingStatements.Count > 0)
        {
            psTask.AddUsingStatements(UsingStatements);
        }

        psTask.Runspace = GetRunspace();
        _tasks[psTask.Id] = psTask.InvokeAsync();
    }

    private async Task ProcessTaskAsync()
    {
        try
        {
            Task awaiter = await Task.WhenAny(_tasks.Values);
            await awaiter;
        }
        catch (Exception exception)
        {
            PSOutputStreams.WriteError(exception.CreateProcessingTaskError(this));
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
