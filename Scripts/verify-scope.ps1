# 과거 명령 이름 호환: 구역·역할을 제한하지 않고 변경 안전성만 확인한다.
[CmdletBinding()]
param([string]$Branch, [string]$Base, [string]$Actor)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')
if (-not $Base) { $Base=Get-CheckBase }
$changed=@(Get-ChangedPaths -Base $Base)
$bad=[Collections.Generic.List[string]]::new()
$unmerged=@(& git -C $RepoRoot ls-files --unmerged)
if ($LASTEXITCODE -ne 0) { throw '충돌 상태 조회 실패' }
if ($unmerged.Count) { $bad.Add('미해결 병합 충돌') }
foreach ($file in $changed) {
    if ($file -match '(^|/)(\.env(?:\..+)?|credentials\.json|token\.json|.*\.(?:pem|p12|pfx|key))$' -and $file -notmatch '(^|/)\.env\.(example|sample|template)$') {
        if (Test-Path -LiteralPath (Get-RepoPath $file)) { $bad.Add("비밀 파일 추적 위험: $file") }
    }
    if ($file -notlike 'Assets/*' -or $file -like '*.meta' -or -not (Test-Path -LiteralPath (Get-RepoPath $file))) { continue }
    foreach ($meta in Get-AssetMetaPaths $file) {
        if (-not (Test-Path -LiteralPath (Get-RepoPath $meta))) { $bad.Add(".meta 없음: $meta") }
    }
}
$bad=@($bad | Sort-Object -Unique)
if ($bad.Count) {
    $bad | ForEach-Object { Write-Output "[위반] $_" }
    Write-Output "Safety: FAIL ($($bad.Count))"
    exit 1
}
Write-Output "Safety: OK ($($changed.Count) files; 작업 구역 제한 없음)"
exit 0
