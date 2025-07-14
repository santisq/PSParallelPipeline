---
external help file: PSParallelPipeline-help.xml
Module Name: PSParallelPipeline
online version: https://github.com/santisq/PSParallelPipeline
schema: 2.0.0
---

# Invoke-Parallel

## SYNOPSIS

Executes parallel processing of pipeline input objects using multithreading.

## SYNTAX

```powershell
Invoke-Parallel
    [-ScriptBlock] <ScriptBlock>
    [-InputObject <Object>]
    [-ThrottleLimit <Int32>]
    [-TimeoutSeconds <Int32>]
    [-Variables <Hashtable>]
    [-Functions <String[]>]
    [-ModuleNames <String[]>]
    [-ModulePaths <String[]>]
    [-UseNewRunspace]
    [<CommonParameters>]
```

## DESCRIPTION

The `Invoke-Parallel` cmdlet enables parallel processing of input objects in PowerShell, including
__Windows PowerShell 5.1__ and PowerShell 7+, offering functionality similar to `ForEach-Object -Parallel` introduced in
PowerShell 7.0. It processes pipeline input across multiple threads, improving performance for tasks that benefit from
parallel execution.

## EXAMPLES

### Example 1: Run a slow script in parallel batches

```powershell
$message = 'Hello world from '

0..10 | Invoke-Parallel {
    $using:message + [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 3
}
```

This example demonstrates parallel execution of a script block with a 3-second delay, appending a unique runspace ID to
a message. The [`$using:` scope modifier](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_scopes#the-using-scope-modifier)
is used to pass the local variable `$message` into the parallel scope, a supported method for accessing external
variables in `Invoke-Parallel`.

### Example 2: Demonstrates `-Variables` Parameter

```powershell
$message = 'Hello world from '

0..10 | Invoke-Parallel {
    $message + [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 3
} -Variables @{ message = $message }
```

This example demonstrates the [`-Variables` parameter](#-variables), which passes the local variable `$message` into
the parallel scope using a hashtable. The key `message` in the hashtable defines the variable name available within the
script block, serving as an alternative to the `$using:` scope modifier.

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

### Example 8: Using the `-ModuleNames` parameter

```powershell
Import-Csv users.csv | Invoke-Parallel { Get-ADUser $_.UserPrincipalName } -ModuleNames ActiveDirectory
```

This example imports the `ActiveDirectory` module into the parallel scope using `-ModuleNames`, enabling the
`Get-ADUser` cmdlet within the script block.

### Example 9: Using the `-ModulePaths` parameter

```powershell
$moduleDir = Join-Path $PSScriptRoot "CustomModule"
0..10 | Invoke-Parallel { Get-CustomCmdlet } -ModulePaths $moduleDir
```

This example imports a custom module from the specified directory using `-ModulePaths`, allowing the `Get-CustomCmdlet` function to be used in the parallel script block.

> [!NOTE]
>
> The path must point to a directory containing a valid PowerShell module.

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

Sets the maximum number of script blocks executed in parallel across multiple threads. Additional input objects wait until the number of running script blocks falls below this limit.

> [!NOTE]
>
> The default value is `5`.

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

### -ModuleNames

Specifies an array of module names to import into the runspaces'
[Initial Session State](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.runspaces.initialsessionstate).
This allows the script block to use cmdlets and functions from the specified modules.

> [!TIP]
>
> Use this parameter to ensure required modules are available in the parallel scope. Module names must be discoverable
via the [`$env:PSModulePath`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_psmodulepath)
environment variable, which lists installed module locations.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: mn

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ModulePaths

Specifies an array of file paths to directories containing PowerShell modules (e.g., `.psm1` or `.psd1` files) to import
into the runspaces' [Initial Session State](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.runspaces.initialsessionstate).
This enables the script block to use cmdlets and functions from custom or local modules.

> [!NOTE]
>
> Paths must be absolute or relative to the current working directory and must point to valid directories containing
PowerShell modules. If an invalid path (e.g., a file or non-existent directory) is provided, a terminating error is
thrown:  
> `"The specified path '{path}' does not exist or is not a directory. The path must be a valid directory containing one
> or more PowerShell modules."`

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: mp

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
- Ensure variables or collections passed to the parallel scope are thread-safe (e.g., use
`[System.Collections.Concurrent.ConcurrentDictionary]` or similar), as shown in Examples
[3](#example-3-adding-to-a-thread-safe-collection-with-using) and [4](#example-4-adding-to-a-thread-safe-collection-with--variables),
to avoid race conditions.
- By default, runspaces are reused from a pool to optimize resource usage. Using `-UseNewRunspace` increases memory and
startup time but ensures isolation.

## RELATED LINKS

[__ForEach-Object -Parallel__](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/foreach-object)

[__Runspaces Overview__](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.runspaces.runspace?view=powershellsdk-7.4.0)

[__Managed threading best practices__](https://learn.microsoft.com/en-us/dotnet/standard/threading/managed-threading-best-practices)

[__Thread-safe collections__](https://learn.microsoft.com/en-us/dotnet/standard/collections/thread-safe/)
