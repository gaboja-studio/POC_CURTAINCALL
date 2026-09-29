# .mcp.json에서 호출. 현재 리포/worktree 루트를 대상으로 Unity MCP 서버를 실행한다.
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$unity = Get-UnityCli
if (-not $unity) { throw 'unity CLI를 찾을 수 없음' }

& $unity mcp --project-path $RepoRoot @RemainingArgs
exit $LASTEXITCODE
