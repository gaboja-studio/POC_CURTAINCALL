# 검증 0단계: 구역 검사. 이 브랜치의 변경 파일이 허용된 곳인지 확인한다.
#   허용: 내 Task 폴더(setup.md 제외), setup.md 작업 구역·배정된 공용 파일, Docs/Domains·Docs/References
#   모두 허용(team-roles.md): 빌드 씬 목록, 게임 세팅 값, 새 에셋 추가(코드·씬 제외). 기존 에셋 수정·삭제는 PM·에셋 관리자
#   급한 예외: Task 폴더의 exception.md가 있으면 위반을 [예외]로 보여 주고 통과(.meta 누락은 그대로 차단)
#   Task 없는 브랜치: resource/*는 새 에셋 추가와 에셋 관리자의 수정·_ThirdParty 포인터만, 급한 fix/*는 PM 전용·다른 Task 구역만 아니면 허용
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
$isAssetManager = Test-ThirdPartyAssetManager -Actor $Actor

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
    # resource/*의 리소스 추가는 아래 공통 허용(새 에셋)으로 통과한다. 기존 에셋 수정·삭제는 PM·에셋 관리자만.
    $mine = [pscustomobject]@{ Allowed = @(); Forbidden = @() }
}
$label = if ($task) { $task.Id } elseif ($mode -eq 'resource') { 'resource, Task 없음' } else { '급한 fix, Task 없음' }
$mergeBase = (& git -C $RepoRoot merge-base $Base HEAD 2>$null)
if (-not $mergeBase) { Write-Output "[위반] 기준 브랜치($Base)를 찾을 수 없습니다. git fetch 후 다시 시도하세요."; exit 1 }

$unmerged = @(& git -C $RepoRoot ls-files --unmerged)
if ($LASTEXITCODE -ne 0) { throw '충돌 상태를 읽지 못했습니다.' }
if ($unmerged.Count) { Write-Output '[위반] 미해결 병합 충돌이 있습니다. 충돌 해결 후 다시 검사하세요.'; exit 1 }

$mergeHeads = @(& git -C $RepoRoot rev-parse --verify --quiet MERGE_HEAD 2>$null)
$metaChanged = @(& git -C $RepoRoot diff --name-only --no-renames $mergeBase.Trim())
if ($LASTEXITCODE -ne 0) { throw '변경 파일을 읽지 못했습니다.' }
$untracked = @(& git -C $RepoRoot ls-files --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw '새 파일을 읽지 못했습니다.' }

