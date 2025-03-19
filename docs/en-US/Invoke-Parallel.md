---
external help file: PSParallelPipeline-help.xml
Module Name: PSParallelPipeline
online version: https://github.com/santisq/PSParallelPipeline
schema: 2.0.0
---

# Invoke-Parallel

## SYNOPSIS

Parallel processing of pipeline input objects.

## SYNTAX

```powershell
Invoke-Parallel
    -InputObject <Object>
    [-ScriptBlock] <ScriptBlock>
    [-ThrottleLimit <Int32>]
    [-Variables <Hashtable>]
    [-Functions <String[]>]
    [-UseNewRunspace]
    [-TimeoutSeconds <Int32>]
    [<CommonParameters>]
```

## DESCRIPTION

The `Invoke-Parallel` cmdlet enables parallel processing of input objects in PowerShell, offering functionality similar to `ForEach-Object -Parallel` introduced in PowerShell 7.0. It processes pipeline input across multiple threads, improving performance for tasks that benefit from parallel execution.

## EXAMPLES

### Example 1: Run a slow script in parallel batches

```powershell
$message = 'Hello world from '

0..10 | Invoke-Parallel {
    $using:message + [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 3
}
```

This example demonstrates parallel execution of a script block with a 3-second delay, appending a unique runspace ID to a message. The [`$using:` scope modifier](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_scopes?view=powershell-7.4#the-using-scope-modifier) is used to pass the local variable `$message` into the parallel scope, a supported method for accessing external variables in `Invoke-Parallel`.

### Example 2: Demonstrates `-Variables` Parameter

```powershell
$message = 'Hello world from '

0..10 | Invoke-Parallel {
    $message + [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 3
} -Variables @{ message = $message }
```

This example demonstrates the [`-Variables` parameter](#-variables), which passes the local variable `$message` into the parallel scope using a hashtable. The key `message` in the hashtable defines the variable name available within the script block, serving as an alternative to the `$using:` scope modifier.

### Example 3: Adding to a thread-safe collection with `$using:`

```powershell
$dict = [System.Collections.Concurrent.ConcurrentDictionary[int, object]]::new()
Get-Process | Invoke-Parallel { ($using:dict)[$_.Id] = $_ }
$dict[$PID]
```

This example uses a thread-safe dictionary to store process objects by ID, leveraging the `$using:` modifier for variable access.

### Example 4: Adding to a thread-safe collection with `-Variables`

```powershell
$dict = [System.Collections.Concurrent.ConcurrentDictionary[int, object]]::new()
Get-Process | Invoke-Parallel { $dict[$_.Id] = $_ } -Variables @{ dict = $dict }
$dict[$PID]
```

Similar to Example 3, this demonstrates the same functionality using `-Variables` instead of `$using:`.

### Example 5: Using the `-Functions` parameter

```powershell
function Greet { param($s) "$s hey there!" }

0..10 | Invoke-Parallel { Greet $_ } -Functions Greet
```

This example imports a local function `Greet` into the parallel scope using [`-Functions` parameter](#-functions), allowing its use within the script block.

### Example 6: Setting a timeout with `-TimeoutSeconds`

```powershell
0..10 | Invoke-Parallel { Start-Sleep 1 } -TimeoutSeconds 3
```

This example limits execution to 3 seconds, stopping all running script blocks and ignoring unprocessed input once the timeout is reached.

### Example 7: Creating new runspaces with `-UseNewRunspace`

```powershell
0..3 | Invoke-Parallel { [runspace]::DefaultRunspace.InstanceId } -ThrottleLimit 2

# Guid
# ----
# c945ae1f-4e66-4312-b23c-f3994965308e
# 1c6af45c-8727-4488-937a-4dfc1d259e9e
# c945ae1f-4e66-4312-b23c-f3994965308e
# 1c6af45c-8727-4488-937a-4dfc1d259e9e

0..3 | Invoke-Parallel { [runspace]::DefaultRunspace.InstanceId } -ThrottleLimit 2 -UseNewRunspace

# Guid
# ----
# 7a1c3871-6ce2-4b7f-ae90-fb1e92cd9678
# 2488be9e-15fe-4be2-882d-7d98b068c913
# d3dd7b5d-e7e3-457f-b6fb-def35fe837d7
# 9af7c222-061d-4c89-b073-375ee925e538
```

This example contrasts default runspace reuse with the `-UseNewRunspace` switch, showing unique runspace IDs for each invocation in the latter case.

## PARAMETERS

### -Functions

Specifies an array of function names from the local session to include in the runspaces' [Initial Session State](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.runspaces.initialsessionstate). This enables their use within the parallel script block.

> [!TIP]
>
> This parameter is the recommended way to make local functions available in the parallel scope. Alternatively, you can retrieve the function definition as a string (e.g., `$def = ${function:Greet}.ToString()`) and use `$using:` to pass it into the script block, defining it there (e.g., `${function:Greet} = $using:def`).

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: funcs

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject

Specifies the objects to process in the script block. This parameter accepts pipeline input.

```yaml
Type: Object
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -ScriptBlock

Defines the script block executed for each input object in parallel.

```yaml
Type: ScriptBlock
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ThrottleLimit

Sets the maximum number of script blocks executed in parallel across multiple threads. Additional input objects wait until the number of running script blocks falls below this limit. The default value is `5`.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases: tl

Required: False
Position: Named
Default value: 5
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeoutSeconds

Specifies the maximum time (in seconds) to process all input objects. When the timeout is reached, running script blocks are terminated, and remaining input is discarded.

> [!NOTE]
>
> A value of `0` (default) disables the timeout, allowing processing to continue until completion.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases: to

Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -UseNewRunspace

Uses a new runspace for each parallel invocation instead of reusing existing runspaces in the runspace pool.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: unr

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Variables

Provides a hashtable of variables to make available in the parallel scope. Keys define the variable names within the script block.

> [!TIP]
>
> Use this parameter as an alternative to the [`$using:` scope modifier](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_scopes?view=powershell-7.4#scope-modifiers).

```yaml
Type: Hashtable
Parameter Sets: (All)
Aliases: vars

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Object

You can pipe any object to this cmdlet.

## OUTPUTS

### System.Object

Returns objects produced by the script block.

## NOTES

- `Invoke-Parallel` uses multithreading, which may introduce overhead. For small datasets, sequential processing might be faster.
- Ensure variables or collections passed to the parallel scope are thread-safe (e.g., `[System.Collections.Concurrent.ConcurrentDictionary]`), as shown in Examples 3 and 4.
- By default, runspaces are reused from a pool to optimize resource usage. Using `-UseNewRunspace` increases memory and startup time but ensures isolation.

## RELATED LINKS

Online Version

[__ForEach-Object -Parallel__](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/foreach-object)

[__Runspaces Overview__](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.runspaces.runspace?view=powershellsdk-7.4.0)

[__Managed threading best practices__](https://learn.microsoft.com/en-us/dotnet/standard/threading/managed-threading-best-practices)

[__Thread-safe collections__](https://learn.microsoft.com/en-us/dotnet/standard/collections/thread-safe/)
