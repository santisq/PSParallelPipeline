using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Threading;

namespace PSParallelPipeline;

internal sealed class PSOutputStreams : IDisposable
{
    private BlockingCollection<PSOutputData> OutputPipe { get => _worker.OutputPipe; }

    private CancellationToken Token { get => _worker.Token; }

    internal PSDataCollection<PSObject> Success { get; } = [];

    internal PSDataCollection<ErrorRecord> Error { get; } = [];

    private readonly Worker _worker;

    internal PSOutputStreams(Worker worker)
    {
        _worker = worker;
        SetStreams(this);
    }

    internal void AddOutput(PSOutputData data) => OutputPipe.Add(data, Token);

    private static void SetStreams(PSOutputStreams outputStreams)
    {
        outputStreams.Success.DataAdded += (s, e) =>
        {
            foreach (PSObject data in outputStreams.Success.ReadAll())
            {
                outputStreams.AddOutput(new PSOutputData(Type.Success, data));
            }
        };

        outputStreams.Error.DataAdded += (s, e) =>
        {
            foreach (ErrorRecord error in outputStreams.Error.ReadAll())
            {
                outputStreams.AddOutput(new PSOutputData(Type.Error, error));
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
