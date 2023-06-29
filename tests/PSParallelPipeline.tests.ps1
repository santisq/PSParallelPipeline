Describe PSParallelPipeline {
    BeforeAll {
        $global:ErrorActionPreference = 'Stop'
        $moduleName = (Get-Item ([IO.Path]::Combine($PSScriptRoot, '..', 'Module', '*.psd1'))).BaseName
        $manifestPath = [IO.Path]::Combine($PSScriptRoot, '..', 'output', $moduleName)
        Import-Module $manifestPath
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

        It 'Should process in parallel' {
            $timer = [System.Diagnostics.Stopwatch]::StartNew()
            0..5 | Invoke-Parallel { Start-Sleep 1 } -ThrottleLimit 5
            $timer.Elapsed | Should -BeLessOrEqual ([timespan]::FromSeconds(5))
            $timer.Stop()
        }

        It 'Should stop processing after a set timeout' {
            $timer = [System.Diagnostics.Stopwatch]::StartNew()

            { 0..5 | Invoke-Parallel { Start-Sleep 10 } -TimeoutSeconds 2 } |
                Should -Throw

            $timer.Elapsed | Should -BeLessOrEqual ([timespan]::FromSeconds(4))
            $timer.Stop()
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

        It 'Should add functions to the parallel scope with -Functions parameter' {
            # This test is broken in Pester, need to figure out why
            # return

            # seems to need global scoped function for some reason...

            function global:Test-Function {
                param($s)
                'Hello {0:D2}' -f $s
            }

            $invokeParallelSplat = @{
                Functions   = 'Test-Function'
                ScriptBlock = { Test-Function $_ }
            }

            0..10 | Invoke-Parallel @invokeParallelSplat |
                Sort-Object |
                Should -BeExactly @(0..10 | ForEach-Object { Test-Function $_ })
        }

        It 'Should autocomplete existing commands in the caller scope' {
            $result = TabExpansion2 -inputScript ($s = 'Invoke-Parallel -Function Get-') -cursorColumn $s.Length
            $result.CompletionMatches.Count | Should -BeGreaterThan 0
            $result.CompletionMatches.ListItemText | Should -Match '^Get-'
        }

        It 'Should throw a terminating error' {
            { 0..1 | Invoke-Parallel { throw } } | Should -Throw
        }

        It 'Should write to the Error Stream' {
            try {
                0..1 | Invoke-Parallel { Write-Error 'hello world!' }
            }
            catch {
                $_ | Should -BeOfType ([System.Management.Automation.ErrorRecord])
            }

        }
    }
}
