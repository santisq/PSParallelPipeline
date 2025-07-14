Start-Sleep 2
function Get-Message { "Hello world from $([runspace]::DefaultRunspace.InstanceId)!" }
