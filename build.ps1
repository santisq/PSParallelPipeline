[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [Parameter()]
    [ValidateSet('Build', 'Test')]
    [string[]] $Task = 'Build'
)

$prev = $ErrorActionPreference
$ErrorActionPreference = 'Stop'

if (-not ('ProjectBuilder.ProjectInfo' -as [type])) {
    try {
        $builderPath = [IO.Path]::Combine($PSScriptRoot, 'tools', 'ProjectBuilder')
        Push-Location $builderPath

        dotnet @(
            'publish'
            '--configuration', 'Release'
            '-o', 'output'
            '--framework', 'netstandard2.0'
            '--verbosity', 'q'
            '-nologo'
        )

        if ($LASTEXITCODE) {
            throw "Failed to compiled 'ProjectBuilder'"
        }

        $dll = [IO.Path]::Combine($builderPath, 'output', 'ProjectBuilder.dll')
        Add-Type -Path $dll
    }
    finally {
        Pop-Location
    }
}

$projectInfo = [ProjectBuilder.ProjectInfo]::Create($PSScriptRoot, $Configuration)
$projectInfo.GetRequirements() | Import-Module -DisableNameChecking -Force

$ErrorActionPreference = $prev

$invokeBuildSplat = @{
    Task        = $Task
    File        = (Get-Item ([IO.Path]::Combine($PSScriptRoot, '*.build.ps1'))).FullName
    ProjectInfo = $projectInfo
}
Invoke-Build @invokeBuildSplat
