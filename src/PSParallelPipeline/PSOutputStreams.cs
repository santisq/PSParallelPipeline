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

    internal PSDataCollection<DebugRecord> Debug { get; } = [];

    internal PSDataCollection<InformationRecord> Information { get; } = [];

    internal PSDataCollection<ProgressRecord> Progress { get; } = [];

    internal PSDataCollection<VerboseRecord> Verbose { get; } = [];

    internal PSDataCollection<WarningRecord> Warning { get; } = [];

    private readonly Worker _worker;

    internal PSOutputStreams(Worker worker)
    {
        _worker = worker;
        SetStreamHandlers(this);
    }

    internal void AddOutput(PSOutputData data) => OutputPipe.Add(data, Token);

    private static void SetStreamHandlers(PSOutputStreams outputStreams)
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

        outputStreams.Debug.DataAdded += (s, e) =>
        {
            foreach (DebugRecord debug in outputStreams.Debug.ReadAll())
            {
                outputStreams.AddOutput(new PSOutputData(Type.Debug, debug.Message));
            }
        };


        outputStreams.Information.DataAdded += (s, e) =>
        {
            foreach (InformationRecord information in outputStreams.Information.ReadAll())
            {
                outputStreams.AddOutput(new PSOutputData(Type.Information, information));
            }
        };

        outputStreams.Progress.DataAdded += (s, e) =>
        {
            foreach (ProgressRecord progress in outputStreams.Progress.ReadAll())
            {
                outputStreams.AddOutput(new PSOutputData(Type.Progress, progress));
            }
        };

        outputStreams.Verbose.DataAdded += (s, e) =>
        {
            foreach (VerboseRecord verbose in outputStreams.Verbose.ReadAll())
            {
                outputStreams.AddOutput(new PSOutputData(Type.Verbose, verbose.Message));
            }
        };

        outputStreams.Warning.DataAdded += (s, e) =>
        {
            foreach (WarningRecord warning in outputStreams.Warning.ReadAll())
            {
                outputStreams.AddOutput(new PSOutputData(Type.Warning, warning.Message));
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
