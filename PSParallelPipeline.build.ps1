using namespace System.IO

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ProjectBuilder.ProjectInfo] $ProjectInfo
)

task Clean {
    $ProjectInfo.CleanRelease()
}

task BuildDocs {
    $helpParams = $ProjectInfo.Documentation.GetParams()
    $null = New-ExternalHelp @helpParams
}

task BuildManaged {
    $arguments = $ProjectInfo.GetBuildArgs()
    Push-Location -LiteralPath $ProjectInfo.Project.Source.FullName

    try {
        foreach ($framework in $ProjectInfo.TargetFrameworks.TargetFrameworks) {
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
    $ProjectInfo.Module.CopyToRelease()
    $ProjectInfo.Project.CopyToRelease()
}

task Package {
    $ProjectInfo.Project.ClearNugetPackage()
    $repoParams = $ProjectInfo.Project.GetPSRepoParams()

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
