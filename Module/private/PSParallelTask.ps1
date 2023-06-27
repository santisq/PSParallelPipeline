using namespace System.Management.Automation

class PSParallelTask : IDisposable {
    [powershell] $Instance
    [IAsyncResult] $AsyncResult
    [PSCmdlet] $Cmdlet

    PSParallelTask([scriptblock] $Action, [object] $PipelineObject, [PSCmdlet] $Cmdlet) {
        # Thanks to Patrick Meinecke for his help here.
        # https://github.com/SeeminglyScience/
        $this.Cmdlet = $Cmdlet
        $this.Instance = [powershell]::Create().
            AddScript({
                param([scriptblock] $Action, [object] $Context)

                $Action.InvokeWithContext($null, [psvariable]::new('_', $Context))
            }).
            AddParameters(@{
                Action  = $Action.Ast.GetScriptBlock()
                Context = $PipelineObject
            })
    }

    [PSParallelTask] AddUsingStatements([hashtable] $UsingStatements) {
        if ($UsingStatements.Count) {
            # Credits to Jordan Borean for his help here.
            # https://github.com/jborean93
            $this.Instance.AddParameters(@{ '--%' = $UsingStatements })
        }
        return $this
    }

    [void] Run() {
        $this.AsyncResult = $this.Instance.BeginInvoke()
    }

    [void] EndInvoke() {
        try {
            $this.Cmdlet.WriteObject($this.Instance.EndInvoke($this.AsyncResult), $true)
            $this.GetErrors()
        }
        catch {
            $this.Cmdlet.WriteError($_)
        }
    }

    [void] Stop() {
        $this.Instance.Stop()
    }

    [void] GetErrors() {
        if ($this.Instance.HadErrors) {
            foreach ($err in $this.Instance.Streams.Error) {
                $this.Cmdlet.WriteError($err)
            }
        }
    }

    [PSParallelTask] AssociateWith([runspace] $Runspace) {
        $this.Instance.Runspace = $Runspace
        return $this
    }

    [runspace] GetRunspace() {
        return $this.Instance.Runspace
    }

    [void] Dispose() {
        $this.Instance.Dispose()
    }
}
