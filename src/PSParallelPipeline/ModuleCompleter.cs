using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        using PowerShell powershell = PowerShell
            .Create(RunspaceMode.CurrentRunspace)
            .AddCommand("Get-Module")
            .AddParameter("ListAvailable");

        return powershell
            .Invoke<PSModuleInfo>()
            .DistinctBy(e => e.Name)
            .Where(e => e.Name.StartsWith(wordToComplete, StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(e => e.Name)
            .Select(e => new CompletionResult(e.Name));
    }
}
