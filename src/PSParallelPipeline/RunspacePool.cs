using System;
using System.Collections.Concurrent;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal sealed class RunspacePool : IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    private readonly PoolSettings _settings;

    private readonly ConcurrentQueue<Runspace> _pool = [];

    private bool UseNewRunspace { get => _settings.UseNewRunspace; }

    internal int MaxRunspaces { get => _settings.MaxRunspaces; }

    internal CancellationToken Token { get; }

    internal PSOutputStreams Streams { get; }

    internal RunspacePool(
        PoolSettings settings,
        PSOutputStreams streams,
        CancellationToken token)
    {
        Streams = streams;
        Token = token;
        _settings = settings;
        _semaphore = new SemaphoreSlim(MaxRunspaces, MaxRunspaces);
    }

    internal void PushRunspace(Runspace runspace)
    {
        if (UseNewRunspace)
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
        Runspace rs = RunspaceFactory.CreateRunspace(_settings.InitialSessionState);
        rs.Open();
        return rs;
    }

    private Task<Runspace> CreateRunspaceAsync() =>
        Task.Run(CreateRunspace, cancellationToken: Token);

    internal async Task<Runspace> GetRunspaceAsync()
    {
        await _semaphore
            .WaitAsync(Token)
            .ConfigureAwait(false);

        if (_pool.TryDequeue(out Runspace runspace))
        {
            return runspace;
        }

        return await CreateRunspaceAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        foreach (Runspace runspace in _pool)
        {
            runspace.Dispose();
        }

        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
