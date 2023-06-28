<#
.SYNOPSIS
Run Pester test

.PARAMETER TestPath
The path to the tests to run

.PARAMETER OutputFile
The path to write the Pester test results to.
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [String] $TestPath,

    [Parameter(Mandatory)]
    [String] $OutputFile,

    [Parameter(ParameterSetName = 'Coverage', Mandatory)]
    [string] $SourceRoot,

    [Parameter(ParameterSetName = 'Coverage', Mandatory)]
    [string] $ReleasePath,

    [Parameter(ParameterSetName = 'Coverage')]
    [switch] $Coverage
)

$ErrorActionPreference = 'Stop'
$requirements = Import-PowerShellDataFile ([IO.Path]::Combine($PSScriptRoot, 'requiredModules.psd1'))
foreach ($req in $requirements.GetEnumerator() | Sort-Object { $_.Value['Priority'] }) {
    $importModuleSplat = @{
        Name                = ([IO.Path]::Combine($PSScriptRoot, 'Modules', $req.Key))
        Force               = $true
        DisableNameChecking = $true
    }

    Write-Host "Importing: $($importModuleSplat['Name'])"
    Import-Module @importModuleSplat
}

[PSCustomObject] $PSVersionTable |
    Select-Object -Property *, @{N = 'Architecture'; E = {
            switch ([IntPtr]::Size) {
                4 {
                    'x86'
                }
                8 {
                    'x64'
                }
                default {
                    'Unknown'
                }
            }
        }
    } |
    Format-List |
    Out-Host

$configuration = [PesterConfiguration]::Default
$configuration.Output.Verbosity = 'Detailed'

if ($Coverage.IsPresent) {
    $configuration.Run.PassThru = $true
    $configuration.CodeCoverage.Enabled = $true
    $configuration.CodeCoverage.Path = $ReleasePath
    $configuration.CodeCoverage.OutputPath = [System.IO.Path]::Combine(
        [System.IO.Path]::GetDirectoryName($OutputFile),
        'Coverage.xml')
    # $source = Join-Path (Split-Path $PSScriptRoot) Module
}

$configuration.Run.Exit = $true
$configuration.Run.Path = $TestPath
$configuration.TestResult.Enabled = $true
$configuration.TestResult.OutputPath = $OutputFile
$configuration.TestResult.OutputFormat = 'NUnitXml'

Invoke-Pester -Configuration $configuration -WarningAction Ignore | & {
    param(
        [Parameter(Mandatory)]
        [string] $SourceRoot,

        [Parameter(ValueFromPipeline)]
        [PSObject] $InputObject
    )

    process {
        Push-Location $SourceRoot
        try {
            $InputObject.CodeCoverage.CommandsMissed |
                Convert-LineNumber -Passthru |
                ForEach-Object {
                    [pscustomobject]@{
                        SourceFile = $_.SourceFile
                        Line       = $_.SourceLineNumber
                        Command    = $_.Command
                    }
            }
        }
        finally {
            Pop-Location
        }
    }
} -SourceRoot $SourceRoot
