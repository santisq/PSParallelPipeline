using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Threading;

namespace PSParallelPipeline;

internal sealed class PSOutputStreams : IDisposable
{
    private BlockingCollection<PSOutputData> OutputPipe { get => _worker.OutputPipe; }

    private CancellationToken Token { get => _worker.Token; }

    internal PSDataCollection<PSObject> Success { get; } = new();

    internal PSDataCollection<ErrorRecord> Error { get; } = new();

    private readonly Worker _worker;

    internal PSOutputStreams(Worker worker)
    {
        _worker = worker;
        SetStreams();
    }

    internal void AddOutput(PSOutputData data) => OutputPipe.Add(data, Token);

    internal void SetStreams()
    {
        Success.DataAdded += (s, e) =>
        {
            foreach (PSObject data in Success.ReadAll())
            {
                AddOutput(new PSOutputData(Type.Success, data));
            }
        };

        Error.DataAdded += (s, e) =>
        {
            foreach (ErrorRecord error in Error.ReadAll())
            {
                AddOutput(new PSOutputData(Type.Error, error));
            }
        };
    }

    public void Dispose()
    {
        Success.Dispose();
        Error.Dispose();
        OutputPipe.Dispose();
        GC.SuppressFinalize(this);
    }
}
