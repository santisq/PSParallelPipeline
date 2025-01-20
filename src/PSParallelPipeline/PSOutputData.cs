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

    internal static PSOutputData WriteError(object error) =>
        new(Type.Error, error);

    internal static PSOutputData WriteDebug(object debug) =>
        new(Type.Debug, debug);

    internal static PSOutputData WriteInformation(object information) =>
        new(Type.Information, information);

    internal static PSOutputData WriteProgress(object progress) =>
        new(Type.Progress, progress);

    internal static PSOutputData WriteVerbose(object verbose) =>
        new(Type.Verbose, verbose);

    internal static PSOutputData WriteWarning(object warning) =>
        new(Type.Warning, warning);
}
