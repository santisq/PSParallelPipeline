using System;
using System.Collections.Concurrent;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal sealed class RunspacePool : IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    private readonly InitialSessionState _initialSessionState;

    private readonly ConcurrentQueue<Runspace> _pool = [];

    private readonly bool _useNewRunspace;

    private readonly int _maxRunspaces;

    private readonly CancellationToken _token;

    private readonly PSOutputStreams _streams;

    internal RunspacePool(
        PoolSettings settings,
        PSOutputStreams streams,
        CancellationToken token)
    {
        _streams = streams;
        _token = token;
        _initialSessionState = settings.InitialSessionState;
        _useNewRunspace = settings.UseNewRunspace;
        _maxRunspaces = settings.MaxRunspaces;
        _semaphore = new SemaphoreSlim(_maxRunspaces, _maxRunspaces);
    }

    private void PushRunspace(Runspace? runspace)
    {
        if (runspace is null) return;

        if (_useNewRunspace)
        {
            runspace.Dispose();
            _semaphore.Release();
            return;
        }

        _pool.Enqueue(runspace);
        _semaphore.Release();
    }

    private Runspace CreateRunspace()
    {
        Runspace rs = RunspaceFactory.CreateRunspace(_initialSessionState);
        rs.Open();
        return rs;
    }

    private Task<Runspace> CreateRunspaceAsync() =>
        Task.Run(CreateRunspace, cancellationToken: _token);

    private async Task<Runspace> GetRunspaceAsync()
    {
        await _semaphore.WaitAsync(_token).NoContext();
        if (_pool.TryDequeue(out Runspace runspace))
            return runspace;

        return await CreateRunspaceAsync().NoContext();
    }

    internal async Task InvokePowerShellAsync(object? input, TaskSettings settings)
    {
        Runspace? runspace = null;

        try
        {
            runspace = await GetRunspaceAsync().NoContext();
            await PSTask.InvokeAsync(input, runspace, _streams, settings, _token);
        }
        catch (Exception exception)
        {
            _streams.AddError(exception.CreateProcessingTaskError());
        }
        finally
        {
            PushRunspace(runspace);
        }
    }


    public void Dispose()
    {
        foreach (Runspace runspace in _pool)
            runspace.Dispose();

        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
