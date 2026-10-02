# 검증 0단계: 구역 검사. 이 브랜치의 변경 파일이 허용된 곳인지 확인한다.
#   허용: 내 Task 폴더(setup.md 제외), setup.md 작업 구역·배정된 공용 파일, Docs/Domains·Docs/References
#   Task 없는 브랜치: resource/*는 Assets/Resources/만, 급한 fix/*는 PM 전용·다른 Task 구역만 아니면 허용
#   위반: PM 전용 경로(.github/CODEOWNERS), 다른 Task 문서·진행 중인 다른 Task의 구역, 수정 금지, 구역 밖, .meta 누락
# 로컬: 커밋 + 저장 안 한 변경 + 새 파일을 검사. GitHub Actions: -Branch -Base -Actor를 넘겨 PR 변경을 검사.
[CmdletBinding()]
param(
    [string]$Branch,
    [string]$Base,
    [string]$Actor
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

if (-not $Branch) { $Branch = (& git -C $RepoRoot branch --show-current).Trim() }
if (-not $Actor) { $Actor = Get-CurrentGitHubUser }
$pm = Get-PmAccount
$isPm = $Actor -and $pm -and ($Actor -eq $pm)

if ($Branch -like 'tests/*') { Write-Output 'Scope: SKIP (tests/* 연습 브랜치)'; exit 0 }
if ($Branch -match '^(dev|integration/.+|builds/.+)$') {
    if ($isPm) { Write-Output "Scope: SKIP (PM @$Actor, $Branch)"; exit 0 }
    Write-Output "[위반] 작업자는 $Branch 에서 직접 작업하지 않습니다. 자기 작업 브랜치에서 작업하세요."
    exit 1
}

$task = Find-CurrentTask -Branch $Branch
$mode = if ($task) { 'task' } elseif ($Branch -like 'resource/*') { 'resource' } elseif ($Branch -like 'fix/*') { 'urgent-fix' } else { $null }
if (-not $mode) { Write-Output "[위반] 브랜치 $Branch 에 대응하는 Task 폴더가 없습니다."; exit 1 }
if ($task) {
    if (-not $Base) { $Base = "origin/$(Get-MetaField -MetaPath (Join-Path $task.Path 'meta.md') -Field 'Integration')" }
    $mine = Get-SetupAreas -SetupPath (Join-Path $task.Path 'setup.md')
} else {
    # Task 없는 fix/*·resource/*는 dev 또는 integration/*로 간다. -Base가 없으면 가장 가까운 원격 브랜치를 기준으로 본다.
    if (-not $Base) {
        $refs = @("origin/$(Get-BaseBranch)") + @(& git -C $RepoRoot for-each-ref --format='%(refname:short)' 'refs/remotes/origin/integration/')
        $Base = $refs | Where-Object { $_ } | Sort-Object { $n = & git -C $RepoRoot rev-list --count "$_..HEAD" 2>$null; if ($LASTEXITCODE) { [int]::MaxValue } else { [int]$n } } | Select-Object -First 1
    }
    $mine = [pscustomobject]@{ Allowed = @(if ($mode -eq 'resource') { 'Assets/Resources/' }); Forbidden = @() }
}
$label = if ($task) { $task.Id } elseif ($mode -eq 'resource') { 'resource, Task 없음' } else { '급한 fix, Task 없음' }
$mergeBase = (& git -C $RepoRoot merge-base $Base HEAD 2>$null)
if (-not $mergeBase) { Write-Output "[위반] 기준 브랜치($Base)를 찾을 수 없습니다. git fetch 후 다시 시도하세요."; exit 1 }

$changed = @(& git -C $RepoRoot diff --name-only --no-renames $mergeBase.Trim()) +
           @(& git -C $RepoRoot ls-files --others --exclude-standard) | Where-Object { $_ } | Sort-Object -Unique

$others = @(Get-ChildItem -LiteralPath (Get-RepoPath 'Tasks/Active') -Directory | Where-Object { $_.FullName -ne $task.Path } | ForEach-Object {
    $s = Join-Path $_.FullName 'setup.md'
    if (Test-Path -LiteralPath $s) { [pscustomobject]@{ Name = $_.Name; Allowed = (Get-SetupAreas -SetupPath $s).Allowed } }
})
$owners = Get-CodeOwnerPatterns

$violations = [System.Collections.Generic.List[string]]::new()
$notes = [System.Collections.Generic.List[string]]::new()
foreach ($file in $changed) {
    if ($task -and $file.StartsWith($task.Rel)) {
        if ($file -eq "$($task.Rel)setup.md" -and -not $isPm) { $violations.Add("PM 전용: $file (작업 구역 정의는 PM이 수정)") }
        continue
    }
    if (-not $isPm -and @($owners | Where-Object { $file -match $_ }).Count) { $violations.Add("PM 전용 경로: $file"); continue }
    if ($file -like 'Tasks/*') { $violations.Add("다른 Task 문서: $file"); continue }
    $inMine = Test-PathInArea -Path $file -Areas $mine.Allowed
    $owner = $others | Where-Object { Test-PathInArea -Path $file -Areas $_.Allowed } | Select-Object -First 1
    if ($owner -and -not $inMine) { $violations.Add("진행 중인 다른 Task($($owner.Name))의 구역: $file"); continue }
    if ($owner -and $mode -eq 'resource') { $notes.Add("진행 중인 Task($($owner.Name))의 구역에 리소스 추가: $file (담당자에게 알리기)") }
    if (-not $inMine -and (Test-PathInArea -Path $file -Areas $mine.Forbidden)) { $violations.Add("수정 금지: $file"); continue }
    if ($mode -eq 'resource') {
        if (-not $inMine -and -not $isPm) { $violations.Add("resource/*는 Assets/Resources/ 안의 리소스만: $file") }
        continue
    }
    if ($inMine -or $mode -eq 'urgent-fix' -or $file -like 'Docs/Domains/*' -or $file -like 'Docs/References/*') { continue }
    if (-not $isPm) { $violations.Add("작업 구역 밖: $file") }
}

# .meta 누락: 새로 생기거나 바뀐 Assets 파일은 자신과 상위 폴더의 .meta가 있어야 한다.
$missingMeta = [System.Collections.Generic.HashSet[string]]::new()
foreach ($file in $changed | Where-Object { $_ -like 'Assets/*' -and $_ -notlike '*.meta' }) {
    if (-not (Test-Path -LiteralPath (Get-RepoPath $file))) { continue }  # 삭제된 파일
    foreach ($m in Get-AssetMetaPaths $file) {
        if (-not (Test-Path -LiteralPath (Get-RepoPath $m))) { [void]$missingMeta.Add($m) }
    }
}
foreach ($m in $missingMeta) { $violations.Add(".meta 없음: $m (Unity에서 해당 파일·폴더를 한 번 열면 생김)") }

$notes | ForEach-Object { Write-Output "[주의] $_" }
if ($violations.Count) {
    $violations | ForEach-Object { Write-Output "[위반] $_" }
    Write-Output "Scope: FAIL ($($violations.Count)) — 구역 밖 파일은 되돌리거나 PM에게 요청하세요."
    exit 1
}
Write-Output "Scope: OK ($($changed.Count) files, $label, 기준 $Base$(if ($isPm) { ', PM' }))"
exit 0
