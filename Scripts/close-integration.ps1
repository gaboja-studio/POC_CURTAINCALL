# PM 전용. 통합 마무리.
#   기본: integration/<slug>에서 Task를 Tasks/Done으로, 통합 기록을 Integrations/Done으로 옮기고 커밋·push, 병합된 PR의 closed #번호를 모은다.
#   -Cleanup: dev 병합 후 통합 브랜치와 병합된 작업 브랜치를 원격·로컬에서 삭제한다.
# 테스트 씬은 지우지 않는다(유지·삭제는 사람이 결정해 setup.md에 기록).
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')][string]$Slug,
    [switch]$Cleanup,
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$base = Get-BaseBranch
$branch = "integration/$Slug"
$gh = Get-GhState
$repo = try { (Get-GitHubRepo).Slug } catch { $null }
if (-not $repo) { $gh = [pscustomobject]@{ State = 'NoRepo'; Gh = $null; Detail = 'origin이 GitHub 저장소가 아님' } }
# WhatIf는 remote-tracking ref/FETCH_HEAD도 바꾸지 않고 로컬에 있는 원격 snapshot만 검사한다.
if (-not $WhatIfPreference) {
    & git -C $RepoRoot -c submodule.recurse=false fetch --no-recurse-submodules origin --prune --quiet
    if ($LASTEXITCODE -ne 0) { throw '통합 마무리용 git fetch 실패' }
} else {
    Write-Output '[미리보기] fetch·안전 전환을 실행하지 않습니다. 기존 origin ref 기준이며 실제 실행 때 다시 검증합니다.'
}

function Get-TaskBranches { param([string]$MetaText)
    @($MetaText -split "`n" | Where-Object { $_ -match '^\|\s*Task-\d{8}-\d{3}\s*\|' } | ForEach-Object {
        ($_.Trim('|').Split('|') | ForEach-Object { $_.Trim() })[3]
    } | Where-Object { $_ -match '^(feat|fix|refactor)/' })
}

if ($Cleanup) {
    $integrationTip = (& git -C $RepoRoot rev-parse --verify --quiet "refs/remotes/origin/$branch^{commit}").Trim()
    if ($LASTEXITCODE -ne 0) { throw "원격에 $branch 가 없습니다(이미 정리됨?)." }
    $baseTip = (& git -C $RepoRoot rev-parse --verify --quiet "refs/remotes/origin/$base^{commit}").Trim()
    if ($LASTEXITCODE -ne 0) { throw "원격 기본 브랜치 커밋 조회 실패: $base" }
    & git -C $RepoRoot merge-base --is-ancestor $integrationTip $baseTip
    if ($LASTEXITCODE -ne 0) { throw "$branch 가 아직 $base 에 병합되지 않았습니다. dev PR을 먼저 병합하세요." }
    $metaText = (& git -C $RepoRoot show "${baseTip}:Integrations/Done/Integration-$Slug/meta.md" 2>$null) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "병합된 통합 기록 조회 실패: $Slug" }
    $taskBranches = @(Get-TaskBranches $metaText)
    $current = (& git -C $RepoRoot branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0) { throw '현재 브랜치 조회 실패' }
    # 어느 원격/로컬 브랜치도 삭제하기 전에 필요한 switch/ff와 private 검증을 끝낸다.
    if ($current -eq $branch -or $taskBranches -contains $current) {
        if ($PSCmdlet.ShouldProcess($base, '삭제 전 기본 브랜치 안전 전환·검증')) {
            & git -C $RepoRoot show-ref --verify --quiet "refs/heads/$base"
            if ($LASTEXITCODE -eq 0) {
                Invoke-SafeBranchTransition -Path $RepoRoot -TargetCommit $baseTip -Branch $base
            } else {
                Invoke-SafeBranchTransition -Path $RepoRoot -TargetCommit $baseTip -Branch $base -Track
            }
        } elseif (-not $WhatIfPreference) { return }
    }
    foreach ($b in $taskBranches) {
        & git -C $RepoRoot rev-parse --verify --quiet "refs/remotes/origin/$b" | Out-Null
        if ($LASTEXITCODE -ne 0) { continue }
        if ($gh.State -eq 'Ready') {
            $done = (& $gh.Gh pr list --repo $repo --head $b --base $branch --state merged --json number --jq 'length')
            if ($LASTEXITCODE -ne 0) { throw "병합된 PR 조회 실패: $b" }
            if ($done -eq '0') { Write-Output "[건너뜀] $b — 병합된 PR이 없음"; continue }
        }
        if ($PSCmdlet.ShouldProcess($b, '작업 브랜치 삭제(원격·로컬)')) {
            & git -C $RepoRoot push origin --delete $b
            if ($LASTEXITCODE -ne 0) { throw "원격 작업 브랜치 삭제 실패: $b" }
            & git -C $RepoRoot show-ref --verify --quiet "refs/heads/$b"
            if ($LASTEXITCODE -eq 0) {
                & git -C $RepoRoot branch -D $b
                if ($LASTEXITCODE -ne 0) { throw "로컬 작업 브랜치 삭제 실패: $b" }
            }
        }
    }
    if ($PSCmdlet.ShouldProcess($branch, '통합 브랜치 삭제(원격·로컬)')) {
        & git -C $RepoRoot push origin --delete $branch
        if ($LASTEXITCODE -ne 0) { throw "원격 통합 브랜치 삭제 실패: $branch" }
        & git -C $RepoRoot show-ref --verify --quiet "refs/heads/$branch"
        if ($LASTEXITCODE -eq 0) {
            & git -C $RepoRoot branch -D $branch
            if ($LASTEXITCODE -ne 0) { throw "로컬 통합 브랜치 삭제 실패: $branch" }
        }
        Write-Output "정리 완료: $branch"
    }
    exit 0
}

