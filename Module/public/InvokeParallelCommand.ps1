function Invoke-Parallel {
    [CmdletBinding(PositionalBinding = $false)]
    [Alias('parallel')]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [object] $InputObject,

        [Parameter(Mandatory, Position = 0)]
        [scriptblock] $ScriptBlock,

        [Parameter()]
        [ValidateRange(1, 63)]
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

            foreach ($key in $Variables.PSBase.Keys) {
                $iss.Variables.Add([SessionStateVariableEntry]::new($key, $Variables[$key], ''))
            }

            foreach ($function in $Functions) {
                $def = (Get-Command $function).Definition
                $iss.Commands.Add([SessionStateFunctionEntry]::new($function, $def))
            }

            $usingParams = @{}

            # Thanks to mklement0 for catching up a bug here.
            # https://github.com/mklement0
            foreach ($usingstatement in $ScriptBlock.Ast.FindAll({ $args[0] -is [UsingExpressionAst] }, $true)) {
                $varText = $usingstatement.Extent.Text
                $varPath = $usingstatement.SubExpression.VariablePath.UserPath

                $key = [Convert]::ToBase64String([Encoding]::Unicode.GetBytes($varText.ToLowerInvariant()))
                if (-not $usingParams.ContainsKey($key)) {
                    $usingParams.Add($key, $PSCmdlet.SessionState.PSVariable.GetValue($varPath))
                }
            }

            $im = [InvocationManager]::new($ThrottleLimit, $Host, $iss, $UseNewRunspace.IsPresent)

            if ($withTimeout = $PSBoundParameters.ContainsKey('TimeoutSeconds')) {
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
                if ($runspace = $im.TryGet()) {
                    continue
                }

                if ($withTimeout) {
                    if ($task = $im.WaitAny($TimeoutSeconds, $timer)) {
                        $im.GetTaskResult($task)
                    }
                    continue
                }

                if ($task = $im.WaitAny()) {
                    $im.GetTaskResult($task)
                }
            }
            until($runspace -or $TimeoutSeconds -lt $timer.Elapsed.TotalSeconds)

            if ($TimeoutSeconds -lt $timer.Elapsed.TotalSeconds) {
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
            if ($withTimeout) {
                while ($task = $im.WaitAny($TimeoutSeconds, $timer)) {
                    $im.GetTaskResult($task)
                }
                return
            }

            while ($task = $im.WaitAny()) {
                $im.GetTaskResult($task)
            }
        }
        catch {
            $PSCmdlet.WriteError($_)
        }
        finally {
            if ($im -is [IDisposable]) {
                $im.Dispose()
            }
        }
    }
}