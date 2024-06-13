using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal sealed class RunspacePool : IDisposable
{
    private readonly int _maxRunspaces;

    private readonly bool _useNewRunspace;

    private int _totalMade;

    private readonly InitialSessionState _iss;

    private readonly Stack<Runspace> _runspacePool;

    private readonly List<Task<PSTask>> _tasks;

    private readonly BlockingCollection<PSOutputData> _outputPipe;

    internal RunspacePool(
        int maxRunspaces,
        bool useNewRunspace,
        InitialSessionState initialSessionState,
        BlockingCollection<PSOutputData> outputPipe)
    {
        _maxRunspaces = maxRunspaces;
        _useNewRunspace = useNewRunspace;
        _iss = initialSessionState;
        _outputPipe = outputPipe;
        _runspacePool = new(_maxRunspaces);
        _tasks = new(_maxRunspaces);
    }

    private Runspace CreateRunspace()
    {
        Runspace rs = RunspaceFactory.CreateRunspace(_iss);
        rs.Open();
        return rs;
    }

    internal async Task<Runspace> GetRunspaceAsync()
    {
        if (_runspacePool.Count > 0)
        {
            return _runspacePool.Pop();
        }

        if (_totalMade == _maxRunspaces)
        {
            await ProcessTaskAsync();
            return _runspacePool.Pop();
        }

        _totalMade++;
        return CreateRunspace();
    }

    internal void Enqueue(Task<PSTask> task) => _tasks.Add(task);

    internal async Task ProcessTasksAsync()
    {
        while (_tasks.Count > 0)
        {
            await ProcessTaskAsync();
        }
    }

    private async Task ProcessTaskAsync()
    {
        try
        {
            Task<PSTask> waitTask = await Task.WhenAny(_tasks);
            _tasks.Remove(waitTask);
            PSTask psTask = await waitTask;
            Runspace runspace = psTask.ReleaseRunspace();

            if (_useNewRunspace)
            {
                runspace.Dispose();
                runspace = CreateRunspace();
            }

            _runspacePool.Push(runspace);
        }
        catch (Exception _) when (_ is TaskCanceledException or OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _outputPipe.Add(exception.CreateProcessingTaskError(this));
        }
    }

    public void Dispose()
    {
        foreach (Runspace runspace in _runspacePool)
        {
            runspace.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
