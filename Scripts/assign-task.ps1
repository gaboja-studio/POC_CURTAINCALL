# PM 전용. 기술 점검 → 통합 준비 커밋·push → 작업 branch 생성 또는 준비 branch 문서 sync PR. 실제 전달은 별도 확인.
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][ValidatePattern('^Task-\d{8}-\d{3}$')][string]$Id,
    [switch]$UsePreparedBranch,
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$current = (& git -C $RepoRoot branch --show-current).Trim()
if ($current -notmatch '^integration/(.+)$') { throw "통합 브랜치(integration/*)에서 실행하세요. 현재: $current" }
$integrationMeta = Get-RepoPath "Integrations/Active/Integration-$($Matches[1])/meta.md"
& git -C $RepoRoot -c submodule.recurse=false fetch --no-recurse-submodules origin --prune --quiet
if ($LASTEXITCODE -ne 0) { throw '배정 관계 확인용 git fetch 실패' }
Assert-IntegrationLineage -Branch $current -Ref HEAD
$integrationText = Get-Content -LiteralPath $integrationMeta -Raw
if ($UsePreparedBranch -or (Get-MetaField -Text $integrationText -Field 'Active Task')) {
    $remoteIntegration = Get-IntegrationMetadata -Branch $current
    if ((Get-MetaField -Text $integrationText -Field 'Base') -cne $remoteIntegration.Base) { throw '원격 통합 Base와 로컬 Base가 다릅니다.' }
}

$dir = @(Get-ChildItem -LiteralPath (Get-RepoPath 'Tasks/Active') -Directory -Filter "$Id-*" -ErrorAction SilentlyContinue)
if ($dir.Count -ne 1) { throw "Tasks/Active/$Id-* 를 찾지 못함" }
$taskDir = $dir[0].FullName
$taskRel = "Tasks/Active/$($dir[0].Name)"
$meta = Join-Path $taskDir 'meta.md'
$setup = Join-Path $taskDir 'setup.md'
$branch = ((Get-MetaField -MetaPath $meta -Field 'Branch') -replace '\s*\(.*\)$', '').Trim()
$status = Get-MetaField -MetaPath $meta -Field 'Status'

$problems = [System.Collections.Generic.List[string]]::new()
# assigned인데 원격 브랜치가 없으면 이전 인계가 중간에 실패한 것이므로 이어서 진행한다.
if ($status -notin 'scaffolded', 'assigned') { $problems.Add("Status가 scaffolded가 아님: $status") }
if ($branch -cnotmatch $script:TaskBranchPattern) { $problems.Add("meta.md Branch 값이 올바르지 않음: $branch") }
elseif ($Matches[1] -cne (Get-MetaField -MetaPath $meta -Field 'Type')) { $problems.Add("Task 종류와 브랜치가 다름: $branch") }
if ((Get-MetaField -MetaPath $meta -Field 'Integration') -cne $current) { $problems.Add('Task Integration이 현재 통합과 다름') }
try {
    Assert-TaskAssignmentGate -Id $Id -TaskPath $taskDir -IntegrationText $integrationText
    if ($UsePreparedBranch) {
        Assert-PreparedBranch -Integration $current -Branch $branch -Id $Id -Type (Get-MetaField -MetaPath $meta -Field 'Type')
        & git -C $RepoRoot show-ref --verify --quiet "refs/heads/$branch"
        if ($LASTEXITCODE -eq 0) { Assert-PreparedBranch -Integration $current -Branch $branch -Id $Id -BranchRef "refs/heads/$branch" }
        if ($NoPush) { throw '-UsePreparedBranch 배정 문서 전달은 sync PR이 필요하므로 -NoPush와 함께 쓸 수 없습니다.' }
    } else {
        foreach ($ref in "refs/heads/$branch", "refs/remotes/origin/$branch") {
            & git -C $RepoRoot rev-parse --verify --quiet $ref | Out-Null
            if ($LASTEXITCODE -eq 0) { throw "이미 있는 브랜치: $branch (-UsePreparedBranch 검증 필요)" }
        }
    }
} catch { $problems.Add($_.Exception.Message) }

$unchecked = @((Get-MarkdownSection -Path $setup -Heading '인계 전 체크') -split "`n" | Where-Object { $_ -match '^\s*-\s*\[ \]' })
foreach ($u in $unchecked) { $problems.Add("인계 체크 미완료: $($u -replace '^\s*-\s*\[ \]\s*', '')") }
$jira = Get-MarkdownSection -Path $setup -Heading 'JIRA'
if ($jira -notmatch '관련 JIRA:\s*\S') { $problems.Add('setup.md JIRA 칸이 비어 있음 (없으면 "없음")') }

