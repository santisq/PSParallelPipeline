using namespace System.Management.Automation
using namespace System.Management.Automation.Language
using namespace System.Management.Automation.Runspaces
using namespace System.Text

# .ExternalHelp PSParallelPipeline-help.xml
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
        [ArgumentCompleter([CommandCompleter])]
        [string[]] $Functions,

        [Parameter()]
        [switch] $UseNewRunspace,

        [Parameter()]
        [ValidateRange(1, [int]::MaxValue)]
        [int] $TimeoutSeconds
    )

    begin {
        $usingParams = [InvocationManager]::GetUsingStatements(
            $ScriptBlock,
            $PSCmdlet)

        try {
            $iss = [initialsessionstate]::CreateDefault2()

            foreach ($key in $Variables.PSBase.Keys) {
                if ($Variables[$key] -is [scriptblock]) {
                    $PSCmdlet.ThrowTerminatingError([ErrorRecord]::new(
                        [PSArgumentException]::new('Passed-in script block variables are not supported.'),
                        'VariableCannotBeScriptBlock',
                        [ErrorCategory]::InvalidType,
                        $Variables[$key]))
                }

                $iss.Variables.Add(
                    [SessionStateVariableEntry]::new($key, $Variables[$key], ''))
            }

            foreach ($function in $Functions) {
                $def = $PSCmdlet.InvokeCommand.GetCommand(
                    $function, [CommandTypes]::Function)

                $iss.Commands.Add(
                    [SessionStateFunctionEntry]::new($function, $def.Definition))
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

            $im.AddTask([PSParallelTask]::new($ScriptBlock, $InputObject, $PSCmdlet).
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
