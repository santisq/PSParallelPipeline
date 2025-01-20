using System;
using System.Management.Automation;

namespace PSParallelPipeline;

internal static class ExceptionHelper
{
    private const string _notsupported =
        "Passed-in script block variables are not supported, and can result in undefined behavior.";

    internal static void WriteTimeoutError(this Exception exception, PSCmdlet cmdlet) =>
        cmdlet.WriteError(new ErrorRecord(
            new TimeoutException("Timeout has been reached.", exception),
            "TimeOutReached",
            ErrorCategory.OperationTimeout,
            cmdlet));

    internal static PSOutputData CreateProcessingTaskError(this Exception exception, object context) =>
        PSOutputData.WriteError(new ErrorRecord(
            exception, "ProcessingTask", ErrorCategory.NotSpecified, context));

    internal static void ThrowFunctionNotFoundError(
        this CommandNotFoundException exception,
        Cmdlet cmdlet,
        string function) =>
        cmdlet.ThrowTerminatingError(new ErrorRecord(
            exception, "FunctionNotFound", ErrorCategory.ObjectNotFound, function));

    private static bool ValueIsNotScriptBlock(object? value) =>
        value is not ScriptBlock and not PSObject { BaseObject: ScriptBlock };

    internal static CommandInfo ThrowIfFunctionNotFoundError(
        this CommandInfo? command,
        string function)
    {
        if (command is not null)
        {
            return command;
        }

        throw new CommandNotFoundException(
            $"The function with name '{function}' could not be found.");
    }

    internal static void ThrowIfVariableIsScriptBlock(this PSCmdlet cmdlet, object? value)
    {
        if (ValueIsNotScriptBlock(value))
        {
            return;
        }

        cmdlet.ThrowTerminatingError(new ErrorRecord(
            new PSArgumentException(_notsupported),
            "PassedInVariableCannotBeScriptBlock",
            ErrorCategory.InvalidType,
            value));
    }

    internal static void ThrowIfInputObjectIsScriptBlock(this PSCmdlet cmdlet, object? value)
    {
        if (ValueIsNotScriptBlock(value))
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
                value));
    }

    internal static void ThrowIfUsingValueIsScriptBlock(this PSCmdlet cmdlet, object? value)
    {
        if (ValueIsNotScriptBlock(value))
        {
            return;
        }

        cmdlet.ThrowTerminatingError(new ErrorRecord(
            new PSArgumentException(
                string.Concat(
                    "A $using: variable cannot be a script block. ",
                    _notsupported)),
                "UsingVariableCannotBeScriptBlock",
                ErrorCategory.InvalidType,
                value));
    }
}
