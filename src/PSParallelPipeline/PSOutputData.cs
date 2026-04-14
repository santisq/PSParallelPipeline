using System.Management.Automation;

namespace PSParallelPipeline;

internal readonly struct PSOutputData(OutputType Type, object Output)
{
    internal static PSOutputData CreateObject(object sendToPipeline) =>
        new(OutputType.Success, sendToPipeline);

    internal static PSOutputData CreateError(object error) =>
        new(OutputType.Error, error);

    internal static PSOutputData CreateDebug(object debug) =>
        new(OutputType.Debug, debug);

    internal static PSOutputData CreateInformation(object information) =>
        new(OutputType.Information, information);

    internal static PSOutputData CreateProgress(object progress) =>
        new(OutputType.Progress, progress);

    internal static PSOutputData CreateVerbose(object verbose) =>
        new(OutputType.Verbose, verbose);

    internal static PSOutputData CreateWarning(object warning) =>
        new(OutputType.Warning, warning);

    internal readonly void WriteToPipeline(PSCmdlet cmdlet)
    {
        switch (Type)
        {
            case OutputType.Success:
                cmdlet.WriteObject(Output);
                break;

            case OutputType.Error:
                cmdlet.WriteError((ErrorRecord)Output);
                break;

            case OutputType.Debug:
                DebugRecord debug = (DebugRecord)Output;
                cmdlet.WriteDebug(debug.Message);
                break;

            case OutputType.Information:
                cmdlet.WriteInformation((InformationRecord)Output);
                break;

            case OutputType.Progress:
                cmdlet.WriteProgress((ProgressRecord)Output);
                break;

            case OutputType.Verbose:
                VerboseRecord verbose = (VerboseRecord)Output;
                cmdlet.WriteVerbose(verbose.Message);
                break;

            case OutputType.Warning:
                WarningRecord warning = (WarningRecord)Output;
                cmdlet.WriteWarning(warning.Message);
                break;
        }
    }
}
