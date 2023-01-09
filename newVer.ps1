using namespace System.Collections.Generic
using namespace System.Management.Automation
using namespace System.Management.Automation.Runspaces
using namespace System.Management.Automation.Language
using namespace System.Management.Automation.Host
using namespace System.Threading
using namespace System.Text

function f {
    [CmdletBinding(PositionalBinding = $false)]
    [Alias('parallel', 'parallelpipeline')]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [object] $InputObject,

        [Parameter(Mandatory, Position = 0)]
        [scriptblock] $ScriptBlock,

        [Parameter()]
        [ValidateRange(1, [int]::MaxValue)]
        [int] $ThrottleLimit = 5,

        [Parameter()]
        [hashtable] $Variables,

        [Parameter()]
        [ArgumentCompleter({
            param(
                [string] $commandName,
                [string] $parameterName,
                [string] $wordToComplete
            )

            (Get-Command -CommandType Filter, Function).Name -like "$wordToComplete*"
        })]
        [string[]] $Functions,

        [Parameter()]
        [switch] $UseNewThread,

        [Parameter()]
        [int] $TimeoutSeconds
    )

    begin {
        try {
            class PSParallelTask : IDisposable {
                [powershell] $Instance
                [IAsyncResult] $AsyncResult
                [PSCmdlet] $Cmdlet
                # [timespan] $TTL

                PSParallelTask([scriptblock] $Action, [object] $PipelineObject, [PSCmdlet] $Cmdlet) {
                    $this.Cmdlet   = $Cmdlet
                    $this.Instance = [powershell]::Create().AddScript({
                        param([scriptblock] $Action, [object] $Context)
                        $Action.InvokeWithContext($null, [psvariable]::new('_', $Context))
                    }).AddParameters(@{
                        Action  = $Action.Ast.GetScriptBlock()
                        Context = $PipelineObject
                    })
                }

                [PSParallelTask] AddUsingStatements([hashtable] $UsingStatements) {
                    if($UsingStatements.Count) {
                        # Credits to Jordan Borean for his help here.
                        # https://github.com/jborean93
                        $this.Instance.AddParameters(@{ '--%' = $UsingStatements })
                    }
                    return $this
                }

                [void] Run() {
                    $this.AsyncResult = $this.Instance.BeginInvoke()
                }

                [void] GetResult() {
                    $this.Instance.Stop()
                    $this.Cmdlet.WriteObject($this.Instance.EndInvoke($this.AsyncResult), $true)
                    foreach($err in $this.Instance.Streams.Error) {
                        $this.Cmdlet.WriteError($err)
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

            # Thanks to Patrick Meinecke for his awesome help here.
            # https://github.com/SeeminglyScience/
            class InvocationManager : IDisposable {
                [int] $ThrottleLimit
                [int] $TotalMade
                [PSHost] $PSHost
                [initialsessionstate] $InitialSessionState
                [Stack[runspace]] $Runspaces = [Stack[runspace]]::new()
                [List[PSParallelTask]] $Tasks = [List[PSParallelTask]]::new()
                [timespan] $InvocationTimeout
                [bool] $WithTimeout

                InvocationManager([int] $ThrottleLimit, [PSHost] $PSHost, [initialsessionstate] $InitialSessionState) {
                    $this.ThrottleLimit       = $ThrottleLimit
                    $this.PSHost              = $PSHost
                    $this.InitialSessionState = $InitialSessionState
                }

                [runspace] TryGet() {
                    if($this.Runspaces.Count) {
                        return $this.Runspaces.Pop()
                    }

                    if($this.TotalMade -ge $this.ThrottleLimit) {
                        return $null
                    }

                    $this.TotalMade++
                    $rs = [runspacefactory]::CreateRunspace($this.PSHost, $this.InitialSessionState)
                    $rs.Open()
                    return $rs
                }

                [PSParallelTask] WaitAny() {
                    if(-not $this.Tasks.Count) {
                        return $null
                    }

                    do {
                        $id = [WaitHandle]::WaitAny($this.Tasks.AsyncResult.AsyncWaitHandle, 200)
                    }
                    while($id -eq [WaitHandle]::WaitTimeout)

                    return $this.Tasks[$id]
                }

                [object] GetTaskResult([PSParallelTask] $Task) {
                    $this.Tasks.Remove($Task)
                    $this.Release($Task.GetRunspace())
                    return $Task.GetResult()
                }

                [void] Release([runspace] $runspace) {
                    $this.Runspaces.Push($runspace)
                }

                [void] AddTask([PSParallelTask] $Task) {
                    $this.Tasks.Add($Task)
                    $Task.Run()
                }

                [void] Dispose() {
                    while($rs = $this.TryGet()) {
                        $rs.Dispose()
                    }
                }

                [void] SetTimeout([int] $Seconds) {
                    $this.WithTimeout       = $true
                    $this.InvocationTimeout = [timespan]::FromSeconds($Seconds)
                }
            }

            $iss = [initialsessionstate]::CreateDefault2()

            foreach($key in $Variables.PSBase.Keys) {
                $iss.Variables.Add([SessionStateVariableEntry]::new($key, $Variables[$key], ''))
            }

            foreach($function in $Functions) {
                $def = (Get-Command $function).Definition
                $iss.Commands.Add([SessionStateFunctionEntry]::new($function, $def))
            }

            $usingParams = @{}

            # Credits to mklement0 for catching up a bug here. Thank you!
            # https://github.com/mklement0
            foreach($usingstatement in $ScriptBlock.Ast.FindAll({ $args[0] -is [UsingExpressionAst] }, $true)) {
                $varText = $usingstatement.Extent.Text
                $varPath = $usingstatement.SubExpression.VariablePath.UserPath

                $key = [Convert]::ToBase64String([Encoding]::Unicode.GetBytes($varText.ToLower()))
                if(-not $usingParams.ContainsKey($key)) {
                    # Huge thanks to SeeminglyScience again and again! The function must use
                    # `$PSCmdlet.SessionState` instead of `$ExecutionContext.SessionState`
                    # to properly refer to the caller's scope.
                    $usingParams.Add($key, $PSCmdlet.SessionState.PSVariable.GetValue($varPath))
                }
            }

            $pool = [InvocationManager]::new($ThrottleLimit, $Host, $iss)

            if($PSBoundParameters.ContainsKey('TimeoutSeconds')) {
                $pool.SetTimeout($TimeoutSeconds)
            }
        }
        catch {
            $PSCmdlet.ThrowTerminatingError($_)
        }
    }
    process {
        try {
            $rs = $pool.TryGet()

            if(-not $rs) {
                $task = $pool.WaitAny()
                $pool.GetTaskResult($task)
                $task.Dispose()
                $rs = $pool.TryGet()
            }

            $task = [PSParallelTask]::new($scriptblock, $InputObject, $PSCmdlet).
                AssociateWith($rs).
                AddUsingStatements($usingParams)

            $pool.AddTask($task)
        }
        catch {
            $PSCmdlet.WriteError($_)
        }
    }
    end {
        try {
            while($task = $pool.WaitAny()) {
                $pool.GetTaskResult($task)
                $task.Dispose()
            }
        }
        catch {
            $PSCmdlet.WriteError($_)
        }
        finally {
            if($pool) {
                $pool.Dispose()
            }
        }
    }
}
function Greet { param($s) "$s hey there!" }

$a = 0..10 | f { Write-Error 123; 1 / 0 }