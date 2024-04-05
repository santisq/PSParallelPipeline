using namespace System.Collections.Generic
using namespace System.Diagnostics
using namespace System.Management.Automation
using namespace System.Management.Automation.Host
using namespace System.Management.Automation.Language
using namespace System.Text
using namespace System.Threading

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
        $this.ThrottleLimit = $ThrottleLimit
        $this.PSHost = $PSHost
        $this.InitialSessionState = $InitialSessionState
        $this.UseNewRunspace = $UseNewRunspace
    }

    [runspace] TryGet() {
        if ($this.Runspaces.Count) {
            return $this.Runspaces.Pop()
        }

        if ($this.TotalMade -ge $this.ThrottleLimit) {
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
        if (-not $this.Tasks.Count) {
            return $null
        }

        do {
            $id = [WaitHandle]::WaitAny($this.Tasks.AsyncResult.AsyncWaitHandle, 200)
        }
        while ($id -eq [WaitHandle]::WaitTimeout)

        return $this.Tasks[$id]
    }

    [PSParallelTask] WaitAny([int] $TimeoutSeconds, [Stopwatch] $Timer) {
        if (-not $this.Tasks.Count) {
            return $null
        }

        do {
            if ($TimeoutSeconds -lt $Timer.Elapsed.TotalSeconds) {
                $this.Tasks[0].Stop()
                return $this.Tasks[0]
            }

            $id = [WaitHandle]::WaitAny($this.Tasks.AsyncResult.AsyncWaitHandle, 200)
        }
        while ($id -eq [WaitHandle]::WaitTimeout)

        return $this.Tasks[$id]
    }

    [void] GetTaskResult([PSParallelTask] $Task) {
        try {
            $this.Tasks.Remove($Task)
            $this.Release($Task.GetRunspace())
            $Task.EndInvoke()
        }
        finally {
            if ($Task -is [IDisposable]) {
                $Task.Dispose()
            }
        }
    }

    [void] Release([runspace] $runspace) {
        if ($this.UseNewRunspace) {
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
        while ($runspace = $this.TryGet()) {
            $runspace.Dispose()
        }
    }

    static [hashtable] GetUsingStatements([scriptblock] $scriptblock, [PSCmdlet] $cmdlet) {
        $usingParams = @{}
        foreach ($usingstatement in $scriptblock.Ast.FindAll({ $args[0] -is [UsingExpressionAst] }, $true)) {
            $variableAst = [UsingExpressionAst]::ExtractUsingVariable($usingstatement)
            $varPath = $variableAst.VariablePath.UserPath
            $varText = $usingstatement.ToString()

            if ($usingstatement.SubExpression -is [VariableExpressionAst]) {
                $varText = $varText.ToLowerInvariant()
            }

            $key = [Convert]::ToBase64String([Encoding]::Unicode.GetBytes($varText))

            if ($usingParams.ContainsKey($key)) {
                continue
            }

            $value = $cmdlet.SessionState.PSVariable.GetValue($varPath)

            if ($value -is [scriptblock]) {
                $cmdlet.ThrowTerminatingError([ErrorRecord]::new(
                    [PSArgumentException]::new('Passed-in script block variables are not supported.'),
                    'VariableCannotBeScriptBlock',
                    [ErrorCategory]::InvalidType,
                    $value))
            }

            if ($usingstatement.SubExpression -isnot [VariableExpressionAst]) {
                [Stack[Ast]] $subexpressionStack = $usingstatement.SubExpression.FindAll({
                        $args[0] -is [IndexExpressionAst] -or
                        $args[0] -is [MemberExpressionAst] },
                    $false)

                while ($subexpressionStack.Count) {
                    $subexpression = $subexpressionStack.Pop()
                    if ($subexpression -is [IndexExpressionAst]) {
                        $idx = $subexpression.Index.SafeGetValue()
                        $value = $value[$idx]
                        continue
                    }

                    if ($subexpression -is [MemberExpressionAst]) {
                        $member = $subexpression.Member.SafeGetValue()
                        $value = $value.$member
                    }
                }
            }

            $usingParams.Add($key, $value)
        }

        return $usingParams
    }
}
