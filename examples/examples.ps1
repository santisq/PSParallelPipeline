# Example 1
$message = 'Hello world from {0}'

0..10 | Invoke-Parallel {
    $using:message -f [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 1
} -ThrottleLimit 3 -UseNewRunspace

# Example 2
$message = 'Hello world from {0}'

0..10 | Invoke-Parallel {
    $message -f [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 1
} -Variables @{ message = $message } -ThrottleLimit 3

# Example 3
$threadSafeDictionary = [System.Collections.Concurrent.ConcurrentDictionary[string,object]]::new()

Get-Process | Invoke-Parallel {
    $dict = $using:threadSafeDictionary
    $dict.TryAdd($_.ProcessName, $_)
}

$threadSafeDictionary["pwsh"]

# Example 4
$threadSafeDictionary = [System.Collections.Concurrent.ConcurrentDictionary[string,object]]::new()

Get-Process | Invoke-Parallel {
    $dict.TryAdd($_.ProcessName, $_)
} -Variables @{ dict = $threadSafeDictionary }

$threadSafeDictionary["pwsh"]

# Example 5
function Greet { param($s) "$s hey there!" }

0..10 | Invoke-Parallel { Greet $_ } -Functions Greet

# Example 6
Get-Process | Invoke-Parallel {
    Start-Sleep (Get-Random -Maximum 4)
    $_
} -ThrottleLimit 10 -TimeoutSeconds 4

# Example 7
$ids = 0..10 | Invoke-Parallel { [runspace]::DefaultRunspace.InstanceId }
($ids | Select-Object -Unique).Count # 5

$ids = 0..10 | Invoke-Parallel { [runspace]::DefaultRunspace.InstanceId } -UseNewRunspace
($ids | Select-Object -Unique).Count # 11