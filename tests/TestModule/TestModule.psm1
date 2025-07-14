Start-Sleep 5
function Get-Message { "Hello world from $([runspace]::DefaultRunspace.InstanceId)!" }
