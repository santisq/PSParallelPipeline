---
external help file: PSParallelPipeline-help.xml
Module Name: PSParallelPipeline
online version: https://github.com/santisq/PSParallelPipeline
schema: 2.0.0
---

# Invoke-Parallel

## SYNOPSIS

Enables parallel processing of pipeline input objects.

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

`Invoke-Parallel` is a PowerShell function that allows parallel processing of input objects with similar capabilities as
[`ForEach-Object`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/foreach-object) with its `-Parallel` parameter.

This function is mostly intended for users of Windows PowerShell 5.1 though fully compatible with newer versions of PowerShell.

## EXAMPLES

### EXAMPLE 1: Run slow script in parallel batches

```powershell
$message = 'Hello world from {0}'

0..10 | Invoke-Parallel {
    $using:message -f [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 3
} -ThrottleLimit 3
```

### Example 2: Demonstrates how `-Variables` parameter works

```powershell
$message = 'Hello world from {0}'

0..10 | Invoke-Parallel {
    $message -f [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 3
} -Variables @{ message = $message } -ThrottleLimit 3
```

The `-Variables` parameter allows to pass variables to the parallel runspaces. The hash table `Keys` become the Variable Name inside the Script Block.

### Example 3: Adding to a single thread safe instance

```powershell
$threadSafeDictionary = [System.Collections.Concurrent.ConcurrentDictionary[string,object]]::new()

Get-Process | Invoke-Parallel {
    $dict = $using:threadSafeDictionary
    $dict.TryAdd($_.ProcessName, $_)
}

$threadSafeDictionary["pwsh"]
```

### Example 4: Adding to a single thread safe instance using `-Variables` parameter

```powershell
$threadSafeDictionary = [System.Collections.Concurrent.ConcurrentDictionary[string,object]]::new()

Get-Process | Invoke-Parallel {
    $dict.TryAdd($_.ProcessName, $_)
} -Variables @{ dict = $threadSafeDictionary }

$threadSafeDictionary["pwsh"]
```

### Example 5: Passing a locally defined Function to the parallel scope

```powershell
function Greet { param($s) "$s hey there!" }

0..10 | Invoke-Parallel { Greet $_ } -Functions Greet
```

This example demonstrates how to pass a locally defined Function to the Runspaces scope.

### Example 6: Setting a timeout for parallel tasks

```powershell
Get-Process | Invoke-Parallel {
    Get-Random -Maximum 4 | Start-Sleep
    $_
} -ThrottleLimit 10 -TimeoutSeconds 4
```

If the timeout in seconds is reached all parallel invocations are stopped.

### Example 7: Using a new runspace for each invocation

```powershell
0..5 | Invoke-Parallel { [runspace]::DefaultRunspace.InstanceId }

Guid
----
ca9e3ff2-1eb0-4911-a288-838574fc7cb2
775c65bd-5267-4ecb-943c-a1a1788d1116
0cffb831-8e41-44b6-9ad8-5c9acfca64ce
e5bc6cce-6cab-4d44-83e5-d947ab56ca15
b7a9ba07-ad6d-4097-9224-3d87c10c01d7
ca9e3ff2-1eb0-4911-a288-838574fc7cb2

0..5 | Invoke-Parallel { [runspace]::DefaultRunspace.InstanceId } -UseNewRunspace

Guid
----
e4047803-0ee7-43e3-b195-c5a456db0cee
3344f9f5-7b02-4926-b69e-313830cf4ee2
ac22866a-7a41-4c24-b31c-47155054022f
d5be0085-6f80-49c6-9a31-e50f1960329d
80405d89-87fb-47f0-b6ba-a59392a99b6f
3b78d3de-5759-4364-85df-dc72427e6af8
```

By default the runspaces are reused. When the `-UseNewRunspace` parameter is used each parallel invocation will create a new runspace.

## PARAMETERS

### -Functions

Existing functions in the Local Session to have available in the Script Block (Runspaces).

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject

Specifies the input objects to be processed in the ScriptBlock.

> [!NOTE]
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
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ThrottleLimit

Specifies the number of script blocks that are invoked in parallel.
Input objects are blocked until the running script block count falls below the ThrottleLimit.

> [!NOTE]
> `-ThrottleLimit` default value is `5`.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 5
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeoutSeconds

Specifies the number of seconds to wait for all input to be processed in parallel.
After the specified timeout time, all running scripts are stopped and any remaining input objects to be processed are ignored.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

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
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Variables

Specifies a hash table of variables to have available in the Script Block (Runspaces).
The hash table `Keys` become the Variable Name inside the Script Block.

```yaml
Type: Hashtable
Parameter Sets: (All)
Aliases:

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

This cmdlet returns objects that are determined by the input.
