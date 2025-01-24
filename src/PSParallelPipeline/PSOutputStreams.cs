using System;
using System.Collections.Concurrent;
using System.Management.Automation;

namespace PSParallelPipeline;

internal sealed class PSOutputStreams : IDisposable
{
    private BlockingCollection<PSOutputData> Output { get; }

    internal PSDataCollection<PSObject> Success { get; } = [];

    internal PSDataCollection<ErrorRecord> Error { get; } = [];

    internal PSDataCollection<DebugRecord> Debug { get; } = [];

    internal PSDataCollection<InformationRecord> Information { get; } = [];

    internal PSDataCollection<ProgressRecord> Progress { get; } = [];

    internal PSDataCollection<VerboseRecord> Verbose { get; } = [];

    internal PSDataCollection<WarningRecord> Warning { get; } = [];

    internal PSOutputStreams(BlockingCollection<PSOutputData> output)
    {
        Output = output;
        SetHandlers();
    }

    internal void AddOutput(PSOutputData data) => Output.Add(data);

    private void SetHandlers()
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
        GC.SuppressFinalize(this);
    }
}
