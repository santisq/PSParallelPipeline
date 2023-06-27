Describe PSParallelPipeline {
    BeforeAll {
        $moduleName = (Get-Item ([IO.Path]::Combine($PSScriptRoot, '..', 'Module', '*.psd1'))).BaseName
        $manifestPath = [IO.Path]::Combine($PSScriptRoot, '..', 'output', $moduleName)
        Import-Module $manifestPath -ErrorAction Stop
    }

    Context 'Invoke-Parallel' -Tag 'Invoke-Parallel' {
        It 'Should process all pipeline input' {
            { 0..10 | Invoke-Parallel { $_ } } |
                Should -Not -Throw

            $items = 0..10 | Invoke-Parallel { $_ } |
                Sort-Object

            $items | Should -BeExactly (0..10)
            $items | Should -HaveCount 11
        }

        It 'Allows $using: statements' {
            $message = 'Hello world from {0:D2}'
            $items = 0..10 | Invoke-Parallel { $using:message -f $_ } |
                Sort-Object

            $items | Should -BeExactly @(
                0..10 | ForEach-Object { 'Hello world from {0:D2}' -f $_ }
            )
        }

        It 'Can make variables available through the -Variables parameter' {
            $invokeParallelSplat = @{
                Variables   = @{ message = 'Hello world from {0:D2}' }
                ScriptBlock = { $message -f $_ }
            }

            $items = 0..10 | Invoke-Parallel @invokeParallelSplat |
                Sort-Object

            $items | Should -BeExactly @(
                0..10 | ForEach-Object { 'Hello world from {0:D2}' -f $_ }
            )
        }

        It 'Should reuse runspaces by default' {
            0..10 | Invoke-Parallel { [runspace]::DefaultRunspace.InstanceId } -ThrottleLimit 5 |
                Select-Object -Unique |
                Should -HaveCount 5
        }

        It 'Should use a new runspace when the -UseNewRunspace parameter is used' {
            0..10 | Invoke-Parallel { [runspace]::DefaultRunspace.InstanceId } -UseNewRunspace |
                Select-Object -Unique |
                Should -HaveCount 11
        }

        It 'Can add items to a single thread instance' {
            $dict = [System.Collections.Concurrent.ConcurrentDictionary[string, object]]::new()

            Get-Process | Invoke-Parallel { ($using:dict).TryAdd($_.Id, $_) } |
                Should -Contain $true

            $dict[$PID].ProcessName | Should -Be (Get-Process -Id $PID).ProcessName
        }
    }
}
