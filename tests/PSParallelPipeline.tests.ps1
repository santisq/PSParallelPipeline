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
            1 | Invoke-Parallel { Write-Error $_ } -ErrorAction Continue 2>&1 |
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
            if ($IsCoreCLR) {
                1 | Invoke-Parallel { Write-Debug $_ -Debug } -Debug 5>&1 |
                    Should -BeOfType ([DebugRecord])
                return
            }

            # Debug is weird in PowerShell 5.1. Needs a different test.
            $DebugPreference = 'Continue'
            1 | Invoke-Parallel { & { [CmdletBinding()]param() $PSCmdlet.WriteDebug(123) } -Debug } 5>&1 |
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
            $runspaces = 0..10 | Invoke-Parallel { [runspace]::DefaultRunspace } |
                Select-Object -ExpandProperty InstanceId -Unique
            $runspaces.Count | Should -BeLessOrEqual 5
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
            } | ForEach-Object TotalSeconds | Should -BeGreaterOrEqual 2

            Measure-Command {
                0..10 | Invoke-Parallel { Start-Sleep 1 } -ThrottleLimit 11
            } | ForEach-Object TotalSeconds | Should -BeLessOrEqual 1.5
        }
    }

    Context 'TimeoutSeconds Parameter' {
        It 'Stops processing after the specified seconds' {
            Assert-RunspaceCount {
                $timer = [Stopwatch]::StartNew()
                {
                    $invokeParallelSplat = @{
                        ThrottleLimit  = 100
                        TimeOutSeconds = 2
                        ErrorAction    = 'Stop'
                        ScriptBlock    = { Start-Sleep 10 }
                    }
                    1..100 | Invoke-Parallel @invokeParallelSplat
                } | Should -Throw -ExceptionType ([TimeoutException])
                $timer.Stop()
                $timer.Elapsed | Should -BeLessOrEqual ([timespan]::FromSeconds(2.2))
                $timer.Restart()
                {
                    $invokeParallelSplat = @{
                        ThrottleLimit  = 5
                        TimeOutSeconds = 1
                        ErrorAction    = 'Stop'
                        ScriptBlock    = { Start-Sleep 10 }
                    }
                    1..100 | Invoke-Parallel @invokeParallelSplat
                } | Should -Throw -ExceptionType ([TimeoutException])
                $timer.Stop()
                $timer.Elapsed | Should -BeLessOrEqual ([timespan]::FromSeconds(1.2))
            }
        }
    }

    Context 'CommandCompleter' {
        It 'Should autocomplete existing commands in the caller scope' {
            Complete 'Invoke-Parallel -Functions Compl' |
                Should -Not -BeNullOrEmpty

            Complete 'Invoke-Parallel -Functions Compl' |
                ForEach-Object ListItemText |
                Should -Contain 'Complete'

            Complete 'Invoke-Parallel -Functions NotExist' |
                Should -BeNullOrEmpty
        }
    }

    Context '$using: scope modifier' {
        It 'Allows passed-in variables through the $using: scope modifier' {
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
        It 'Should throw on passed-in Script Block via $using: scope modifier' {
            { $sb = { 1 + 1 }; 1..1 | Invoke-Parallel { & $using:sb } } |
                Should -Throw -ExceptionType ([PSArgumentException])
        }

        It 'Should throw on passed-in Script Block via -Variables parameter' {
            { $sb = { 1 + 1 }; 1..1 | Invoke-Parallel { & $sb } -Variables @{ sb = $sb } } |
                Should -Throw -ExceptionType ([PSArgumentException])
        }

        It 'Should throw on passed-in Script Block via input object' {
            { { 1 + 1 } | Invoke-Parallel { & $_ } } |
                Should -Throw -ExceptionType ([PSArgumentException])
        }
    }

    Context 'Invoke-Parallel' {
        BeforeAll {
            $testOne = {
                1..100 | Invoke-Parallel {
                    0..10 | ForEach-Object { Start-Sleep 1; $_ }
                } -ThrottleLimit 100 | Select-Object -First 5
            }

            $testTwo = {
                1..100 | Invoke-Parallel { Start-Sleep 1; $_ } -ThrottleLimit 100 |
                    Select-Object -First 10
            }

            $testThree = {
                1..100 |
                    ForEach-Object { $_; Start-Sleep -Milliseconds 200 } |
                    Invoke-Parallel { $_ } |
                    Select-Object -First 10
            }

            $testOne, $testTwo, $testThree | Out-Null
        }

        It 'Process in parallel' {
            $timer = [Stopwatch]::StartNew()
            1..5 | Invoke-Parallel { Start-Sleep 1 }
            $timer.Stop()
            $timer.Elapsed | Should -BeLessOrEqual ([timespan]::FromSeconds(1.5))
        }

        It 'Supports streaming output' {
            Assert-RunspaceCount {
                Measure-Command { & $testOne | Should -HaveCount 5 } |
                    ForEach-Object TotalSeconds |
                    Should -BeLessThan 2

                Measure-Command { & $testTwo | Should -HaveCount 10 } |
                    ForEach-Object TotalSeconds |
                    Should -BeLessThan 6

                Measure-Command { & $testThree | Should -HaveCount 10 } |
                    ForEach-Object TotalSeconds |
                    Should -BeLessThan 3
            }
        }

        It 'Can add items to a single thread instance' {
            $dict = [ConcurrentDictionary[string, object]]::new()

            Get-Process | Invoke-Parallel { ($using:dict).TryAdd($_.Id, $_) } |
                Should -Contain $true

            $dict[$PID].ProcessName | Should -Be (Get-Process -Id $PID).ProcessName
        }
    }

    Context 'Runspace Disposal Assertions' {
        It 'Disposes on CTRL+C' {
            $rs = [runspacefactory]::CreateRunspace()
            $rs.Open()

            Assert-RunspaceCount {
                param([runspace] $runspace)

                $scripts = @(
                    '1..100 | Invoke-Parallel { Start-Sleep 1; $_ } -ThrottleLimit 50'
                    '1..100 | Invoke-Parallel { Start-Sleep 1; $_ } -ThrottleLimit 100 -UseNewRunspace'
                )

                try {
                    $ps = [powershell]::Create()
                    $ps.Runspace = $runspace
                    $scripts | ForEach-Object {
                        $ps = $ps.AddScript($script).AddStatement()
                    }
                    $timer = [Stopwatch]::StartNew()
                    $task = $ps.BeginInvoke()
                    Start-Sleep 1
                    $ps.Stop()
                    while (-not $task.AsyncWaitHandle.WaitOne(200)) { }
                    $timer.Stop()
                    $timer.Elapsed | Should -BeLessOrEqual ([timespan]::FromSeconds(2))
                }
                finally {
                    $ps.Dispose()
                }
            } -ArgumentList $rs -TestCount 10

            $rs.Dispose()
        }

        It 'Disposes on PipelineStoppedException' {
            $invokeParallelSplat = @{
                ThrottleLimit = 11
                ScriptBlock   = { $_ }
            }

            Assert-RunspaceCount {
                0..10 | Invoke-Parallel @invokeParallelSplat |
                    Select-Object -First 1
            } -TestCount 100

            Assert-RunspaceCount {
                $invokeParallelSplat['UseNewRunspace'] = $true
                0..10 | Invoke-Parallel @invokeParallelSplat |
                    Select-Object -First 10
            } -TestCount 100
        }

        It 'Disposes on OperationCanceledException' {
            $invokeParallelSplat = @{
                ThrottleLimit  = 300
                TimeOutSeconds = 1
                ScriptBlock    = { Start-Sleep 1 }
            }

            Assert-RunspaceCount {
                { 0..1000 | Invoke-Parallel @invokeParallelSplat } |
                    Should -Throw
            } -TestCount 50

            Assert-RunspaceCount {
                $invokeParallelSplat['UseNewRunspace'] = $true
                $invokeParallelSplat['ThrottleLimit'] = 1001
                { 0..1000 | Invoke-Parallel @invokeParallelSplat } |
                    Should -Throw
            } -TestCount 50
        }
    }
}
