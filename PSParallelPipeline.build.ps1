using namespace System.IO

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]
    $Configuration = 'Debug'
)

$modulePath = [Path]::Combine($PSScriptRoot, 'module')
$manifestItem = Get-Item ([Path]::Combine($modulePath, '*.psd1'))
$ModuleName = $manifestItem.BaseName
$testModuleManifestSplat = @{
    Path          = $manifestItem.FullName
    ErrorAction   = 'Ignore'
    WarningAction = 'Ignore'
}
$Manifest = Test-ModuleManifest @testModuleManifestSplat
$Version = $Manifest.Version
$BuildPath = [Path]::Combine($PSScriptRoot, 'output')
$PowerShellPath = [Path]::Combine($PSScriptRoot, 'module')
$CSharpPath = [Path]::Combine($PSScriptRoot, 'src', $ModuleName)
$ReleasePath = [Path]::Combine($BuildPath, $ModuleName, $Version)
$csProjPath = Convert-Path ([Path]::Combine($CSharpPath, '*.csproj')) | Select-Object -First 1
$csharpProjectInfo = [xml]::new()
$csharpProjectInfo.Load($csProjPath)
$TargetFrameworks = $csharpProjectInfo.
    SelectSingleNode('Project/PropertyGroup/TargetFrameworks').
    InnerText.Split(';', [StringSplitOptions]::RemoveEmptyEntries)
$PSFramework = $TargetFrameworks | Select-Object -First 1

task Clean {
    if (Test-Path $ReleasePath) {
        Remove-Item $ReleasePath -Recurse -Force
    }
    New-Item -ItemType Directory $ReleasePath | Out-Null
}

task BuildDocs {
    $helpParams = @{
        Path       = [Path]::Combine($PSScriptRoot, 'docs', 'en-US')
        OutputPath = [Path]::Combine($ReleasePath, 'en-US')
    }
    New-ExternalHelp @helpParams | Out-Null
}

task BuildManaged {
    $arguments = @(
        'publish'
        '--configuration', $Configuration
        '--verbosity', 'q'
        '-nologo'
        "-p:Version=$Version"
    )

    Push-Location -LiteralPath $CSharpPath
    try {
        foreach ($framework in $TargetFrameworks) {
            Write-Host "Compiling for $framework"
            dotnet @arguments --framework $framework

            if ($LASTEXITCODE) {
                throw "Failed to compiled code for $framework"
            }
        }
    }
    finally {
        Pop-Location
    }
}

task CopyToRelease {
    $copyParams = @{
        Path        = [Path]::Combine($PowerShellPath, '*')
        Destination = $ReleasePath
        Recurse     = $true
        Force       = $true
    }
    Copy-Item @copyParams

    foreach ($framework in $TargetFrameworks) {
        $buildFolder = [Path]::Combine($CSharpPath, 'bin', $Configuration, $framework, 'publish')
        $binFolder = [Path]::Combine($ReleasePath, 'bin', $framework)
        if (-not (Test-Path -LiteralPath $binFolder)) {
            New-Item -Path $binFolder -ItemType Directory | Out-Null
        }
        Copy-Item ([Path]::Combine($buildFolder, '*')) -Destination $binFolder -Recurse
    }
}

task Package {
    $nupkgPath = [Path]::Combine($BuildPath, "$ModuleName.$Version*.nupkg")
    if (Test-Path $nupkgPath) {
        Remove-Item $nupkgPath -Force
    }

    $repoParams = @{
        Name               = 'LocalRepo'
        SourceLocation     = $BuildPath
        PublishLocation    = $BuildPath
        InstallationPolicy = 'Trusted'
    }

    if (Get-PSRepository -Name $repoParams.Name -ErrorAction SilentlyContinue) {

        Unregister-PSRepository -Name $repoParams.Name
    }

    Register-PSRepository @repoParams
    try {
        Publish-Module -Path $ReleasePath -Repository $repoParams.Name
    }
    finally {
        Unregister-PSRepository -Name $repoParams.Name
    }
}

task Analyze {
    $analyzerPath = [Path]::Combine($PSScriptRoot, 'ScriptAnalyzerSettings.psd1')
    if (-not (Test-Path $analyzerPath)) {
        Write-Host 'No Analyzer Settings found, skipping'
        return
    }

    $pssaSplat = @{
        Path        = $ReleasePath
        Settings    = $analyzerPath
        Recurse     = $true
        ErrorAction = 'SilentlyContinue'
    }
    $results = Invoke-ScriptAnalyzer @pssaSplat

    if ($results) {
        $results | Out-String
        throw 'Failed PsScriptAnalyzer tests, build failed'
    }
}

task DoTest {
    $pesterScript = [Path]::Combine($PSScriptRoot, 'tools', 'PesterTest.ps1')
    if (-not (Test-Path $pesterScript)) {
        Write-Host 'No Pester tests found, skipping'
        return
    }

    $resultsPath = [Path]::Combine($BuildPath, 'TestResults')
    if (-not (Test-Path $resultsPath)) {
        New-Item $resultsPath -ItemType Directory -ErrorAction Stop | Out-Null
    }

    $resultsFile = [Path]::Combine($resultsPath, 'Pester.xml')
    if (Test-Path $resultsFile) {
        Remove-Item $resultsFile -ErrorAction Stop -Force
    }

    $pwsh = [Environment]::GetCommandLineArgs()[0] -replace '\.dll$'

    $arguments = @(
        '-NoProfile'
        '-NonInteractive'
        if (-not $IsLinux) {
            '-ExecutionPolicy', 'Bypass'
        }
        '-File', $pesterScript
        '-TestPath', ([Path]::Combine($PSScriptRoot, 'tests'))
        '-OutputFile', $resultsFile
    )

    if ($Configuration -eq 'Debug') {
        $unitCoveragePath = [Path]::Combine($resultsPath, 'UnitCoverage.json')
        $targetArgs = '"' + ($arguments -join '" "') + '"'

        if ($PSVersionTable.PSVersion -gt '7.0') {
            $watchFolder = [Path]::Combine($ReleasePath, 'bin', $PSFramework)
        }
        else {
            $targetArgs = '"' + ($targetArgs -replace '"', '\"') + '"'
            $watchFolder = '"{0}"' -f ([Path]::Combine($ReleasePath, 'bin', $PSFramework))
        }

        $sourceMappingFile = [Path]::Combine($resultsPath, 'CoverageSourceMapping.txt')

        $arguments = @(
            $watchFolder
            '--target', $pwsh
            '--targetargs', $targetArgs
            '--output', ([Path]::Combine($resultsPath, 'Coverage.xml'))
            '--format', 'cobertura'
            if (Test-Path -LiteralPath $unitCoveragePath) {
                '--merge-with', $unitCoveragePath
            }
            if ($env:GITHUB_ACTIONS -eq 'true') {
                Set-Content $sourceMappingFile "|$PSScriptRoot$([Path]::DirectorySeparatorChar)=/_/"
                '--source-mapping-file', $sourceMappingFile
            }
        )
        $pwsh = 'coverlet'
    }

    & $pwsh $arguments

    if ($LASTEXITCODE) {
        throw 'Pester failed tests'
    }
}

task Build -Jobs Clean, BuildManaged, CopyToRelease, BuildDocs, Package
task Test -Jobs BuildManaged, Analyze, DoTest
