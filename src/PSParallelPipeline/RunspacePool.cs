using System;
using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal sealed class RunspacePool : IDisposable
{
    internal PSOutputStreams PSOutputStreams { get => _woker.OutputStreams; }

    private CancellationToken Token { get => _woker.Token; }

    private InitialSessionState InitialSessionState { get => _settings.InitialSessionState; }

    private int MaxRunspaces { get => _settings.MaxRunspaces; }

    private bool UseNewRunspace { get => _settings.UseNewRunspace; }

    private Dictionary<string, object?> UsingStatements { get => _settings.UsingStatements; }

    private int _totalMade;

    private readonly Queue<Runspace> _pool;

    private readonly List<Task<PSTask>> _tasks;

    private readonly PoolSettings _settings;

    private readonly Worker _woker;

    internal RunspacePool(PoolSettings settings, Worker worker)
    {
        _settings = settings;
        _woker = worker;
        _pool = new Queue<Runspace>(MaxRunspaces);
        _tasks = new List<Task<PSTask>>(MaxRunspaces);
    }

    private Runspace CreateRunspace()
    {
        Runspace rs = RunspaceFactory.CreateRunspace(InitialSessionState);
        rs.Open();
        return rs;
    }

    private async Task<Runspace> GetRunspaceAsync()
    {
        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
        }

        if (_totalMade == MaxRunspaces)
        {
            await ProcessTaskAsync();
            Token.ThrowIfCancellationRequested();
            return _pool.Dequeue();
        }

        _totalMade++;
        return CreateRunspace();
    }

    internal async Task ProcessTasksAsync()
    {
        while (_tasks.Count > 0)
        {
            await ProcessTaskAsync();
            Token.ThrowIfCancellationRequested();
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
            Token.ThrowIfCancellationRequested();
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
            ps?.Dispose();
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