$current = (& git -C $RepoRoot branch --show-current).Trim()
if ($LASTEXITCODE -ne 0) { throw '현재 브랜치 조회 실패' }
if ($current -ne $branch) { throw "$branch 에서 실행하세요. 현재: $current" }
Assert-CleanTree -Hint '마무리 전에 준비 중인 변경을 커밋하거나 정리하세요.'
$targetTip = (& git -C $RepoRoot rev-parse --verify "refs/remotes/origin/$branch^{commit}").Trim()
if ($LASTEXITCODE -ne 0) { throw "원격 통합 커밋 조회 실패: $branch" }
if ($PSCmdlet.ShouldProcess($branch, '통합 마무리 전 안전 ff·검증')) {
    Invoke-SafeBranchTransition -Path $RepoRoot -TargetCommit $targetTip
} elseif (-not $WhatIfPreference) { return }

$intDir = Get-RepoPath "Integrations/Active/Integration-$Slug"
if (-not (Test-Path -LiteralPath $intDir)) { throw "통합 기록이 없습니다: $intDir" }
$tasks = @(Get-ChildItem -LiteralPath (Get-RepoPath 'Tasks/Active') -Directory | Where-Object {
    (Get-MetaField -MetaPath (Join-Path $_.FullName 'meta.md') -Field 'Integration') -eq $branch
})

