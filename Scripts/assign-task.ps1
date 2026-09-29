# PM 전용. 인계 전 점검 → 준비 내용(Task 폴더, 테스트 씬·폴더)을 통합 브랜치에 커밋·push → 작업 브랜치 생성·push.
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][ValidatePattern('^Task-\d{8}-\d{3}$')][string]$Id,
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$current = (& git -C $RepoRoot branch --show-current).Trim()
if ($current -notmatch '^integration/(.+)$') { throw "통합 브랜치(integration/*)에서 실행하세요. 현재: $current" }
$integrationMeta = Get-RepoPath "Integrations/Active/Integration-$($Matches[1])/meta.md"

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
if ($branch -notmatch '^(feat|fix|refactor)/\d{8}-\d{3}-') { $problems.Add("meta.md Branch 값이 올바르지 않음: $branch") }
& git -C $RepoRoot rev-parse --verify --quiet "refs/remotes/origin/$branch" | Out-Null
if ($LASTEXITCODE -eq 0) { $problems.Add("이미 원격에 있는 브랜치: $branch") }

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
if (-not $PSCmdlet.ShouldProcess($branch, '인계(커밋·push·작업 브랜치 생성)')) { return }

Set-MetaField -MetaPath $meta -Field 'Status' -Value 'assigned'
Set-MetaField -MetaPath $meta -Field 'Branch' -Value $branch
Set-MetaField -MetaPath $meta -Field 'Updated' -Value (Get-Date -Format 'yyyy-MM-dd')
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
& git -C $RepoRoot branch $branch HEAD
if (-not $NoPush) {
    & git -C $RepoRoot push -u origin $branch
    if ($LASTEXITCODE -ne 0) { throw "작업 브랜치 push 실패: $branch" }
}
$title = Get-MetaField -MetaPath $meta -Field 'Title'
$assignee = Get-MetaField -MetaPath $meta -Field 'Assignee'
Write-Output ''
Write-Output "인계 완료. 담당자($assignee)에게 보낼 메시지:"
Write-Output "  $Id($title) 맡아 주세요. AI에게 '$Id 시작해줘'라고 하시면 됩니다."
