# 검증 1단계: 문서/규칙 점검 (수 초). 게임·Unity 테스트 통과를 의미하지 않는다.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$failed = [System.Collections.Generic.List[string]]::new()
foreach ($check in 'verify-structure.ps1', 'verify-knowledge.ps1', 'verify-context.ps1') {
    & (Join-Path $PSScriptRoot $check)
    if ($LASTEXITCODE -ne 0) { $failed.Add($check) }
}

& git -C $RepoRoot diff --check
if ($LASTEXITCODE -ne 0) { $failed.Add('git diff --check') }

Write-Output ''
if ($failed.Count) {
    Write-Output "Fast: FAIL ($($failed -join ', '))"
    exit 1
}
Write-Output 'Fast: OK (문서/규칙 점검만 통과. 게임·Unity 테스트 결과가 아님)'
exit 0
