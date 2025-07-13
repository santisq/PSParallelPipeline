Start-Sleep 5
function Get-HelloWorld { "Hello world from $([runspace]::DefaultRunspace.InstanceId)!" }
