using System;
using System.Management.Automation;

namespace PSParallelPipeline;

internal static class ExceptionHelpers
{
    internal static void WriteTimeoutError(this Exception exception, PSCmdlet cmdlet) =>
        cmdlet.WriteError(new ErrorRecord(
            new TimeoutException("Timeout has been reached.", exception),
            "TimeOutReached",
            ErrorCategory.OperationTimeout,
            cmdlet));

    internal static void WriteEndProcessingError(this Exception exception, PSCmdlet cmdlet) =>
        cmdlet.WriteError(new ErrorRecord(
            exception, "EndProcessingOutput", ErrorCategory.NotSpecified, cmdlet));

    internal static void WriteProcessOutputError(this Exception exception, PSCmdlet cmdlet) =>
        cmdlet.WriteError(new ErrorRecord(
            exception, "ProcessingOutput", ErrorCategory.NotSpecified, cmdlet));

    internal static PSOutputData CreateProcessingTaskError(
        this Exception exception,
        PSCmdlet cmdlet) => new()
        {
            Type = Type.Error,
            Output = new ErrorRecord(
                exception,
                "ProcessingTask",
                ErrorCategory.NotSpecified,
                cmdlet)
        };
}
