using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace PSParallelPipeline;

public sealed class VariableTransformation : ArgumentTransformationAttribute
{
    private static readonly HashSet<string> s_defaultVars = [.. GetDefaultVariables()];

    public override object Transform(EngineIntrinsics engineIntrinsics, object inputData)
    {
        if (inputData is PSObject pso) inputData = pso.BaseObject;
        if (inputData is IDictionary) return inputData;

        PSVariable[] vars = [.. engineIntrinsics.InvokeProvider.ChildItem
            .Get("variable:", true)
            .Select(pso => pso.BaseObject)
            .Cast<PSVariable>()
            .Where(var => var.Value.IsNotScriptBlock() && !s_defaultVars.Contains(var.Name))];

        Hashtable parallelVars = [];
        foreach (object? input in LanguagePrimitives.ConvertTo<object[]>(inputData))
        {
            if (input is null)
                throw new ArgumentNullException();

            if (LanguagePrimitives.TryConvertTo(input, out Hashtable hash))
            {
                foreach (DictionaryEntry entry in hash)
                    parallelVars[entry.Key] = entry.Value;

                continue;
            }

            bool shouldThrow = true;
            string inputAsString = LanguagePrimitives.ConvertTo<string>(input);
            foreach (PSVariable var in vars)
            {
                if (inputAsString.Matches(var.Name))
                {
                    shouldThrow = false;
                    parallelVars[var.Name] = var.Value;
                }
            }

            if (shouldThrow)
                throw new ItemNotFoundException(
                    $"Could not find any variable matching the name or pattern '{inputAsString}'.");
        }

        return parallelVars;
    }

    private static IEnumerable<string> GetDefaultVariables()
    {
        using PowerShell ps = PowerShell.Create().AddCommand("Get-Variable");
        return ps.Invoke<PSVariable>().Select(e => e.Name);
    }
}