$problems = [System.Collections.Generic.List[string]]::new()
$issues = [System.Collections.Generic.SortedSet[int]]::new()
foreach ($t in $tasks) {
    $meta = Join-Path $t.FullName 'meta.md'
    $status = Get-MetaField -MetaPath $meta -Field 'Status'
    if ($status -ne 'integrated') { $problems.Add("$($t.Name): Status가 integrated가 아님($status)") }
    $scene = Get-MarkdownSection -Path (Join-Path $t.FullName 'setup.md') -Heading '테스트 씬 처리'
    if ($scene -match '결정:\s*미정') { $problems.Add("$($t.Name): 테스트 씬 유지/삭제가 '미정' — 사람이 결정해 setup.md에 기록") }
    $taskBranch = ((Get-MetaField -MetaPath $meta -Field 'Branch') -replace '\s*\(.*\)$', '').Trim()
    if ($gh.State -eq 'Ready') {
        $prs = (& $gh.Gh pr list --repo $repo --head $taskBranch --base $branch --state merged --json number,body) | ConvertFrom-Json
        if (-not @($prs).Count) { $problems.Add("$($t.Name): 병합된 PR이 없음 ($taskBranch)") }
        foreach ($p in $prs) {
            foreach ($m in [regex]::Matches("$($p.body)", '(?i)\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+#(\d+)')) { [void]$issues.Add([int]$m.Groups[1].Value) }
        }
    }
}
if ($gh.State -ne 'Ready') { Write-Output "[경고] gh 미연결 — PR 병합 여부와 이슈 번호를 확인하지 못했습니다. ($($gh.Detail))" }

Write-Output "통합: $branch → Task $($tasks.Count)개 마무리"
$tasks | ForEach-Object { Write-Output "  - $($_.Name)" }
Write-Output "모은 이슈 번호: $(if ($issues.Count) { ($issues | ForEach-Object { "#$_" }) -join ', ' } else { '없음' })"
$problems | ForEach-Object { Write-Output "[문제] $_" }
if ($problems.Count) {
    if ($WhatIfPreference) { exit 1 }
    throw '마무리 조건을 충족하지 못했습니다.'
}
if (-not $PSCmdlet.ShouldProcess($branch, 'Task·통합 기록을 Done으로 이동 후 커밋·push')) { return }

$today = Get-Date -Format 'yyyy-MM-dd'
Assert-CommitIdentity
New-Item -ItemType Directory -Path (Get-RepoPath 'Tasks/Done'), (Get-RepoPath 'Integrations/Done') -Force | Out-Null
foreach ($t in $tasks) {
    $meta = Join-Path $t.FullName 'meta.md'
    Set-MetaField -MetaPath $meta -Field 'Status' -Value 'done'
    Set-MetaField -MetaPath $meta -Field 'Updated' -Value $today
    & git -C $RepoRoot add -- $t.FullName
    & git -C $RepoRoot mv $t.FullName (Get-RepoPath "Tasks/Done/$($t.Name)")
    [void](Set-IntegrationTaskRow -MetaPath (Join-Path $intDir 'meta.md') -Columns @(($t.Name -replace '^(Task-\d{8}-\d{3}).*', '$1'), '', '', '', '', 'done'))
}
Set-MetaField -MetaPath (Join-Path $intDir 'meta.md') -Field 'Status' -Value 'to-dev'
Set-MetaField -MetaPath (Join-Path $intDir 'meta.md') -Field 'Updated' -Value $today
& git -C $RepoRoot add -- $intDir
& git -C $RepoRoot mv $intDir (Get-RepoPath "Integrations/Done/Integration-$Slug")
& git -C $RepoRoot commit -m "chore: close integration $Slug"
if ($LASTEXITCODE -ne 0) { throw 'git commit 실패' }
if (-not $NoPush) {
    & git -C $RepoRoot push origin $branch
    if ($LASTEXITCODE -ne 0) { throw 'git push 실패 (브랜치 보호 우회 목록에 PM 계정이 있는지 확인)' }
}
$issueArg = if ($issues.Count) { " -Issues $(($issues | ForEach-Object { $_ }) -join ',')" } else { '' }
Write-Output ''
Write-Output '다음: dev로 PR (Merge commit으로 병합)'
Write-Output "  pwsh -File Scripts/new-pr.ps1 -Base $base -Title `"<기능 묶음 이름>`" -Summary `"<요약>`"$issueArg -DryRun"
Write-Output "병합 후: pwsh -File Scripts/close-integration.ps1 -Slug $Slug -Cleanup -WhatIf"
