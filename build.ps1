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

$projectInfo = [ProjectBuilder.ProjectInfo]::Create($pwd, $Configuration)
$projectInfo.GetRequirements($requirements) | Import-Module -DisableNameChecking -Force

if (-not (dotnet tool list --global | Select-String coverlet.console -SimpleMatch)) {
    Write-Host 'Installing dotnet tool coverlet.console'
    dotnet tool install --global coverlet.console
}

$ErrorActionPreference = $prev

$invokeBuildSplat = @{
    Task          = $Task
    File          = (Get-Item ([IO.Path]::Combine($PSScriptRoot, '*.build.ps1'))).FullName
}
Invoke-Build @invokeBuildSplat
