# Example 1
$message = 'Hello world from {0}'

0..10 | Invoke-Parallel {
    $using:message -f [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 3
} -ThrottleLimit 3

# Example 2
$message = 'Hello world from {0}'

0..10 | Invoke-Parallel {
    $message -f [runspace]::DefaultRunspace.InstanceId
    Start-Sleep 3
} -ArgumentList @{ message = $message } -ThrottleLimit 3

# Example 3
$sync = [hashtable]::Synchronized(@{})

Get-Process | Invoke-Parallel {
    $sync = $using:sync
    $sync[$_.Name] += @( $_ )
}

$sync

# Example 4
$sync = [hashtable]::Synchronized(@{})

Get-Process | Invoke-Parallel {
    $sync[$_.Name] += @( $_ )
} -ArgumentList @{ sync = $sync }

$sync

# Example 5
function Greet { param($s) "$s hey there!" }

0..10 | Invoke-Parallel {
    Greet $_
} -Functions Greet