using System;
using System.IO;
using System.Management.Automation;
using Microsoft.PowerShell.Commands;

namespace PSParallelPipeline;

internal static class ExceptionExtensions
{
    private const string ScriptBlockNotSupported =
        "Passed-in script block variables are not supported, and can result in undefined behavior.";

    extension(Exception exception)
    {
        internal void WriteTimeoutError(PSCmdlet cmdlet) =>
        cmdlet.WriteError(new ErrorRecord(
            new TimeoutException("Timeout has been reached.", exception),
            "TimeOutReached",
            ErrorCategory.OperationTimeout,
            cmdlet));

        internal PSOutputData CreateProcessingTaskError() =>
            PSOutputData.CreateError(new ErrorRecord(
                exception, "ProcessingTask", ErrorCategory.NotSpecified, null));
    }

    extension(object? value)
    {
        internal bool IsNotScriptBlock() =>
        value is not ScriptBlock and not PSObject { BaseObject: ScriptBlock };

        internal void ThrowIfInputObjectIsScriptBlock(PSCmdlet cmdlet)
        {
            if (value.IsNotScriptBlock()) return;

            cmdlet.ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException(
                    string.Concat(
                        "Piped input object cannot be a script block. ",
                        ScriptBlockNotSupported)),
                    "InputObjectCannotBeScriptBlock",
                    ErrorCategory.InvalidType,
                    value));
        }
    }

    extension(PSCmdlet cmdlet)
    {
        internal void ThrowIfVariableIsScriptBlock(object? value)
        {
            if (value.IsNotScriptBlock()) return;

            cmdlet.ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException(ScriptBlockNotSupported),
                "PassedInVariableCannotBeScriptBlock",
                ErrorCategory.InvalidType,
                value));
        }

        internal void ThrowIfUsingValueIsScriptBlock(object? value)
        {
            if (value.IsNotScriptBlock()) return;

            cmdlet.ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException(
                    string.Concat(
                        "A $using: variable cannot be a script block. ",
                        ScriptBlockNotSupported)),
                    "UsingVariableCannotBeScriptBlock",
                    ErrorCategory.InvalidType,
                    value));
        }

        internal void ThrowFunctionNotFoundError(string function)
        {
            Exception ex = new CommandNotFoundException(
                $"Could not find any function matching the name or pattern '{function}'.");
            ErrorRecord error = new(ex, "FunctionNotFound", ErrorCategory.ObjectNotFound, function);
            cmdlet.ThrowTerminatingError(error);
        }

        internal void ThrowVariableNotFoundError(string variable)
        {
            Exception ex = new ItemNotFoundException(
                $"Could not find any variable matching the name or pattern '{variable}'.");
            ErrorRecord error = new(ex, "VariableNotFound", ErrorCategory.ObjectNotFound, variable);
            cmdlet.ThrowTerminatingError(error);
        }
    }

    extension(ProviderInfo provider)
    {
        internal void ThrowIfInvalidProvider(string path, PSCmdlet cmdlet)
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
    }

    extension(string path)
    {
        internal void ThrowIfNotDirectory(PSCmdlet cmdlet)
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
}
