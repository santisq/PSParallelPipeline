using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

public sealed class CommandCompleter : IArgumentCompleter
{
    [ThreadStatic]
    private static HashSet<string>? _builtinFuncs;

    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        try
        {
            _builtinFuncs ??= new HashSet<string>(GetBuiltinFunctions());

            return CompletionCompleters
                .CompleteCommand(
                    commandName: wordToComplete,
                    moduleName: null,
                    commandTypes: CommandTypes.Function)
                .Where(e => !_builtinFuncs.Contains(e.CompletionText));
        }
        catch
        {
            return [];
        }
    }

    private IEnumerable<string> GetBuiltinFunctions() =>
        Runspace.DefaultRunspace.InitialSessionState.Commands
            .Where(e => e.CommandType is CommandTypes.Function)
            .Select(e => e.Name);
}
