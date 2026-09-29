# 작업자의 "저장해줘". 구역 검사 → 문서 크기 검사 → 커밋 → 내 작업 브랜치에 push.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Message,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$branch = (& git -C $RepoRoot branch --show-current).Trim()
if ($branch -match '^(dev|integration/.+|builds/.+)$') { throw "$branch 에는 이 방법으로 저장하지 않습니다(PM 절차 사용)." }
if ($branch -notmatch '^(feat|fix|refactor|tests)/') { throw "규칙에 없는 브랜치입니다: $branch" }

$status = & git -C $RepoRoot status --porcelain
if (-not $status) { Write-Output '저장할 변경이 없습니다.'; exit 0 }

$blocked = $false
try { Assert-CommitIdentity } catch { Write-Output "[위반] $($_.Exception.Message)"; $blocked = $true }
if ($branch -notlike 'tests/*') {
    & (Join-Path $PSScriptRoot 'verify-scope.ps1')
    if ($LASTEXITCODE -ne 0) { $blocked = $true }
}
& (Join-Path $PSScriptRoot 'verify-context.ps1')
if ($LASTEXITCODE -ne 0) { $blocked = $true }

Write-Output ''
Write-Output '저장할 변경:'
$status | ForEach-Object { Write-Output "  $_" }
if ($blocked) {
    Write-Output '저장하지 않았습니다. 위 [위반]/[HARD] 항목을 먼저 해결하세요.'
    exit 1
}
if ($DryRun) { Write-Output "미리보기만 했습니다. 커밋 메시지: $Message"; exit 0 }

Assert-CommitIdentity
& git -C $RepoRoot add -A
& git -C $RepoRoot commit -m $Message
if ($LASTEXITCODE -ne 0) { throw 'git commit 실패' }
& git -C $RepoRoot push -u origin $branch
if ($LASTEXITCODE -ne 0) { throw 'git push 실패. 인터넷·GitHub 연결을 확인하세요(저장 기록은 내 PC에 남아 있음).' }
Write-Output "저장 완료: $branch — $Message"
