namespace PSParallelPipeline;

internal record struct PSOutputData(OutputType Type, object Output)
{
    internal static PSOutputData WriteObject(object sendToPipeline) =>
        new(OutputType.Success, sendToPipeline);

    internal static PSOutputData WriteError(object error) =>
        new(OutputType.Error, error);

    internal static PSOutputData WriteDebug(object debug) =>
        new(OutputType.Debug, debug);

    internal static PSOutputData WriteInformation(object information) =>
        new(OutputType.Information, information);

    internal static PSOutputData WriteProgress(object progress) =>
        new(OutputType.Progress, progress);

    internal static PSOutputData WriteVerbose(object verbose) =>
        new(OutputType.Verbose, verbose);

    internal static PSOutputData WriteWarning(object warning) =>
        new(OutputType.Warning, warning);
}
