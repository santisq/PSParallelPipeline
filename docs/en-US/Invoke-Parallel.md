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
Invoke-Parallel -InputObject <Object> [-ScriptBlock] <ScriptBlock> [-ThrottleLimit <Int32>]
 [-Variables <Hashtable>] [-Functions <String[]>] [-UseNewRunspace] [-TimeoutSeconds <Int32>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION

`Invoke-Parallel` is a PowerShell function that intends to emulate [`ForEach-Object -Parallel`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/foreach-object?view=powershell-7.2#-parallel) for those stuck with Windows PowerShell. This function shares similar usage and capabilities than the ones available in the built-in cmdlet.

This project is greatly inspired by RamblingCookieMonster's [`Invoke-Parallel`](https://github.com/RamblingCookieMonster/Invoke-Parallel) and Boe Prox's [`PoshRSJob`](https://github.com/proxb/PoshRSJob).

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

__Note: This parameter is intended to be bound from pipeline.__

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
__The default value is 5__.

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

### PSObject

You can pipe any object to this cmdlet.

## OUTPUTS

### PSObject

This cmdlet returns objects that are determined by the input.
