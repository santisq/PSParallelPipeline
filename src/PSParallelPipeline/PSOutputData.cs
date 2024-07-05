using System.Management.Automation;

namespace PSParallelPipeline;

internal enum Type
{
    Success,
    Error,
    Debug,
    Information,
    Progress,
    Verbose,
    Warning
}

internal record struct PSOutputData(Type Type, object Output)
{
    internal static PSOutputData WriteObject(object sendToPipeline) =>
        new(Type.Success, sendToPipeline);

    internal static PSOutputData WriteError(ErrorRecord error) =>
        new(Type.Error, error);

    internal static PSOutputData WriteDebug(DebugRecord debug) =>
        new(Type.Debug, debug.Message);

    internal static PSOutputData WriteInformation(InformationRecord information) =>
        new(Type.Information, information);

    internal static PSOutputData WriteProgress(ProgressRecord progress) =>
        new(Type.Progress, progress);

    internal static PSOutputData WriteVerbose(VerboseRecord verbose) =>
        new(Type.Verbose, verbose.Message);

    internal static PSOutputData WriteWarning(WarningRecord warning) =>
        new(Type.Warning, warning.Message);
}
