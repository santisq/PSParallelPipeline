<h1 align="center">PSParallelPipeline</h1>

## SYNOPSIS

Enables parallel processing of pipeline input objects.

## SYNTAX

```powershell
Invoke-Parallel -InputObject <Object> [-ScriptBlock] <ScriptBlock> [-ThrottleLimit <Int32>]
 [-Variables <Hashtable>] [-Functions <String[]>] [-ThreadOptions <PSThreadOptions>] [<CommonParameters>]
```

## DESCRIPTION

PowerShell function that intends to emulate [`ForEach-Object -Parallel`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/foreach-object?view=powershell-7.2#-parallel) for those stuck with Windows PowerShell. This function shares similar usage and capabilities than the ones available in the built-in cmdlet.

This project is greatly inspired by RamblingCookieMonster's [`Invoke-Parallel`](https://github.com/RamblingCookieMonster/Invoke-Parallel) and Boe Prox's [`PoshRSJob`](https://github.com/proxb/PoshRSJob) and is merely a simplified take on those with some few improvements.

## REQUIREMENTS

Compatible with __Windows PowerShell 5.1__ and __PowerShell Core 7+__.

## INSTALLATION

```powershell
Install-Module PSParallelPipeline -Scope CurrentUser
```

## EXAMPLES

### EXAMPLE 1: Run slow script in parallel batches

```powershell
$message = 'Hello world from {0}'

0..10 | Invoke-Parallel {
    $using:message -f [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 3
} -ThrottleLimit 3
```

### EXAMPLE 2: Same as previous example but with `-Variables` parameter

```powershell
$message = 'Hello world from {0}'

0..10 | Invoke-Parallel {
    $message -f [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 3
} -Variables @{ message = $message } -ThrottleLimit 3
```

### EXAMPLE 3: Adding to a single thread safe instance

```powershell
$sync = [hashtable]::Synchronized(@{})

Get-Process | Invoke-Parallel {
    $sync = $using:sync
    $sync[$_.Name] += @( $_ )
}

$sync
```

### EXAMPLE 4: Same as previous example but using `-Variables` to pass the reference instance to the Runspaces

This method is the recommended when passing reference instances to the runspaces, `$using:` may fail in some situations.

```powershell
$sync = [hashtable]::Synchronized(@{})

Get-Process | Invoke-Parallel {
    $sync[$_.Name] += @( $_ )
} -Variables @{ sync = $sync }

$sync
```

### EXAMPLE 5: Demonstrates how to pass a locally defined Function to the Runspace scope

```powershell
function Greet { param($s) "$s hey there!" }

0..10 | Invoke-Parallel {
    Greet $_
} -Functions Greet
```

## PARAMETERS

### -InputObject

Specifies the input objects to be processed in the ScriptBlock.
<br>__Note: This parameter is intended to be bound from pipeline.__

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
<br>This script block is run for every object in the pipeline.

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
<br>Input objects are blocked until the running script block count falls below the ThrottleLimit.
<br>The default value is `5`.

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

### -Variables

Specifies a hash table of variables to have available in the Script Block (Runspaces).
The hash table Keys become the Variable Name inside the Script Block.

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

### -ThreadOptions

These options control whether a new thread is created when a command is executed within a Runspace.
<br>This parameter is limited to `ReuseThread` and `UseNewThread`. Default value is `ReuseThread`.
<br>See [PSThreadOptions Enum](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.runspaces.psthreadoptions?view=powershellsdk-7.2.0) for details.

```yaml
Type: PSThreadOptions
Parameter Sets: (All)
Aliases:
Accepted values: Default, UseNewThread, ReuseThread, UseCurrentThread

Required: False
Position: Named
Default value: ReuseThread
Accept pipeline input: False
Accept wildcard characters: False
```
