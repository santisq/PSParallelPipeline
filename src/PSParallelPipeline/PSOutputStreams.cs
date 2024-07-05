using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Threading;

namespace PSParallelPipeline;

internal sealed class PSOutputStreams : IDisposable
{
    internal BlockingCollection<PSOutputData> OutputPipe { get => _worker.OutputPipe; }

    internal CancellationToken Token { get => _worker.Token; }

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

    private void SetStreamHandlers()
    {
        Success.DataAdded += (s, e) =>
        {
            foreach (PSObject data in Success.ReadAll())
            {
                WriteObject(data);
            }
        };

        Error.DataAdded += (s, e) =>
        {
            foreach (ErrorRecord error in Error.ReadAll())
            {
                WriteError(error);
            }
        };

        Debug.DataAdded += (s, e) =>
        {
            foreach (DebugRecord debug in Debug.ReadAll())
            {
                WriteDebug(debug);
            }
        };


        Information.DataAdded += (s, e) =>
        {
            foreach (InformationRecord information in Information.ReadAll())
            {
                WriteInformation(information);
            }
        };

        Progress.DataAdded += (s, e) =>
        {
            foreach (ProgressRecord progress in Progress.ReadAll())
            {
                WriteProgress(progress);
            }
        };

        Verbose.DataAdded += (s, e) =>
        {
            foreach (VerboseRecord verbose in Verbose.ReadAll())
            {
                WriteVerbose(verbose);
            }
        };

        Warning.DataAdded += (s, e) =>
        {
            foreach (WarningRecord warning in Warning.ReadAll())
            {
                WriteWarning(warning);
            }
        };
    }

    private void WriteObject(PSObject data) =>
        OutputPipe.Add(PSOutputData.WriteObject(data), Token);

    internal void WriteError(ErrorRecord error) =>
        OutputPipe.Add(PSOutputData.WriteError(error), Token);

    private void WriteDebug(DebugRecord debug) =>
        OutputPipe.Add(PSOutputData.WriteDebug(debug), Token);

    private void WriteInformation(InformationRecord information) =>
        OutputPipe.Add(PSOutputData.WriteInformation(information), Token);

    private void WriteProgress(ProgressRecord progress) =>
        OutputPipe.Add(PSOutputData.WriteProgress(progress), Token);

    private void WriteVerbose(VerboseRecord verbose) =>
        OutputPipe.Add(PSOutputData.WriteVerbose(verbose), Token);

    private void WriteWarning(WarningRecord warning) =>
        OutputPipe.Add(PSOutputData.WriteWarning(warning), Token);

    public void Dispose()
    {
        Success.Dispose();
        Error.Dispose();
        OutputPipe.Dispose();
        GC.SuppressFinalize(this);
    }
}
