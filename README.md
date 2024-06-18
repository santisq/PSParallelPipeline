<h1 align="center">PSParallelPipeline</h1>
<div align="center">
<sub>Parallel processing of pipeline input objects!</sub>
<br /><br />

[![build](https://github.com/santisq/PSParallelPipeline/actions/workflows/ci.yml/badge.svg)](https://github.com/santisq/PSParallelPipeline/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/santisq/PSParallelPipeline/branch/main/graph/badge.svg?token=b51IOhpLfQ)](https://codecov.io/gh/santisq/PSParallelPipeline)
[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/PSParallelPipeline?label=gallery)](https://www.powershellgallery.com/packages/PSParallelPipeline)
[![LICENSE](https://img.shields.io/github/license/santisq/PSParallelPipeline)](https://github.com/santisq/PSParallelPipeline/blob/main/LICENSE)

</div>

PSParallelPipeline is a PowerShell Module that includes `Invoke-Parallel`, a cmdlet that allows for parallel processing of input objects, sharing similar capabilities as
[`ForEach-Object -Parallel`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/foreach-object) introduced in PowerShell v7.0.

This project was inspired by RamblingCookieMonster's [`Invoke-Parallel`](https://github.com/RamblingCookieMonster/Invoke-Parallel) and is developed with Windows PowerShell 5.1 users in mind where the closest there is to parallel pipeline processing is [`Start-ThreadJob`](https://learn.microsoft.com/en-us/powershell/module/threadjob/start-threadjob?view=powershell-7.4).

## What does this Module have to offer?

Except for [`-AsJob`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/foreach-object?view=powershell-7.4#-asjob), this module offers the same capabilities as `ForEach-Object -Parallel` in addition to supporting [Common Parameters](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_commonparameters), a missing feature in the _built-in_ cmdlet.

### Pipeline streaming capabilities

```powershell
Measure-Command {
    $null | Invoke-Parallel { 0..10 | ForEach-Object { Start-Sleep 1; $_ } } |
        Select-Object -First 1
} | Select-Object TotalSeconds

# TotalSeconds
# ------------
#        1.06
```

### Support for [CommonParameters](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_commonparameters?view=powershell-7.4)

Something missing on `ForEach-Object -Parallel` as of `v7.5.0.3`.

```powershell
PS \> 0..5 | ForEach-Object -Parallel { Write-Error $_ } -ErrorAction Stop
# ForEach-Object: The following common parameters are not currently supported in the Parallel parameter set:
# ErrorAction, WarningAction, InformationAction, PipelineVariable
```

A few examples, they should all work properly, please submit an issue if not 😅.

```powershell
PS \> 0..5 | Invoke-Parallel { Write-Error $_ } -ErrorAction Stop
# Invoke-Parallel: 0

PS \>  0..5 | Invoke-Parallel { Write-Warning $_ } -WarningAction Stop
# WARNING: 1
# Invoke-Parallel: The running command stopped because the preference variable "WarningPreference" or common parameter is set to Stop: 1

PS \> 0..5 | Invoke-Parallel { $_ } -PipelineVariable pipe | ForEach-Object { "[$pipe]" }
# [0]
# [1]
# [5]
# [2]
# [3]
# [4]
```

## Improved `-TimeOutSeconds` error message

In `ForEach-Object -Parallel` we get an error message per stopped parallel invocation instead of a single one.

```powershell
PS \>  0..10 | ForEach-Object -Parallel { $_; Start-Sleep 5 } -TimeoutSeconds 2
# 0
# 1
# 2
# 3
# 4
# InvalidOperation: The pipeline has been stopped.
# InvalidOperation: The pipeline has been stopped.
# InvalidOperation: The pipeline has been stopped.
# InvalidOperation: The pipeline has been stopped.
# InvalidOperation: The pipeline has been stopped.
```

With `Invoke-Parallel` you get a single, _friendlier_, error message.

```powershell
PS \> 0..10 | Invoke-Parallel { $_; Start-Sleep 5 } -TimeoutSeconds 2
# 0
# 1
# 2
# 3
# 4
# Invoke-Parallel: Timeout has been reached.
```

## [`$using:`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_language_keywords?view=powershell-7.4) Support

Same as `ForEach-Object -Parallel` you can use the `$using:` scope modifier to pass-in variables to the parallel invocations.

```powershell
$message = 'world!'
'hello ' | Invoke-Parallel { $_ + $using:message }
# hello world!
```

## `-Functions` and `-Variables` Parameters

Both parameters are a quality of life addition, specially `-Functions`, which adds the locally defined functions to the runspaces [Initial Session State](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.runspaces.initialsessionstate), a missing feature on `ForEach-Object -Parallel`. This is a much better alternative to passing-in the function definition to the parallel scope.

### [`-Variables` Parameter](./docs/en-US/Invoke-Parallel.md#-variables)

```powershell
'hello ' | Invoke-Parallel { $_ + $msg } -Variables @{ msg = 'world!' }
# hello world!
```

### [`-Functions` Parameter](./docs/en-US/Invoke-Parallel.md#-functions)

```powershell
function Get-Message {param($MyParam) $MyParam + 'world!' }
'hello ' | Invoke-Parallel { Get-Message $_ } -Functions Get-Message
# hello world!
```

## Documentation

Check out [__the docs__](./docs/en-US/Invoke-Parallel.md) for information about how to use this Module.

## Installation

### Gallery

The module is available through the [PowerShell Gallery](https://www.powershellgallery.com/packages/PSParallelPipeline):

```powershell
Install-Module PSParallelPipeline -Scope CurrentUser
```

### Source

```powershell
git clone 'https://github.com/santisq/PSParallelPipeline.git'
Set-Location ./PSParallelPipeline
./build.ps1
```

## Requirements

This module has no requirements and is fully compatible with __Windows PowerShell 5.1__ and [__PowerShell Core 7+__](https://github.com/PowerShell/PowerShell).

## Contributing

Contributions are more than welcome, if you wish to contribute, fork this repository and submit a pull request with the changes.
