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

`Invoke-Parallel` is a PowerShell cmdlet that allows parallel processing of input objects with similar capabilities as
[`ForEach-Object -Parallel`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/foreach-object) introduced in PowerShell v7.0.

## EXAMPLES

### Example 1: Run slow script in parallel batches

```powershell
$message = 'Hello world from '

0..10 | Invoke-Parallel {
    $using:message + [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 3
}
```

### Example 2: Demonstrates `-Variables` Parameter

```powershell
$message = 'Hello world from '

0..10 | Invoke-Parallel {
    $message + [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 3
} -Variables @{ message = $message }
```

[`-Variables`](#-variables) specifies a hashtable with key / value pairs of variables to pass-in to the parallel scope. The hashtable keys defines the name for passed-in variables. This parameter is an alternative for the `$using:` scope modifier.

### Example 3: Adding to a single thread safe instance with `$using:` scope modifier

```powershell
$dict = [System.Collections.Concurrent.ConcurrentDictionary[int, object]]::new()
Get-Process | Invoke-Parallel { ($using:dict)[$_.Id] = $_ }
$dict[$PID]
```

### Example 4: Adding to a single thread safe instance using `-Variables`

```powershell
$dict = [System.Collections.Concurrent.ConcurrentDictionary[int, object]]::new()
Get-Process | Invoke-Parallel { $dict[$_.Id] = $_ } -Variables @{ dict = $dict }
$dict[$PID]
```

### Example 5: Demonstrates `-Functions` Parameter

```powershell
function Greet { param($s) "$s hey there!" }

0..10 | Invoke-Parallel { Greet $_ } -Functions Greet
```

[`-Functions`](#-functions) adds locally defined functions to the runspaces [Initial Session State](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.runspaces.initialsessionstate) allowing you to use them in the parallel scope.

### Example 6: Demonstrates `-TimeoutSeconds` Parameter

```powershell
0..10 | Invoke-Parallel { Start-Sleep 1 } -TimeoutSeconds 3
```

All parallel invocations are stopped when the timeout is reached and any remaining input objects to be processed are ignored.

### Example 7: Demonstrates `-UseNewRunspace` Parameter

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

By default the runspaces are reused. With `-UseNewRunspace` a new runspace is created per input object.

## PARAMETERS

### -Functions

Specifies existing functions in the Local Session to have added to the runspaces [Initial Session State](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.runspaces.initialsessionstate).

> [!TIP]
>
> This method is the recommended way of passing-in local functions to the parallel scope. The alternative to this method is passing-in the function definition (as a string) to the parallel scope and define the function in it.

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

Specifies the input objects to be processed in the ScriptBlock.

> [!NOTE]
>
> This parameter is intended to be bound from pipeline.

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

Specifies the operation that is performed on each input object.
This script block is run for every object in the pipeline.

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

Specifies the number of script blocks that are invoked in parallel (Degree of Parallelism).
Input objects are blocked until the running script block count falls below the ThrottleLimit.

> [!NOTE]
>
> `-ThrottleLimit` default value is `5`.

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

Specifies the number of seconds to wait for all input to be processed in parallel.
After the specified timeout time, all running scripts are stopped and any remaining input objects to be processed are ignored.

> [!NOTE]
>
> Default value of `0` disables the timeout and the cmdlet runs until all pipeline input is processed.

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

Uses a new runspace for each parallel invocation instead of reusing them.

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

Specifies a hashtable of variables to have available in the parallel scope.
The hashtable keys defines the name for passed-in variables.

> [!TIP]
>
> This parameter is an alternative for the [`$using:` scope modifier](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_scopes?view=powershell-7.4#scope-modifiers).

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

### Object

You can pipe any object to this cmdlet.

## OUTPUTS

### Object

This cmdlet returns objects that are determined by the script block.
