# PM 전용. 선언된 부모 → 직계 자식 통합의 PR만 생성/재사용한다. checkout·자동 병합 없음.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^integration/[a-z0-9]+(?:-[a-z0-9]+)*$')][string]$Child,
    [string]$Parent,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$gh = Get-GhState
if ($gh.State -ne 'Ready') { throw "gh가 필요합니다: $($gh.Detail) (Skill: setup-gh)" }
$repo = (Get-GitHubRepo).Slug

function Invoke-SyncApi {
    param([string]$Endpoint, [string[]]$Options = @(), [string]$Body)
    if ($Body) { $output = @($Body | & $gh.Gh api $Endpoint --method POST --input - 2>$null) }
    else { $output = @(& $gh.Gh api $Endpoint @Options 2>$null) }
    if ($LASTEXITCODE -ne 0) { throw "GitHub API 실패: $Endpoint (인증·권한·네트워크 확인)" }
    try { return ConvertFrom-Json -InputObject ($output -join "`n") -NoEnumerate -ErrorAction Stop }
    catch { throw "GitHub API JSON 오류: $Endpoint" }
}

# GITHUB_ACTOR 등 환경변수 대신 이 명령을 실제 인증하는 계정을 확인한다.
$user = Invoke-SyncApi 'user'
$pm = Get-PmAccount
if (-not $pm -or -not $user.login -or $user.login -ine $pm) { throw 'PM의 gh 인증 계정으로만 동기화 PR을 만들 수 있습니다.' }
$remoteRepo = Invoke-SyncApi "repos/$repo"
if (-not $remoteRepo.id -or $remoteRepo.full_name -ine $repo) { throw 'origin과 GitHub 저장소가 일치하지 않습니다.' }

$pins = [ordered]@{}
$branch = $Child
$root = Get-BaseBranch
while ($true) {
    if ($branch -cne $root -and $branch -cnotmatch '^integration/[a-z0-9]+(?:-[a-z0-9]+)*$') { throw "허용하지 않는 부모 브랜치: $branch" }
    if ($pins.Contains($branch)) { throw "통합 Base 자기참조/순환: $branch" }
    # ref만 갱신한다. 사용자 index/worktree 및 private/LFS에는 접근하지 않는다.
    & git -C $RepoRoot -c submodule.recurse=false fetch --no-recurse-submodules --no-tags --no-write-fetch-head origin "refs/heads/${branch}:refs/remotes/origin/$branch" --quiet
    if ($LASTEXITCODE -ne 0) { throw "원격 계보 fetch 실패: $branch" }
    $sha = & git -C $RepoRoot rev-parse --verify "refs/remotes/origin/$branch^{commit}"
    if ($LASTEXITCODE -ne 0 -or "$sha" -cnotmatch '^[a-f0-9]{40}$') { throw "원격 SHA 조회 실패: $branch" }
    $pins[$branch] = "$sha"
    if ($branch -ceq $root) { break }
    $meta = Get-IntegrationMetadata -Branch $branch -Ref $sha
    if ($branch -ceq $Child) { $declaredParent = $meta.Base }
    if (-not $meta.Base) { throw "통합 Base 누락: $branch" }
    $branch = $meta.Base
}
Assert-IntegrationLineage -Branch $Child -PinnedRefs $pins -RequireActive
if ($Parent -and $Parent -cne $declaredParent) { throw "선언된 부모와 불일치: 요청 $Parent / Base $declaredParent" }
$Parent = $declaredParent

function Assert-SyncTips {
    $refs = @($pins.Keys | ForEach-Object { "refs/heads/$_" })
    $lines = @(& git -C $RepoRoot ls-remote --heads origin @refs 2>$null)
    if ($LASTEXITCODE -ne 0) { throw '원격 tip 재확인 실패. 새 미리보기가 필요합니다.' }
    $observed = [System.Collections.Generic.Dictionary[string,string]]::new([System.StringComparer]::Ordinal)
    foreach ($line in $lines) {
        if ($line -cnotmatch '^([a-f0-9]{40})\s+refs/heads/(.+)$') { throw '원격 tip 응답 형식 오류' }
        if (-not $observed.TryAdd($Matches[2], $Matches[1])) { throw '원격 tip 응답 중복' }
    }
    foreach ($name in $pins.Keys) {
        if (-not $observed.ContainsKey($name) -or $observed[$name] -cne $pins[$name]) {
            throw "원격 tip 변경: $name. 새 미리보기·재검토가 필요합니다. 이미 생성된 PR은 삭제/수정하지 않습니다."
        }
    }
}

function Assert-SyncPrShape {
    param($Pr)
    if (-not $Pr -or $Pr.number -isnot [long] -and $Pr.number -isnot [int] -or $Pr.number -le 0 -or
        $Pr.state -cnotin @('open', 'closed') -or -not $Pr.head.ref -or -not $Pr.base.ref -or
        -not $Pr.base.repo.id -or -not $Pr.base.repo.full_name -or
        "$($Pr.head.sha)" -cnotmatch '^[a-f0-9]{40}$' -or "$($Pr.base.sha)" -cnotmatch '^[a-f0-9]{40}$') {
        throw 'PR 응답 필수 필드가 없거나 잘못되었습니다.'
    }
}

