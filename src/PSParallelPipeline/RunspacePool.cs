using System;
using System.Collections.Concurrent;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal sealed class RunspacePool : IDisposable
{
    private readonly PoolSettings _settings;

    private readonly CancellationToken _token;

    private InitialSessionState InitialSessionState { get => _settings.InitialSessionState; }

    private readonly ConcurrentQueue<Runspace> _pool = [];

    private readonly ConcurrentDictionary<Guid, Runspace> _created;

    private bool UseNewRunspace { get => _settings.UseNewRunspace; }

    private readonly SemaphoreSlim _semaphore;

    internal PSOutputStreams Streams { get; }

    internal int MaxRunspaces { get => _settings.MaxRunspaces; }

    internal RunspacePool(
        PoolSettings settings,
        PSOutputStreams streams,
        CancellationToken token)
    {
        _settings = settings;
        Streams = streams;
        _token = token;
        _semaphore = new SemaphoreSlim(MaxRunspaces, MaxRunspaces);
        _created = new ConcurrentDictionary<Guid, Runspace>(
            Environment.ProcessorCount,
            MaxRunspaces);
    }

    internal void PushRunspace(Runspace runspace)
    {
        if (_token.IsCancellationRequested)
        {
            return;
        }

        if (UseNewRunspace)
        {
            runspace.Dispose();
            _created.TryRemove(runspace.InstanceId, out _);
            runspace = CreateRunspace();
        }

        _pool.Enqueue(runspace);
        _semaphore.Release();
    }

    internal CancellationTokenRegistration RegisterCancellation(Action callback) =>
        _token.Register(callback);

    private Runspace CreateRunspace()
    {
        Runspace rs = RunspaceFactory.CreateRunspace(InitialSessionState);
        _created[rs.InstanceId] = rs;
        rs.Open();
        return rs;
    }

    internal async Task<Runspace> GetRunspaceAsync()
    {
        await _semaphore
            .WaitAsync(_token)
            .ConfigureAwait(false);

        if (_pool.TryDequeue(out Runspace runspace))
        {
            return runspace;
        }

        return CreateRunspace();
    }

    public void Dispose()
    {
        foreach (Runspace runspace in _created.Values)
        {
            runspace.Dispose();
        }

        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
