<h1 align="center">PSParallelPipeline</h1>

</div>

<div align="center">
    <sub>
        Parallel processing of pipeline input objects!
    </sub>
    <br /><br />

[![build](https://github.com/santisq/PSParallelPipeline/actions/workflows/ci.yml/badge.svg)](https://github.com/santisq/PSParallelPipeline/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/santisq/PSParallelPipeline/branch/main/graph/badge.svg?token=b51IOhpLfQ)](https://codecov.io/gh/santisq/PSParallelPipeline)
[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/PSParallelPipeline?label=gallery)](https://www.powershellgallery.com/packages/PSParallelPipeline)
[![LICENSE](https://img.shields.io/github/license/santisq/PSParallelPipeline)](https://github.com/santisq/PSParallelPipeline/blob/main/LICENSE)

</div>

PSParallelPipeline is a PowerShell Module that includes the `Invoke-Parallel` function, a function that allows parallel processing of input objects with similar capabilities as [`ForEach-Object`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/foreach-object?view=powershell-7.3) with its `-Parallel` parameter, introduced in PowerShell 7.0.

This project is greatly inspired by RamblingCookieMonster's [`Invoke-Parallel`](https://github.com/RamblingCookieMonster/Invoke-Parallel) and Boe Prox's [`PoshRSJob`](https://github.com/proxb/PoshRSJob).

## Documentation

Check out [__the docs__](./docs/en-US/) for information about how to use this Module.

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

Compatible with __Windows PowerShell 5.1__ and [__PowerShell Core 7+__](https://github.com/PowerShell/PowerShell).

## Contributing

Contributions are more than welcome, if you wish to contribute, fork this repository and submit a pull request with the changes.
