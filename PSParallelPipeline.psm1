using namespace System.Collections.Generic
using namespace System.Management.Automation
using namespace System.Management.Automation.Runspaces
using namespace System.Management.Automation.Language
using namespace System.Text

function Invoke-Parallel {
    <#
    .SYNOPSIS
    Enables parallel processing of pipeline input objects.

    .DESCRIPTION
    PowerShell function that intends to emulate `ForEach-Object -Parallel` functionality in PowerShell Core for those stuck with Windows PowerShell.
    This function shares similar usage and capabilities than the ones available in the built-in cmdlet.
    This was inspired by RamblingCookieMonster's [`Invoke-Parallel`](https://github.com/RamblingCookieMonster/Invoke-Parallel) and Boe Prox's [`PoshRSJob`](https://github.com/proxb/PoshRSJob)
    and is merely a simplified take on those.

    TO DO:
        - Add `-TimeoutSeconds` parameter.

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

    .PARAMETER ArgumentList
    Specifies a hashtable of values to be passed to the Runspaces.
    Hashtable Keys become the Variable Name inside the ScriptBlock.

    .PARAMETER ThreadOptions
    These options control whether a new thread is created when a command is executed within a Runspace.
    See [`PSThreadOptions` Enum](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.runspaces.psthreadoptions?view=powershellsdk-7.2.0).
    Default value is `ReuseThread`.

    .PARAMETER Functions
    Existing functions in the current scope that we want to have available in the Runspaces.

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
    } -ArgumentList @{ message = $message } -ThrottleLimit 3

    Same as Example 1 but with `-ArgumentList` parameter.

    .EXAMPLE
    $sync = [hashtable]::Synchronized(@{})

    Get-Process | Invoke-Parallel {
        $sync = $using:sync
        $sync[$_.Name] += @( $_ )
    } -ThrottleLimit 5

    $sync

    Adding to a single thread safe instance.

    .EXAMPLE
    $sync = [hashtable]::Synchronized(@{})

    Get-Process | Invoke-Parallel {
        $sync = $using:sync
        $sync[$_.Name] += @( $_ )
    } -ThrottleLimit 5

    $sync

    Same as Example 3, but using `-ArgumentList` to pass the reference instance to the runspaces.
    This method is the recommended when passing reference instances to the runspaces, `$using:` may fail in some situations.

    .EXAMPLE
    function Greet { param($s) "$s hey there!" }

    0..10 | Invoke-Parallel {
        Greet $_
    } -Functions Greet

    This example demonstrates how to pass a locally defined Function to the Runspaces scope.

    .LINK
    https://github.com/santisq/PSParallelPipeline
    #>

    [CmdletBinding(PositionalBinding = $false)]
    [Alias('parallel')]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [object] $InputObject,

        [Parameter(Mandatory, Position = 0)]
        [scriptblock] $ScriptBlock,

        [Parameter()]
        [int] $ThrottleLimit = 5,

        [Parameter()]
        [hashtable] $ArgumentList,

        [Parameter()]
        [PSThreadOptions] $ThreadOptions = [PSThreadOptions]::ReuseThread,

        [Parameter()]
        [ArgumentCompleter({
            param(
                [string] $commandName,
                [string] $parameterName,
                [string] $wordToComplete
            )

            (Get-Command -CommandType Filter, Function).Name -like "$wordToComplete*"
        })]
        [string[]] $Functions
    )

    begin {
        try {
            $iss = [initialsessionstate]::CreateDefault2()

            foreach($key in $ArgumentList.PSBase.Keys) {
                $iss.Variables.Add([SessionStateVariableEntry]::new($key, $ArgumentList[$key], ''))
            }

            foreach($function in $Functions) {
                $def = (Get-Command $function).Definition
                $iss.Commands.Add([SessionStateFunctionEntry]::new($function, $def))
            }

            $usingParams = @{}

            foreach($usingstatement in $ScriptBlock.Ast.FindAll({ $args[0] -is [UsingExpressionAst] }, $true)) {
                $varText = $usingstatement.Extent.Text
                $varPath = $usingstatement.SubExpression.VariablePath.UserPath

                if(-not $usingParams.ContainsKey($varText)) {
                    $key = [Convert]::ToBase64String([Encoding]::Unicode.GetBytes($varText))
                    $usingParams[$key] = $ExecutionContext.SessionState.PSVariable.Get($varPath).Value
                }
            }

            $pool  = [runspacefactory]::CreateRunspacePool(1, $ThrottleLimit, $iss, $Host)
            $tasks = [List[hashtable]]::new()
            $pool.ThreadOptions = $ThreadOptions
            $pool.Open()
        }
        catch {
            $PSCmdlet.ThrowTerminatingError($_)
        }
    }
    process {
        try {
            # Thanks to Patrick Meinecke for his help here.
            # https://github.com/SeeminglyScience/
            $ps = [powershell]::Create().AddScript({
                $args[0].InvokeWithContext($null, [psvariable]::new('_', $args[1]))
            }).AddArgument($ScriptBlock.Ast.GetScriptBlock()).AddArgument($InputObject)

            # This is how `Start-Job` does it's magic. Credits to Jordan Borean for his help here.
            # https://github.com/jborean93

            # Reference in the source code:
            # https://github.com/PowerShell/PowerShell/blob/7dc4587014bfa22919c933607bf564f0ba53db2e/src/System.Management.Automation/engine/ParameterBinderController.cs#L647-L653
            if($usingParams.Count) {
                $null = $ps.AddParameters(@{ '--%' = $usingParams })
            }

            $ps.RunspacePool = $pool

            $tasks.Add(@{
                Instance    = $ps
                AsyncResult = $ps.BeginInvoke()
            })
        }
        catch {
            $PSCmdlet.WriteError($_)
        }
    }
    end {
        try {
            foreach($task in $tasks) {
                $task['Instance'].EndInvoke($task['AsyncResult'])

                if($task['Instance'].HadErrors) {
                    $task['Instance'].Streams.Error
                }
            }
        }
        catch {
            $PSCmdlet.WriteError($_)
        }
        finally {
            $tasks.Instance, $pool | ForEach-Object Dispose
        }
    }
}