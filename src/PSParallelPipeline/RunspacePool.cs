using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal sealed class RunspacePool : IDisposable
{
    private CancellationToken Token { get; }

    private readonly InitialSessionState _iss;

    private readonly ConcurrentQueue<Runspace> _pool = [];

    private readonly List<Runspace> _created;

    private readonly bool _useNew;

    private readonly SemaphoreSlim _semaphore;

    internal PSOutputStreams Streams { get; }

    internal int MaxRunspaces { get; }

    internal RunspacePool(
        PoolSettings settings,
        PSOutputStreams streams,
        CancellationToken token)
    {
        (MaxRunspaces, _useNew, _iss) = settings;
        Streams = streams;
        Token = token;
        _created = new List<Runspace>(MaxRunspaces);
        _semaphore = new SemaphoreSlim(MaxRunspaces, MaxRunspaces);
    }
    internal void PushRunspace(Runspace runspace)
    {
        if (_useNew)
        {
            runspace.Dispose();

            lock (_created)
            {
                _created.Remove(runspace);
                runspace = CreateRunspace();
                _created.Add(runspace);
            }
        }

        _pool.Enqueue(runspace);
        _semaphore.Release();
    }

    internal CancellationTokenRegistration RegisterCancellation(Action callback) =>
        Token.Register(callback);

    private Runspace CreateRunspace()
    {
        Runspace rs = RunspaceFactory.CreateRunspace(_iss);
        rs.Open();
        return rs;
    }

    internal async Task<Runspace> GetRunspaceAsync()
    {
        await _semaphore.WaitAsync(Token);
        if (_pool.TryDequeue(out Runspace runspace))
        {
            return runspace;
        }

        lock (_created)
        {
            runspace = CreateRunspace();
            _created.Add(runspace);
        }

        return runspace;
    }

    public void Dispose()
    {
        foreach (Runspace runspace in _created)
        {
            runspace.Dispose();
        }

        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
