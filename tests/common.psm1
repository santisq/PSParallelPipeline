function Test-Function {
    param($s)

    'Hello {0:D2}' -f $s
}

function Complete {
    [OutputType([System.Management.Automation.CompletionResult])]
    param([string] $Expression)

    end {
        [System.Management.Automation.CommandCompletion]::CompleteInput(
            $Expression,
            $Expression.Length,
            $null).CompletionMatches
    }
}

function Assert-RunspaceCount {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [scriptblock] $ScriptBlock,

        [Parameter()]
        [int] $WaitSeconds = 5
    )

    try {
        $count = @(Get-Runspace).Count
        & $ScriptBlock
    }
    finally {
        Start-Sleep $WaitSeconds
        Get-Runspace |
            Should -HaveCount $count -Because 'Runspaces should be correctly disposed'
    }
}
