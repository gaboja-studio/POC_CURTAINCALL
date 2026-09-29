# PM 전용. integration/<slug>로 온 PR 현황 보기 / 하나씩 Squash 병합 / 뒤처진 PR 최신화. gh 필요.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')][string]$Slug,
    [int]$Merge,
    [int]$Update
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$gh = Get-GhState
if ($gh.State -ne 'Ready') { throw "gh가 필요합니다: $($gh.Detail) (Skill: setup-gh)" }
$repo = (Get-GitHubRepo).Slug
$base = "integration/$Slug"
$stateText = @{
    CLEAN = '병합 가능'; BEHIND = '뒤처짐(Update 필요)'; DIRTY = '충돌'; BLOCKED = '승인·검사 대기'
    UNSTABLE = '검사 실패'; UNKNOWN = '확인 중'; HAS_HOOKS = '병합 가능'; DRAFT = '초안'
}

function Get-Prs { param([string]$State = 'open')
    (& $gh.Gh pr list --repo $repo --base $base --state $State --limit 100 --json number,title,headRefName,state,mergeStateStatus,isDraft) | ConvertFrom-Json
}

if ($Update) {
    & $gh.Gh pr update-branch $Update --repo $repo
    if ($LASTEXITCODE -ne 0) { throw "PR #$Update 최신화 실패 (충돌이면 담당자와 함께 해결)" }
    Write-Output "PR #$Update 를 최신 $base 로 갱신했습니다. 검사가 끝나면 병합하세요."
    exit 0
}

if ($Merge) {
    $pr = (& $gh.Gh pr view $Merge --repo $repo --json baseRefName,state,mergeStateStatus,title) | ConvertFrom-Json
    if ($pr.baseRefName -ne $base) { throw "PR #$Merge 의 대상이 $base 가 아님: $($pr.baseRefName)" }
    if ($pr.state -ne 'OPEN') { throw "PR #$Merge 가 열려 있지 않음: $($pr.state)" }
    if ($pr.mergeStateStatus -in 'DIRTY', 'BEHIND') { throw "PR #$Merge 상태: $($stateText[$pr.mergeStateStatus]). 먼저 해결하세요." }
    & $gh.Gh pr merge $Merge --repo $repo --squash
    if ($LASTEXITCODE -ne 0) { throw "PR #$Merge 병합 실패" }
    Write-Output "병합 완료(Squash): #$Merge $($pr.title)"
    Start-Sleep -Seconds 3
    $behind = @(Get-Prs | Where-Object { $_.mergeStateStatus -in 'BEHIND', 'DIRTY', 'UNKNOWN' })
    foreach ($b in $behind) { Write-Output "  → #$($b.number) $($stateText[$b.mergeStateStatus] ?? $b.mergeStateStatus): pwsh -File Scripts/merge-integration.ps1 -Slug $Slug -Update $($b.number)" }
    exit 0
}

# 현황
$open = @(Get-Prs)
$merged = @(Get-Prs -State merged)
Write-Output "== $base PR 현황 =="
foreach ($p in $open) { Write-Output ("  #{0,-4} {1,-14} {2}  ({3})" -f $p.number, ($p.isDraft ? '초안' : ($stateText[$p.mergeStateStatus] ?? $p.mergeStateStatus)), $p.title, $p.headRefName) }
foreach ($p in $merged) { Write-Output ("  #{0,-4} {1,-14} {2}" -f $p.number, '병합됨', $p.title) }

$meta = Get-RepoPath "Integrations/Active/Integration-$Slug/meta.md"
if (Test-Path -LiteralPath $meta) {
    $heads = @($open.headRefName) + @($merged.headRefName)
    $rows = Get-Content -LiteralPath $meta | Where-Object { $_ -match '^\|\s*Task-\d{8}-\d{3}\s*\|' }
    foreach ($row in $rows) {
        $cols = $row.Trim('|').Split('|') | ForEach-Object { $_.Trim() }
        if ($cols[3] -match '^(feat|fix|refactor)/' -and $heads -notcontains $cols[3]) { Write-Output "  [미제출] $($cols[0]) ($($cols[2])) — $($cols[3])" }
    }
}
Write-Output ''
Write-Output "병합: -Merge <번호>   최신화: -Update <번호>"
