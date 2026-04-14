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
        RegisterHandlers();
    }

    private void AddOutput(object sender, DataAddingEventArgs args)
        => Output.Add(PSOutputData.CreateObject(args.ItemAdded));

    internal void AddError(PSOutputData outputData) => Output.Add(outputData);

    private void AddError(object sender, DataAddingEventArgs args)
        => Output.Add(PSOutputData.CreateError(args.ItemAdded));

    private void AddDebug(object sender, DataAddingEventArgs args)
        => Output.Add(PSOutputData.CreateDebug(args.ItemAdded));

    private void AddInformation(object sender, DataAddingEventArgs args)
        => Output.Add(PSOutputData.CreateInformation(args.ItemAdded));

    private void AddProgress(object sender, DataAddingEventArgs args)
        => Output.Add(PSOutputData.CreateProgress(args.ItemAdded));

    private void AddVerbose(object sender, DataAddingEventArgs args)
        => Output.Add(PSOutputData.CreateVerbose(args.ItemAdded));

    private void AddWarning(object sender, DataAddingEventArgs args)
        => Output.Add(PSOutputData.CreateWarning(args.ItemAdded));

    private void RegisterHandlers()
    {
        Success.DataAdding += AddOutput;
        Error.DataAdding += AddError;
        Debug.DataAdding += AddDebug;
        Information.DataAdding += AddInformation;
        Progress.DataAdding += AddProgress;
        Verbose.DataAdding += AddVerbose;
        Warning.DataAdding += AddWarning;
    }

    private void UnregisterHandlers()
    {
        Success.DataAdding -= AddOutput;
        Error.DataAdding -= AddError;
        Debug.DataAdding -= AddDebug;
        Information.DataAdding -= AddInformation;
        Progress.DataAdding -= AddProgress;
        Verbose.DataAdding -= AddVerbose;
        Warning.DataAdding -= AddWarning;
    }

    public void Dispose()
    {
        UnregisterHandlers();
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
