<h1 align="center">PSParallelPipeline</h1>
<div align="center">
<sub>Parallel processing of pipeline input objects!</sub>
<br /><br />

[![build](https://github.com/santisq/PSParallelPipeline/actions/workflows/ci.yml/badge.svg)](https://github.com/santisq/PSParallelPipeline/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/santisq/PSParallelPipeline/branch/main/graph/badge.svg?token=b51IOhpLfQ)](https://codecov.io/gh/santisq/PSParallelPipeline)
[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/PSParallelPipeline?label=gallery)](https://www.powershellgallery.com/packages/PSParallelPipeline)
[![LICENSE](https://img.shields.io/github/license/santisq/PSParallelPipeline)](https://github.com/santisq/PSParallelPipeline/blob/main/LICENSE)

</div>

`PSParallelPipeline` is a PowerShell module featuring the `Invoke-Parallel` cmdlet, designed to process pipeline input objects in parallel using multithreading. It mirrors the capabilities of [`ForEach-Object -Parallel`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/foreach-object) from PowerShell 7.0+, bringing this functionality to Windows PowerShell 5.1, surpassing the constraints of [`Start-ThreadJob`](https://learn.microsoft.com/en-us/powershell/module/threadjob/start-threadjob?view=powershell-7.4).

# Why Use This Module?

Except for [`-AsJob`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/foreach-object?view=powershell-7.4#-asjob), `Invoke-Parallel` delivers the same capabilities as `ForEach-Object -Parallel` and adds support for [Common Parameters](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_commonparameters)—a feature missing from the built-in cmdlet. For larger datasets or time-intensive tasks, `Invoke-Parallel` can significantly reduce execution time compared to sequential processing.

## Streamlined Pipeline Processing

`Invoke-Parallel` can stream objects as they complete, a capability shared with `ForEach-Object -Parallel`. Each iteration sleeps for 1 second, but `Select-Object -First 1` stops the pipeline after the first object is available, resulting in a total time of ~1 second instead of 11 seconds if all `0..10` were processed sequentially.

```powershell
Measure-Command {
    $null | Invoke-Parallel { 0..10 | ForEach-Object { Start-Sleep 1; $_ } } |
        Select-Object -First 1
} | Select-Object TotalSeconds

# TotalSeconds
# ------------
#        1.06
```

## Common Parameters Support

Unlike `ForEach-Object -Parallel` (up to v7.5), `Invoke-Parallel` supports [Common Parameters](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_commonparameters?view=powershell-7.4), enhancing control and debugging.

```powershell
# Stops on first error
0..5 | Invoke-Parallel { Write-Error $_ } -ErrorAction Stop
# Invoke-Parallel: 0

# Stops on warnings
0..5 | Invoke-Parallel { Write-Warning $_ } -WarningAction Stop
# WARNING: 1
# Invoke-Parallel: The running command stopped because the preference variable "WarningPreference" is set to Stop: 1

# Pipeline variable support
0..5 | Invoke-Parallel { $_ * 2 } -PipelineVariable pipe | ForEach-Object { "[$pipe]" }
# [6] [0] [8] [2] [4] [10]
```

## Cleaner Timeout Handling

Get a single, friendly timeout message instead of multiple errors:

```powershell
0..10 | Invoke-Parallel { $_; Start-Sleep 5 } -TimeoutSeconds 2
# 0 1 2 3 4
# Invoke-Parallel: Timeout has been reached.
```

## [`$using:`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_scopes?view=powershell-7.4#the-using-scope-modifier) Scope Support

Easily pass variables into parallel scopes with the `$using:` modifier, just like `ForEach-Object -Parallel`:

```powershell
$message = 'world!'
'hello ' | Invoke-Parallel { $_ + $using:message }
# hello world!
```

## `-ModuleNames`, `-ModulePaths`, `-Variables` and `-Functions` Parameters

- [`-ModuleNames](./docs/en-US/Invoke-Parallel.md#-modulenames) and [`-ModulePaths`](./docs/en-US/Invoke-Parallel.md#-modulepaths): {complete here}

Measure-Command {
    0..4 | Invoke-Parallel {
        Import-Module .\tests\TestModule\
        Get-HelloWorld | Write-Host
    }
}

- [`-Variables` Parameter](./docs/en-US/Invoke-Parallel.md#-variables): Pass variables directly to parallel runspaces.

    ```powershell
    'hello ' | Invoke-Parallel { $_ + $msg } -Variables @{ msg = 'world!' }
    # hello world!
    ```

- [`-Functions` Parameter](./docs/en-US/Invoke-Parallel.md#-functions): Use local functions in parallel scopes without redefining them.

    ```powershell
    function Get-Message {param($MyParam) $MyParam + 'world!' }
    'hello ' | Invoke-Parallel { Get-Message $_ } -Functions Get-Message
    # hello world!
    ```

Both parameters are quality-of-life enhancements, especially `-Functions`, which adds locally defined functions to the runspaces’ [Initial Session State](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.runspaces.initialsessionstate)—a feature absent in `ForEach-Object -Parallel`. This is a far better option than passing function definitions into the parallel scope.

# Documentation

Explore detailed usage in [__the docs__](./docs/en-US/Invoke-Parallel.md).

# Installation

## PowerShell Gallery

The module is available through the [PowerShell Gallery](https://www.powershellgallery.com/packages/PSParallelPipeline):

```powershell
Install-Module PSParallelPipeline -Scope CurrentUser
```

## From Source

```powershell
git clone 'https://github.com/santisq/PSParallelPipeline.git'
Set-Location ./PSParallelPipeline
./build.ps1
```

# Requirements

- Compatible with _Windows PowerShell 5.1_ and _PowerShell 7+_
- No external dependencies

# Contributing

Contributions are more than welcome! Fork the repo, make your changes, and submit a pull request. Check out the [source](./src/PSParallelPipeline/) for more details.
