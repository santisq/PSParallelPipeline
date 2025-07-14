<h1 align="center">PSParallelPipeline</h1>
<div align="center">
<sub>Parallel processing of pipeline input objects!</sub>
<br /><br />

[![build](https://github.com/santisq/PSParallelPipeline/actions/workflows/ci.yml/badge.svg)](https://github.com/santisq/PSParallelPipeline/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/santisq/PSParallelPipeline/branch/main/graph/badge.svg?token=b51IOhpLfQ)](https://codecov.io/gh/santisq/PSParallelPipeline)
[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/PSParallelPipeline?label=gallery)](https://www.powershellgallery.com/packages/PSParallelPipeline)
[![LICENSE](https://img.shields.io/github/license/santisq/PSParallelPipeline)](https://github.com/santisq/PSParallelPipeline/blob/main/LICENSE)

</div>

`PSParallelPipeline` is a PowerShell module featuring the `Invoke-Parallel` cmdlet, designed to process pipeline input objects in parallel using multithreading. It mirrors the capabilities of [`ForEach-Object -Parallel`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/foreach-object) from PowerShell 7.0+, bringing this functionality to Windows PowerShell 5.1, surpassing the constraints of [`Start-ThreadJob`](https://learn.microsoft.com/en-us/powershell/module/threadjob/start-threadjob).

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

Unlike `ForEach-Object -Parallel` (up to v7.5), `Invoke-Parallel` supports [Common Parameters](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_commonparameters), enhancing control and debugging.

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

## [`$using:`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_scopes#the-using-scope-modifier) Scope Support

Easily pass variables into parallel scopes with the `$using:` modifier, just like `ForEach-Object -Parallel`:

```powershell
$message = 'world!'
'hello ' | Invoke-Parallel { $_ + $using:message }
# hello world!
```

## `-Variables`, `-Functions`, `-ModuleNames`, and `-ModulePaths` Parameters

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

- [`-ModuleNames` Parameter](./docs/en-US/Invoke-Parallel.md#-modulenames): Import system-installed modules into parallel runspaces by name, using modules discoverable via `$env:PSModulePath`.

    ```powershell
    Import-Csv users.csv | Invoke-Parallel { Get-ADUser $_.UserPrincipalName } -ModuleNames ActiveDirectory
    # Imports ActiveDirectory module for Get-ADUser
    ```

- [`-ModulePaths` Parameter](./docs/en-US/Invoke-Parallel.md#-modulepaths): Import custom modules from specified directory paths into parallel runspaces.

    ```powershell
    $moduleDir = Join-Path $PSScriptRoot "CustomModule"
    0..10 | Invoke-Parallel { Get-CustomCmdlet } -ModulePaths $moduleDir
    # Imports custom module for Get-CustomCmdlet
    ```

These parameters are a quality-of-life enhancement, especially `-Functions`, which incorporates locally defined functions to the runspaces’ [Initial Session State](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.runspaces.initialsessionstate)—a feature absent in `ForEach-Object -Parallel` and a far better option than passing function definitions into the parallel scope. The new `-ModuleNames` and `-ModulePaths` parameters simplify module integration by automatically loading system-installed and custom modules, respectively, eliminating the need for manual `Import-Module` calls within the script block.

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