$areas = Get-SetupAreas -SetupPath $setup
if (-not $areas.Allowed.Count) { $problems.Add('setup.md 작업 구역에 백틱 경로가 없음') }
$stagePaths = [System.Collections.Generic.List[string]]::new()
$stagePaths.Add($taskRel)
foreach ($a in $areas.Allowed) {
    $full = Get-RepoPath $a.TrimEnd('/')
    if (-not (Test-Path -LiteralPath $full)) { $problems.Add("작업 구역 경로가 없음: $a"); continue }
    $stagePaths.Add($a.TrimEnd('/'))
    foreach ($m in Get-AssetMetaPaths $a) {
        if (Test-Path -LiteralPath (Get-RepoPath $m)) { if (-not $stagePaths.Contains($m)) { $stagePaths.Add($m) } }
        else { $problems.Add(".meta 없음: $m (Unity에서 저장하면 생김)") }
    }
}
$joint = Get-MarkdownSection -Path (Join-Path $taskDir 'plan.md') -Heading '공동 테스트'
if ($joint -notmatch '(?m)^\s*\d+\.\s*\S' -and $joint -notmatch '불필요') { $problems.Add('plan.md 공동 테스트 항목이 비어 있음') }

Write-Output "Task:     $Id → 작업 브랜치 $branch"
Write-Output "올릴 경로: $($stagePaths -join ', ')"
$problems | ForEach-Object { Write-Output "[문제] $_" }
if ($problems.Count) {
    if ($WhatIfPreference) { exit 1 }
    throw '인계 전 점검을 통과하지 못했습니다.'
}
if (-not $PSCmdlet.ShouldProcess($branch, '기술 배정 준비(커밋·push·브랜치 생성 또는 sync PR)')) { return }
Assert-CommitIdentity

Set-MetaField -MetaPath $meta -Field 'Status' -Value 'assigned'
Set-MetaField -MetaPath $meta -Field 'Branch' -Value $branch
Set-MetaField -MetaPath $meta -Field 'Updated' -Value (Get-Date -Format 'yyyy-MM-dd')
# 점검을 통과했으므로 PM 준비 항목을 체크한다(Task 폴더째 커밋된다).
$todo = Join-Path (Split-Path $meta) 'todo.md'
foreach ($item in '테스트 씬·폴더 생성', '`plan.md` 공동 테스트 항목 합의', '인계 전 기술 점검 완료') { [void](Set-TodoChecked -TodoPath $todo -Text $item) }
if (Set-IntegrationTaskRow -MetaPath $integrationMeta -Columns @($Id, '', '', $branch, '', 'assigned')) {
    $stagePaths.Add([System.IO.Path]::GetRelativePath($RepoRoot, $integrationMeta))
}

& git -C $RepoRoot add -- @stagePaths
& git -C $RepoRoot commit -m "chore: scaffold $Id" -- @stagePaths
if ($LASTEXITCODE -ne 0) { throw 'git commit 실패' }
if (-not $NoPush) {
    & git -C $RepoRoot push origin $current
    if ($LASTEXITCODE -ne 0) { throw 'git push 실패 (브랜치 보호 우회 목록에 PM 계정이 있는지 확인)' }
}
if ($UsePreparedBranch) {
    # 기존 준비 브랜치는 이동시키지 않는다. 배정 문서는 PR의 Merge commit 수신 후에만 유효하다.
    & (Join-Path $PSScriptRoot 'new-pr.ps1') -Base $branch -Title "$Id 배정 문서 sync" -Summary "$Id 기술 배정 준비 문서 전달. Merge commit으로 수신하며 실제 전달은 PM 별도 확인." -ScopeCheck 'PM 통합 준비 문서' -CompileCheck '해당 없음 (문서 sync)' -TestCheck 'assign-task 기술 점검 완료'
    if ($LASTEXITCODE -ne 0) { throw '배정 문서 sync PR 생성 실패' }
    Write-Output '배정 문서 수신 대기: sync PR을 정식 Merge commit으로 반영한 뒤 실제 전달을 확인하세요(자동 병합 없음).'
} else {
    & git -C $RepoRoot branch $branch HEAD
    if ($LASTEXITCODE -ne 0) { throw "작업 브랜치 생성 실패: $branch" }
    if (-not $NoPush) {
        & git -C $RepoRoot push -u origin $branch
        if ($LASTEXITCODE -ne 0) { throw "작업 브랜치 push 실패: $branch" }
    }
}
$title = Get-MetaField -MetaPath $meta -Field 'Title'
$assignee = Get-MetaField -MetaPath $meta -Field 'Assignee'
Write-Output ''
Write-Output '기술 배정 준비 완료 · 실제 전달 미확인'
Write-Output "담당자($assignee)에게 보낼 메시지 초안(문서 수신·PM 실제 전달 확인 후):"
Write-Output "  $Id($title) 맡아 주세요. AI에게 '$Id 시작해줘'라고 하시면 됩니다."
