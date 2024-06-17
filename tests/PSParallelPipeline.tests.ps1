using namespace System.IO
using namespace System.Management.Automation
using namespace System.Diagnostics
using namespace System.Collections.Concurrent

$moduleName = (Get-Item ([Path]::Combine($PSScriptRoot, '..', 'module', '*.psd1'))).BaseName
$manifestPath = [Path]::Combine($PSScriptRoot, '..', 'output', $moduleName)

Import-Module $manifestPath
Import-Module ([Path]::Combine($PSScriptRoot, 'common.psm1'))

Describe PSParallelPipeline {
    Context 'Output Streams' {
        It 'Success' {
            1 | Invoke-Parallel { $_ } | Should -BeOfType ([int])
            1 | Invoke-Parallel { $_ } | Should -BeExactly 1
        }

        It 'Error' {
            1 | Invoke-Parallel { Write-Error $_ } 2>&1 |
                Should -BeOfType ([ErrorRecord])
        }

        It 'Warning' {
            1 | Invoke-Parallel { Write-Warning $_ } 3>&1 |
                Should -BeOfType ([WarningRecord])
        }

        It 'Verbose' {
            1 | Invoke-Parallel { Write-Verbose $_ -Verbose } -Verbose 4>&1 |
                Should -BeOfType ([VerboseRecord])
        }

        It 'Debug' {
            1 | Invoke-Parallel { Write-Debug $_ -Debug } -Debug -Verbose 5>&1 |
                Should -BeOfType ([DebugRecord])
        }

        It 'Information' {
            1 | Invoke-Parallel { Write-Host $_ } 6>&1 |
                Should -BeOfType ([InformationRecord])
        }

        It 'Progress' {
            $ProgressPreference = 'SilentlyContinue'

            $null | Invoke-Parallel {
                1..10 | ForEach-Object {
                    Write-Progress -Activity 'Progress Output' -PercentComplete (10 * $_)
                    Start-Sleep -Milliseconds 200
                }
                Write-Progress -Completed -Activity 'Progress Output'
            } | Should -BeNullOrEmpty
        }
    }

    Context 'Common Parameters' {
        It 'Supports ActionPreference' {
            { 1 | Invoke-Parallel { Write-Error $_ } -ErrorAction Stop } |
                Should -Throw

            { 1 | Invoke-Parallel { Write-Warning $_ } -WarningAction Stop 3>$null } |
                Should -Throw

            { 1 | Invoke-Parallel { Write-Host $_ } -InformationAction Stop 6>$null } |
                Should -Throw

            1 | Invoke-Parallel { Write-Error $_ } -ErrorAction Ignore 2>&1 |
                Should -BeNullOrEmpty

            1 | Invoke-Parallel { Write-Warning $_ } -WarningAction Ignore 2>&1 |
                Should -BeNullOrEmpty

            1 | Invoke-Parallel { Write-Host $_ } -InformationAction Ignore 2>&1 |
                Should -BeNullOrEmpty
        }

        It 'Supports PipelineVariable' {
            1 | Invoke-Parallel { $_ } -PipelineVariable pipe |
                ForEach-Object { Get-Variable pipe -ValueOnly } |
                Should -BeExactly 1
        }
    }

    Context 'UseNewRunspace Parameter' {
        It 'Should reuse runspaces by default' {
            0..10 | Invoke-Parallel { [runspace]::DefaultRunspace } |
                Select-Object -ExpandProperty InstanceId -Unique |
                Should -HaveCount 5
        }

        It 'Should use a new runspace when the -UseNewRunspace is used' {
            0..10 | Invoke-Parallel { [runspace]::DefaultRunspace } -UseNewRunspace |
                Select-Object -ExpandProperty InstanceId -Unique |
                Should -HaveCount 11
        }
    }

    Context 'Variables Parameter' {
        It 'Makes variables available in the parallel scope' {
            $items = 0..10 | Invoke-Parallel { $message -f $_ } -Variables @{
                message = 'Hello world from {0:D2}'
            } | Sort-Object

            $shouldBe = 0..10 | ForEach-Object { 'Hello world from {0:D2}' -f $_ }
            $items | Should -BeExactly $shouldBe
        }
    }

    Context 'Functions Parameter' {
        It 'Makes functions available in the parallel scope' {
            0..10 | Invoke-Parallel { Test-Function $_ } -Functions Test-Function |
                Sort-Object |
                Should -BeExactly @(0..10 | ForEach-Object { Test-Function $_ })
        }
    }

    Context 'ThrottleLimit Parameter' {
        It 'Defines the degree of parallelism' {
            Measure-Command {
                0..10 | Invoke-Parallel { Start-Sleep 1 }
            } | ForEach-Object TotalSeconds | Should -BeGreaterOrEqual 3

            Measure-Command {
                0..10 | Invoke-Parallel { Start-Sleep 1 } -ThrottleLimit 11
            } | ForEach-Object TotalSeconds | Should -BeLessOrEqual 1.5
        }
    }

    Context 'TimeoutSeconds Parameter' {
        It 'Stops processing after the specified seconds' {
            $timer = [Stopwatch]::StartNew()
            { 0..5 | Invoke-Parallel { Start-Sleep 10 } -TimeoutSeconds 2 -ErrorAction Stop } |
                Should -Throw -ExceptionType ([TimeoutException])
            $timer.Stop()
            $timer.Elapsed | Should -BeLessOrEqual ([timespan]::FromSeconds(2.1))
        }
    }

    Context 'CommandCompleter' {
        It 'Should autocomplete existing commands in the caller scope' {
            Complete 'Invoke-Parallel -Functions Compl' |
                Should -Not -BeNullOrEmpty

            Complete 'Invoke-Parallel -Functions Compl' |
                ForEach-Object ListItemText |
                Should -Be 'Complete'
        }
    }

    Context '$using: keyword Support' {
        It 'Allows passed-in variables through $using: keyword' {
            $message = 'Hello world from {0:D2}'
            $items = 0..10 | Invoke-Parallel { $using:message -f $_ } |
                Sort-Object

            $shouldBe = 0..10 | ForEach-Object { 'Hello world from {0:D2}' -f $_ }
            $items | Should -BeExactly $shouldBe
        }

        It 'Allows indexing on $using: passed-in variables' {
            $arr = 0..10; $hash = @{ foo = 'bar' }
            1 | Invoke-Parallel { $using:arr[-1] } | Should -BeExactly 10
            1 | Invoke-Parallel { $using:hash['FOO'] } | Should -BeExactly 'bar'
        }

        It 'Allows member access on $using: passed-in variables' {
            $hash = @{
                foo = @{
                    bar = [pscustomobject]@{ Index = 0..10 }
                }
            }

            1 | Invoke-Parallel { $using:hash['foo']['bar'].Index[5] } |
                Should -BeExactly 5
        }
    }

    Context 'Script Block Assertions' {
        It 'Should throw on passed-in Script Block via $using: keyword' {
            { $sb = { }; 1..1 | Invoke-Parallel { $using:sb } } |
                Should -Throw -ExceptionType ([PSArgumentException])
        }

        It 'Should throw on passed-in Script Block via -Variables parameter' {
            { $sb = { }; 1..1 | Invoke-Parallel { $sb } -Variables @{ sb = $sb } } |
                Should -Throw -ExceptionType ([PSArgumentException])
        }

        It 'Should throw on passed-in Script Block via input object' {
            { { 1 + 1 } | Invoke-Parallel { & $_ } } |
                Should -Throw -ExceptionType ([PSArgumentException])
        }
    }

    Context 'Invoke-Parallel' {
        It 'Process in parallel' {
            $timer = [Stopwatch]::StartNew()
            1..5 | Invoke-Parallel { Start-Sleep 1 }
            $timer.Stop()
            $timer.Elapsed | Should -BeLessOrEqual ([timespan]::FromSeconds(1.1))
        }

        It 'Can add items to a single thread instance' {
            $dict = [ConcurrentDictionary[string, object]]::new()

            Get-Process | Invoke-Parallel { ($using:dict).TryAdd($_.Id, $_) } |
                Should -Contain $true

            $dict[$PID].ProcessName | Should -Be (Get-Process -Id $PID).ProcessName
        }
    }
}
