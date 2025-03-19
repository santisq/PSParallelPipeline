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
        [int] $WaitSeconds,

        [Parameter()]
        [object[]] $ArgumentList,

        [Parameter()]
        [ValidateRange(1, [int]::MaxValue)]
        [int] $TestCount = 1
    )

    $count = @(Get-Runspace).Count
    for ($i = 0; $i -lt $TestCount; $i++) {
        try {
            if ($ArgumentList) {
                $null = & $ScriptBlock @ArgumentList
                continue
            }

            $null = & $ScriptBlock
        }
        finally {
            if ($WaitSeconds) {
                Start-Sleep $WaitSeconds
            }

            Get-Runspace |
                Should -HaveCount $count -Because 'Runspaces should be correctly disposed'
        }
    }
}

function Get-ModulePath {
    $moduleName = (Get-Item ([Path]::Combine($PSScriptRoot, '..', 'module', '*.psd1'))).BaseName
    [Path]::Combine($PSScriptRoot, '..', 'output', $moduleName)
}