function Test-SyncPrPair {
    param($Pr)
    return $Pr.state -ceq 'open' -and $Pr.head.ref -ceq $Parent -and $Pr.base.ref -ceq $Child -and
        $Pr.head.repo.id -eq $remoteRepo.id -and $Pr.base.repo.id -eq $remoteRepo.id -and
        $Pr.head.repo.full_name -ieq $repo -and $Pr.base.repo.full_name -ieq $repo
}

function Find-SyncPr {
    # 모든 페이지를 검사한다. 조회 오류나 잘못된 JSON을 빈 목록으로 간주하지 않는다.
    $pages = Invoke-SyncApi "repos/$repo/pulls" -Options @('--method', 'GET', '-f', 'state=open', '-f', "head=$($repo.Split('/')[0]):$Parent", '-f', "base=$Child", '-f', 'per_page=100', '--paginate', '--slurp')
    if ($pages -isnot [array] -or $pages.Count -eq 0) { throw 'PR 목록 응답 형식 오류' }
    $found = @()
    foreach ($page in $pages) {
        if ($page -isnot [array]) { throw 'PR 목록 페이지 형식 오류' }
        foreach ($pr in $page) {
            Assert-SyncPrShape $pr
            if (Test-SyncPrPair $pr) { $found += $pr }
        }
    }
    if ($found.Count -gt 1) { throw '동일 방향의 열린 PR이 여러 개입니다. PM 확인이 필요합니다.' }
    if ($found.Count -eq 1) { return $found[0] }
    return $null
}

function Confirm-SyncPr {
    param($Pr)
    Assert-SyncPrShape $Pr
    $fresh = Invoke-SyncApi "repos/$repo/pulls/$($Pr.number)"
    Assert-SyncPrShape $fresh
    if ($fresh.number -ne $Pr.number -or -not (Test-SyncPrPair $fresh) -or
        $fresh.html_url -ine "https://github.com/$repo/pull/$($fresh.number)" -or
        $fresh.head.sha -cne $pins[$Parent] -or $fresh.base.sha -cne $pins[$Child]) {
        throw 'PR의 저장소/방향/상태/SHA가 바뀌었습니다. 재검토 필요; PR을 삭제/수정하지 않습니다.'
    }
    Assert-SyncTips
    return $fresh
}

$title = "[sync] $Parent → $Child"
$summary = @"
선언된 부모의 변경을 직계 자식 통합으로 동기화합니다.
- 부모: $Parent ($($pins[$Parent]))
- 자식: $Child ($($pins[$Child]))
- 원격 계보 메타를 고정 SHA로 검증했습니다. checkout/사용자 파일 변경은 하지 않습니다.
- 필수 리뷰·CI가 끝난 뒤 PM이 Merge commit으로 병합합니다. 자동 병합·보호 우회는 없습니다.
- GitHub 생성 API는 브랜치 기반입니다. 위 SHA 이후 변경은 새 미리보기와 재검토가 필요합니다.
"@
$body = New-PrTemplateBody -Summary $summary -ScopeCheck '미실행 (원격 계보만 검증; CI 구역 검사와 다름)' -MergeNote '병합 방식: Merge commit (Squash 금지). 이 PR에 Update branch를 실행하지 않습니다(자식 변경의 부모 역유입 방지).'
Write-Host "방향: $Parent → $Child"
Write-Host "고정 SHA: $($pins[$Parent]) → $($pins[$Child])"

$existing = Find-SyncPr
if ($existing) {
    $confirmed = Confirm-SyncPr $existing
    if ($DryRun) { Write-Host "예정 동작: Reused $($confirmed.html_url) (기존 본문 유지, 외부 쓰기 없음)" }
    [pscustomobject]@{ Status = 'Reused'; Url = $confirmed.html_url; DryRun = [bool]$DryRun }
    return
}
$compare = Invoke-SyncApi "repos/$repo/compare/$($pins[$Child])...$($pins[$Parent])"
if ($compare.status -cnotin @('ahead', 'diverged', 'behind', 'identical')) { throw 'compare 응답 상태가 없거나 잘못되었습니다.' }
Assert-SyncTips
if ($compare.status -cin @('behind', 'identical')) {
    [pscustomobject]@{ Status = 'UpToDate'; Url = $null; DryRun = [bool]$DryRun }
    return
}
if ($DryRun) {
    Write-Host "예정 동작: Created (미리보기만, 외부 쓰기 없음)`n제목: $title`n----- 본문 -----`n$body"
    [pscustomobject]@{ Status = 'WouldCreate'; Url = $null; DryRun = $true }
    return
}

# 바로 직전 다시 확인해도 API 자체의 원자적 SHA 고정을 대신하지는 못한다.
Assert-SyncTips
try {
    $created = Invoke-SyncApi "repos/$repo/pulls" -Body (@{ head = $Parent; base = $Child; title = $title; body = $body } | ConvertTo-Json -Compress)
} catch {
    $failure = $_
    $raced = Find-SyncPr
    if (-not $raced) { throw $failure }
    $confirmed = Confirm-SyncPr $raced
    [pscustomobject]@{ Status = 'Reused'; Url = $confirmed.html_url; DryRun = $false }
    return
}
$confirmed = Confirm-SyncPr $created
[pscustomobject]@{ Status = 'Created'; Url = $confirmed.html_url; DryRun = $false }
