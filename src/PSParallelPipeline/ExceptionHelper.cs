using System;
using System.IO;
using System.Management.Automation;
using Microsoft.PowerShell.Commands;

namespace PSParallelPipeline;

internal static class ExceptionHelper
{
    private const string ScriptBlockNotSupported =
        "Passed-in script block variables are not supported, and can result in undefined behavior.";

    internal static void WriteTimeoutError(this Exception exception, PSCmdlet cmdlet) =>
        cmdlet.WriteError(new ErrorRecord(
            new TimeoutException("Timeout has been reached.", exception),
            "TimeOutReached",
            ErrorCategory.OperationTimeout,
            cmdlet));

    internal static PSOutputData CreateProcessingTaskError(this Exception exception, object context) =>
        PSOutputData.CreateError(new ErrorRecord(
            exception, "ProcessingTask", ErrorCategory.NotSpecified, context));

    internal static void ThrowFunctionNotFoundError(
        this Cmdlet cmdlet,
        string function)
    {
        Exception ex = new CommandNotFoundException(
            $"Could not find any function matching the name or pattern '{function}'.");
        ErrorRecord error = new(ex, "FunctionNotFound", ErrorCategory.ObjectNotFound, function);
        cmdlet.ThrowTerminatingError(error);
    }

    private static bool ValueIsNotScriptBlock(object? value) =>
        value is not ScriptBlock and not PSObject { BaseObject: ScriptBlock };

    internal static void ThrowIfVariableIsScriptBlock(this PSCmdlet cmdlet, object? value)
    {
        if (ValueIsNotScriptBlock(value)) return;

        cmdlet.ThrowTerminatingError(new ErrorRecord(
            new PSArgumentException(ScriptBlockNotSupported),
            "PassedInVariableCannotBeScriptBlock",
            ErrorCategory.InvalidType,
            value));
    }

    internal static void ThrowIfInputObjectIsScriptBlock(this object? value, PSCmdlet cmdlet)
    {
        if (ValueIsNotScriptBlock(value)) return;

        cmdlet.ThrowTerminatingError(new ErrorRecord(
            new PSArgumentException(
                string.Concat(
                    "Piped input object cannot be a script block. ",
                    ScriptBlockNotSupported)),
                "InputObjectCannotBeScriptBlock",
                ErrorCategory.InvalidType,
                value));
    }

    internal static void ThrowIfUsingValueIsScriptBlock(this PSCmdlet cmdlet, object? value)
    {
        if (ValueIsNotScriptBlock(value)) return;

        cmdlet.ThrowTerminatingError(new ErrorRecord(
            new PSArgumentException(
                string.Concat(
                    "A $using: variable cannot be a script block. ",
                    ScriptBlockNotSupported)),
                "UsingVariableCannotBeScriptBlock",
                ErrorCategory.InvalidType,
                value));
    }

    internal static void ThrowIfInvalidProvider(
        this ProviderInfo provider,
        string path,
        PSCmdlet cmdlet)
    {
        if (provider.ImplementingType == typeof(FileSystemProvider)) return;

        ErrorRecord error = new(
            new NotSupportedException(
                $"The resolved path '{path}' is not a FileSystem path but '{provider.Name}'."),
            "NotFileSystemPath",
            ErrorCategory.InvalidArgument,
            path);

        cmdlet.ThrowTerminatingError(error);
    }

    internal static void ThrowIfNotDirectory(
        this string path,
        PSCmdlet cmdlet)
    {
        if (Directory.Exists(path)) return;

        ErrorRecord error = new(
            new ArgumentException(
                $"The specified path '{path}' does not exist or is not a directory. " +
                "The path must be a valid directory containing one or more PowerShell modules."),
            "NotDirectoryPath",
            ErrorCategory.InvalidArgument,
            path);

        cmdlet.ThrowTerminatingError(error);
    }
}
