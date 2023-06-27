Describe PSParallelPipeline {
    BeforeAll {
        $moduleName = (Get-Item ([IO.Path]::Combine($PSScriptRoot, '..', 'Module', '*.psd1'))).BaseName
        $manifestPath = [IO.Path]::Combine($PSScriptRoot, '..', 'output', $moduleName)
        Import-Module $manifestPath -ErrorAction Stop
    }

    Context 'Invoke-Parallel' -Tag 'Invoke-Parallel' {

    }
}
