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
        SetStreamHandlers();
    }

    internal void AddOutput(PSOutputData data) => OutputPipe.Add(data); //, Token);

    private void SetStreamHandlers()
    {
        Success.DataAdding += (s, e) =>
            AddOutput(PSOutputData.WriteObject(e.ItemAdded));

        Error.DataAdding += (s, e) =>
            AddOutput(PSOutputData.WriteError(e.ItemAdded));

        Debug.DataAdding += (s, e) =>
            AddOutput(PSOutputData.WriteDebug(e.ItemAdded));

        Information.DataAdding += (s, e) =>
            AddOutput(PSOutputData.WriteInformation(e.ItemAdded));

        Progress.DataAdding += (s, e) =>
            AddOutput(PSOutputData.WriteProgress(e.ItemAdded));

        Verbose.DataAdding += (s, e) =>
            AddOutput(PSOutputData.WriteVerbose(e.ItemAdded));

        Warning.DataAdding += (s, e) =>
            AddOutput(PSOutputData.WriteWarning(e.ItemAdded));
    }

    public void Dispose()
    {
        Success.Dispose();
        Error.Dispose();
        Debug.Dispose();
        Information.Dispose();
        Progress.Dispose();
        Verbose.Dispose();
        Warning.Dispose();
        OutputPipe.Dispose();
        GC.SuppressFinalize(this);
    }
}
