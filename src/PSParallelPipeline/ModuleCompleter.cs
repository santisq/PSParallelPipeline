using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace PSParallelPipeline;

public sealed class ModuleCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        using PowerShell ps = PowerShell
            .Create(RunspaceMode.CurrentRunspace)
            .AddCommand("Get-Module")
            .AddParameter("ListAvailable");

        foreach (PSModuleInfo module in ps.Invoke<PSModuleInfo>())
        {
            if (module.Name.StartsWith(wordToComplete, StringComparison.InvariantCultureIgnoreCase))
            {
                yield return new CompletionResult(module.Name);
            }
        }
    }
}
