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
        if (inputData is Hashtable) return inputData;

        PSVariable[] vars = [.. engineIntrinsics.InvokeProvider.ChildItem
            .Get("variable:", true)
            .Select(pso => pso.BaseObject)
            .Cast<PSVariable>()
            .Where(var => var.Value is not ScriptBlock && !s_defaultVars.Contains(var.Name))];

        Hashtable parallelVars = [];
        foreach (object input in LanguagePrimitives.ConvertTo<object[]>(inputData))
        {
            if (LanguagePrimitives.TryConvertTo(input, out Hashtable hash))
            {
                foreach (DictionaryEntry entry in hash)
                    parallelVars[entry.Key] = entry.Value;

                continue;
            }

            WildcardPattern pattern = WildcardPattern.Get(
                LanguagePrimitives.ConvertTo<string>(input),
                WildcardOptions.IgnoreCase);

            foreach (PSVariable var in vars)
            {
                if (pattern.IsMatch(var.Name))
                    parallelVars[var.Name] = var.Value;
            }
        }

        return parallelVars;
    }

    private static IEnumerable<string> GetDefaultVariables()
    {
        using PowerShell ps = PowerShell.Create().AddCommand("Get-Variable");
        return ps.Invoke<PSVariable>().Select(e => e.Name);
    }
}