if ($mergeHeads.Count) {
    $currentBranch = (& git -C $RepoRoot branch --show-current).Trim()
    $baseCommit = & git -C $RepoRoot rev-parse --verify "$Base^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0 -or $mergeHeads.Count -ne 1 -or $Branch -cne $currentBranch -or $mergeHeads[0] -ne $baseCommit) {
        Write-Output "[위반] 진행 중인 병합은 현재 작업 브랜치에 기준 브랜치($Base)의 최신 커밋을 받는 경우에만 검사합니다."
        exit 1
    }
    # 내 기존 커밋도 검사한다. 병합 결과가 통합 쪽과 같아져도 내 구역 밖 변경이 숨지 않게 한다.
    $ownChanges = @(& git -C $RepoRoot diff --name-only --no-renames $mergeBase.Trim() HEAD)
    if ($LASTEXITCODE -ne 0) { throw '작업 브랜치 변경을 읽지 못했습니다.' }
    $resolvedChanges = @(& git -C $RepoRoot diff --cached --name-only --no-renames $mergeHeads[0])
    if ($LASTEXITCODE -ne 0) { throw '병합 해결 결과를 읽지 못했습니다.' }
    $localChanges = @(& git -C $RepoRoot diff --name-only --no-renames)
    if ($LASTEXITCODE -ne 0) { throw '저장 대기 중인 추가 변경을 읽지 못했습니다.' }
    $changed = @($ownChanges) + @($resolvedChanges) + @($localChanges) + @($untracked) |
        Where-Object { $_ } | Sort-Object -Unique
    Write-Output 'Scope: 병합 검사 — 통합 유입은 구역 검사에서 제외, 내 커밋·충돌 해결 차이·추가 로컬 변경은 검사'
} else {
    $changed = @($metaChanged) + @($untracked) | Where-Object { $_ } | Sort-Object -Unique
}
# 받아 온 에셋에도 .meta가 빠지지 않았는지 검사한다.
$metaChanged = @($metaChanged) + @($changed) | Where-Object { $_ } | Sort-Object -Unique
# 새로 추가한 파일: 에셋 추가는 모두에게 허용하므로 수정·삭제와 구분한다.
$added = [System.Collections.Generic.HashSet[string]]::new([string[]]@(@(& git -C $RepoRoot diff --name-only --no-renames --diff-filter=A $mergeBase.Trim()) + @($untracked) | Where-Object { $_ }))
if ($LASTEXITCODE -ne 0) { throw '추가된 파일을 읽지 못했습니다.' }
$exception = if ($task) { Get-UrgentException -TaskPath $task.Path } else { $null }

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
    # 에셋 관리자는 private 저장소를 먼저 저장한 뒤 이 포인터만 갱신한다.
    # 기존 Task의 포괄적 수정 금지보다 역할 위임을 우선하며 다른 경로 권한은 늘리지 않는다.
    if ($file -eq 'Assets/_ThirdParty') {
        if ($isPm -or $isAssetManager) { continue }
        $violations.Add("외부 에셋 포인터는 PM·지정 에셋 관리자만 수정: $file")
        continue
    }
    $inMine = Test-PathInArea -Path $file -Areas $mine.Allowed
    $owner = $others | Where-Object { Test-PathInArea -Path $file -Areas $_.Allowed } | Select-Object -First 1
    if (-not $inMine) {
        $free = Get-SharedAllowance -Path $file -IsNew $added.Contains($file) -HasTask ([bool]$task) -CanEditAssets ($isPm -or $isAssetManager)
        if ($free) {
            if ($free -eq '게임 세팅 연결 기능') { $notes.Add("게임 세팅 연결 기능 변경: $file (현재 Task와 관련된 변경인지 PR에서 확인)") }
            if ($owner) { $notes.Add("진행 중인 Task($($owner.Name))의 구역에 $free`: $file (담당자에게 알리기)") }
            continue
        }
    }
    if ($owner -and -not $inMine) { $violations.Add("진행 중인 다른 Task($($owner.Name))의 구역: $file"); continue }
    if ($owner -and $mode -eq 'resource') { $notes.Add("진행 중인 Task($($owner.Name))의 구역에 리소스 추가: $file (담당자에게 알리기)") }
    if (-not $inMine -and (Test-PathInArea -Path $file -Areas $mine.Forbidden)) { $violations.Add("수정 금지: $file"); continue }
    if ($mode -eq 'resource') {
        if (-not $inMine -and -not $isPm) { $violations.Add("resource/*는 리소스 추가만 (기존 에셋 수정·삭제는 PM·에셋 관리자): $file") }
        continue
    }
    if ($inMine -or $mode -eq 'urgent-fix' -or $file -like 'Docs/Domains/*' -or $file -like 'Docs/References/*') { continue }
    if (-not $isPm) { $violations.Add("작업 구역 밖: $file") }
}

# 급한 예외: PM 구두 허락을 기록한 Task는 구역 위반을 막지 않고 모두 드러낸다. .meta 누락은 아래에서 그대로 막는다.
if ($exception -and $violations.Count) {
    $violations | ForEach-Object { Write-Output "[예외] $_" }
    Write-Output "급한 예외 적용: $($exception.Reason) (승인 $($exception.ApprovedBy)) — PR에서 PM 사후 확인 필요"
    $violations.Clear()
}

# .meta 누락: 새로 생기거나 바뀐 Assets 파일은 자신과 상위 폴더의 .meta가 있어야 한다.
$missingMeta = [System.Collections.Generic.HashSet[string]]::new()
foreach ($file in $metaChanged | Where-Object { $_ -like 'Assets/*' -and $_ -notlike '*.meta' }) {
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
Write-Output "Scope: OK ($($changed.Count) files, $label, 기준 $Base$(if ($isPm) { ', PM' })$(if ($exception) { ', 급한 예외' }))"
exit 0
