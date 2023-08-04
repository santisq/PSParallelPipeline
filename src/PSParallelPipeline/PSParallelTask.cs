using System;
using System.Management.Automation;

namespace PSParallelPipeline;

internal class PSParallelTask : IDisposable
{

    PSParallelTask(ScriptBlock action, PSObject pipelineObject)
    {
        PowerShell.Create()
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
