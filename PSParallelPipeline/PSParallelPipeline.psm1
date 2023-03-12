using namespace System.Text
using namespace System.Threading
using namespace System.Diagnostics
using namespace System.Collections.Generic
using namespace System.Management.Automation
using namespace System.Management.Automation.Host
using namespace System.Management.Automation.Language
using namespace System.Management.Automation.Runspaces

class PSParallelTask : IDisposable {
    [powershell] $Instance
    [IAsyncResult] $AsyncResult
    [PSCmdlet] $Cmdlet

    PSParallelTask([scriptblock] $Action, [object] $PipelineObject, [PSCmdlet] $Cmdlet) {
        # Thanks to Patrick Meinecke for his help here.
        # https://github.com/SeeminglyScience/
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
        if($this.Instance.HadErrors) {
            foreach($err in $this.Instance.Streams.Error) {
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

class InvocationManager : IDisposable {
    [int] $ThrottleLimit
    [PSHost] $PSHost
    [initialsessionstate] $InitialSessionState
    [Stack[runspace]] $Runspaces = [Stack[runspace]]::new()
    [List[PSParallelTask]] $Tasks = [List[PSParallelTask]]::new()
    [bool] $UseNewRunspace
    hidden [int] $TotalMade

    InvocationManager(
        [int] $ThrottleLimit,
        [PSHost] $PSHost,
        [initialsessionstate] $InitialSessionState,
        [bool] $UseNewRunspace
    ) {
        $this.ThrottleLimit       = $ThrottleLimit
        $this.PSHost              = $PSHost
        $this.InitialSessionState = $InitialSessionState
        $this.UseNewRunspace      = $UseNewRunspace
    }

    [runspace] TryGet() {
        if($this.Runspaces.Count) {
            return $this.Runspaces.Pop()
        }

        if($this.TotalMade -ge $this.ThrottleLimit) {
            return $null
        }

        $this.TotalMade++
        return $this.CreateRunspace()
    }

    [runspace] CreateRunspace() {
        $runspace = [runspacefactory]::CreateRunspace($this.PSHost, $this.InitialSessionState)
        $runspace.Open()
        return $runspace
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

    [PSParallelTask] WaitAny([int] $TimeoutSeconds, [Stopwatch] $Timer) {
        if(-not $this.Tasks.Count) {
            return $null
        }

        do {
            if($TimeoutSeconds -lt $Timer.Elapsed.TotalSeconds) {
                $this.Tasks[0].Stop()
                return $this.Tasks[0]
            }

            $id = [WaitHandle]::WaitAny($this.Tasks.AsyncResult.AsyncWaitHandle, 200)
        }
        while($id -eq [WaitHandle]::WaitTimeout)

        return $this.Tasks[$id]
    }

    [void] GetTaskResult([PSParallelTask] $Task) {
        $this.Tasks.Remove($Task)
        $this.Release($Task.GetRunspace())
        $Task.EndInvoke()

        if($Task -is [IDisposable]) {
            $Task.Dispose()
        }
    }

    [void] Release([runspace] $runspace) {
        if($this.UseNewRunspace) {
            $runspace.Dispose()
            $runspace = $this.CreateRunspace()
        }

        $this.Runspaces.Push($runspace)
    }

    [void] AddTask([PSParallelTask] $Task) {
        $this.Tasks.Add($Task)
        $Task.Run()
    }

    [void] Dispose() {
        while($runspace = $this.TryGet()) {
            $runspace.Dispose()
        }
    }
}

<#
    .SYNOPSIS
    Enables parallel processing of pipeline input objects.

    .DESCRIPTION
    PowerShell function that intends to emulate `ForEach-Object -Parallel` for those stuck with Windows PowerShell.
    This function shares similar usage and capabilities than the ones available in the built-in cmdlet.
    This project is greatly inspired by RamblingCookieMonster's `Invoke-Parallel` and Boe Prox's `PoshRSJob`.

    .PARAMETER InputObject
    Specifies the input objects to be processed in the ScriptBlock.
    Note: This parameter is intended to be bound from pipeline.

    .PARAMETER ScriptBlock
    Specifies the operation that is performed on each input object.
    This script block is run for every object in the pipeline.

    .PARAMETER ThrottleLimit
    Specifies the number of script blocks that are invoked in parallel.
    Input objects are blocked until the running script block count falls below the ThrottleLimit.
    The default value is `5`.

    .PARAMETER Variables
    Specifies a hash table of variables to have available in the Script Block (Runspaces).
    The hash table `Keys` become the Variable Name inside the Script Block.

    .PARAMETER Functions
    Existing functions in the Local Session to have available in the Script Block (Runspaces).

    .PARAMETER TimeoutSeconds
    Specifies the number of seconds to wait for all input to be processed in parallel.
    After the specified timeout time, all running scripts are stopped and any remaining input objects to be processed are ignored.

    .PARAMETER UseNewRunspace
    Uses a new runspace for each parallel invocation instead of reusing them.

    .EXAMPLE
    $message = 'Hello world from {0}'

    0..10 | Invoke-Parallel {
        $using:message -f [runspace]::DefaultRunspace.InstanceId
        Start-Sleep 3
    } -ThrottleLimit 3

    Run slow script in parallel batches.

    .EXAMPLE
    $message = 'Hello world from {0}'

    0..10 | Invoke-Parallel {
        $message -f [runspace]::DefaultRunspace.InstanceId
        Start-Sleep 3
    } -Variables @{ message = $message } -ThrottleLimit 3

    Same as Example 1 but with `-Variables` parameter.

    .EXAMPLE
    $threadSafeDictionary = [System.Collections.Concurrent.ConcurrentDictionary[string,object]]::new()

    Get-Process | Invoke-Parallel {
        $dict = $using:threadSafeDictionary
        $dict.TryAdd($_.ProcessName, $_)
    }

    $threadSafeDictionary["pwsh"]

    Adding to a single thread safe instance.

    .EXAMPLE
    $threadSafeDictionary = [System.Collections.Concurrent.ConcurrentDictionary[string,object]]::new()

    Get-Process | Invoke-Parallel {
        $dict.TryAdd($_.ProcessName, $_)
    } -Variables @{ dict = $threadSafeDictionary }

    $threadSafeDictionary["pwsh"]

    Same as Example 3, but using `-Variables` to pass the reference instance to the runspaces.

    .EXAMPLE
    function Greet { param($s) "$s hey there!" }

    0..10 | Invoke-Parallel { Greet $_ } -Functions Greet

    This example demonstrates how to pass a locally defined Function to the Runspaces scope.

    .LINK
    https://github.com/santisq/PSParallelPipeline
#>

function Invoke-Parallel {
    [CmdletBinding(PositionalBinding = $false)]
    [Alias('parallel')]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [object] $InputObject,

        [Parameter(Mandatory, Position = 0)]
        [scriptblock] $ScriptBlock,

        [Parameter()]
        [ValidateRange(1, [int]::MaxValue)]
        [int] $ThrottleLimit = 5,

        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [hashtable] $Variables,

        [Parameter()]
        [ValidateNotNullOrEmpty()]
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
        [switch] $UseNewRunspace,

        [Parameter()]
        [ValidateRange(1, [int]::MaxValue)]
        [int] $TimeoutSeconds
    )

    begin {
        try {
            $iss = [initialsessionstate]::CreateDefault2()

            foreach($key in $Variables.PSBase.Keys) {
                $iss.Variables.Add([SessionStateVariableEntry]::new($key, $Variables[$key], ''))
            }

            foreach($function in $Functions) {
                $def = (Get-Command $function).Definition
                $iss.Commands.Add([SessionStateFunctionEntry]::new($function, $def))
            }

            $usingParams = @{}

            # Thanks to mklement0 for catching up a bug here.
            # https://github.com/mklement0
            foreach($usingstatement in $ScriptBlock.Ast.FindAll({ $args[0] -is [UsingExpressionAst] }, $true)) {
                $varText = $usingstatement.Extent.Text
                $varPath = $usingstatement.SubExpression.VariablePath.UserPath

                $key = [Convert]::ToBase64String([Encoding]::Unicode.GetBytes($varText.ToLowerInvariant()))
                if(-not $usingParams.ContainsKey($key)) {
                    $usingParams.Add($key, $PSCmdlet.SessionState.PSVariable.GetValue($varPath))
                }
            }

            $im = [InvocationManager]::new($ThrottleLimit, $Host, $iss, $UseNewRunspace.IsPresent)

            if($withTimeout = $PSBoundParameters.ContainsKey('TimeoutSeconds')) {
                $timer = [Stopwatch]::StartNew()
            }
        }
        catch {
            $PSCmdlet.ThrowTerminatingError($_)
        }
    }

    process {
        try {
            do {
                if($runspace = $im.TryGet()) {
                    continue
                }

                if($withTimeout) {
                    if($task = $im.WaitAny($TimeoutSeconds, $timer)) {
                        $im.GetTaskResult($task)
                    }
                    continue
                }

                if($task = $im.WaitAny()) {
                    $im.GetTaskResult($task)
                }
            }
            until($runspace -or $TimeoutSeconds -lt $timer.Elapsed.TotalSeconds)

            if($TimeoutSeconds -lt $timer.Elapsed.TotalSeconds) {
                return
            }

            $im.AddTask(
                [PSParallelTask]::new($ScriptBlock, $InputObject, $PSCmdlet).
                AssociateWith($runspace).
                AddUsingStatements($usingParams))
        }
        catch {
            $PSCmdlet.WriteError($_)
        }
    }

    end {
        try {
            if($withTimeout) {
                while($task = $im.WaitAny($TimeoutSeconds, $timer)) {
                    $im.GetTaskResult($task)
                }
                return
            }

            while($task = $im.WaitAny()) {
                $im.GetTaskResult($task)
            }
        }
        catch {
            $PSCmdlet.WriteError($_)
        }
        finally {
            if($im -is [IDisposable]) {
                $im.Dispose()
            }
        }
    }
}