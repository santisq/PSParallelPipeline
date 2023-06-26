
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

