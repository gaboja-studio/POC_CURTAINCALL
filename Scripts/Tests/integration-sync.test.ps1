# 호환 명령. 예전 구역·PM·순차 제한 회귀는 폐지하고 새 작업 흐름을 검사한다.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ScratchRoot)
& ([Environment]::ProcessPath) -NoProfile -File (Join-Path $PSScriptRoot 'harness-flow.test.ps1') -ScratchRoot $ScratchRoot
exit $LASTEXITCODE
