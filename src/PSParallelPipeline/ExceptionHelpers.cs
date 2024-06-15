using System;
using System.Management.Automation;

namespace PSParallelPipeline;

internal static class ExceptionHelpers
{
    private const string _notsupported =
        "Passed-in script block variables are not supported, and can result in undefined behavior.";

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
        this Exception exception, object context) =>
        new()
        {
            Type = Type.Error,
            Output = new ErrorRecord(
                exception,
                "ProcessingTask",
                ErrorCategory.NotSpecified,
                context)
        };

    internal static void ThrowIfInputObjectIsScriptblock(this PSCmdlet cmdlet, object? input)
    {
        if (input is not ScriptBlock and not PSObject { BaseObject: ScriptBlock })
        {
            return;
        }

        cmdlet.ThrowTerminatingError(new ErrorRecord(
            new PSArgumentException(
                string.Concat(
                    "Piped input object cannot be a script block. ",
                    _notsupported)),
                "InputObjectCannotBeScriptBlock",
                ErrorCategory.InvalidType,
                cmdlet));
    }

    internal static void ThrowIfUsingValueIsScriptblock(this PSCmdlet cmdlet, object? value)
    {
        if (value is not ScriptBlock and not PSObject { BaseObject: ScriptBlock })
        {
            return;
        }

        cmdlet.ThrowTerminatingError(new ErrorRecord(
            new PSArgumentException(
                string.Concat(
                    "A $using: variable cannot be a script block. ",
                    _notsupported)),
                "InputObjectCannotBeScriptBlock",
                ErrorCategory.InvalidType,
                cmdlet));
    }
}
