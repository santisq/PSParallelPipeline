using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal sealed class RunspacePool
{
    private readonly int _maxRunspaces;

    private readonly bool _useNewRunspace;

    private readonly InitialSessionState _iss;

    private readonly Stack<Runspace> _runspacePool;

    private readonly List<Task> _tasks;

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

    // internal

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

        if (_tasks.Count == _maxRunspaces)
        {
            await ProcessTask();
        }

        return CreateRunspace();
    }

    private async Task ProcessTask()
    {
        try
        {
            Task task = await Task.WhenAny(_tasks);
            _tasks.Remove(task);
            await task;
        }
        catch (Exception exception) when (exception is TaskCanceledException or OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _outputPipe.Add(new PSOutputData
            {
                Type = Type.Error,
                Output = new ErrorRecord(
                    exception,
                    "ProcessingTask",
                    ErrorCategory.NotSpecified,
                    this)
            });
        }
    }
}
