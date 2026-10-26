# plan의 Branch로 시작·재개한다. 상태·PM 인계·Jira gate와 자동 저장은 없다.
[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^Task-\d{8}-\d{3}$')][string]$Id, [string]$Base, [switch]$Fetch)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')
if ($Fetch) {
    & git -C $RepoRoot -c submodule.recurse=false fetch --no-recurse-submodules origin --prune --quiet
    if ($LASTEXITCODE -ne 0) { throw '최신 ref 확인 실패' }
}
$dirs=@(Get-ChildItem -LiteralPath (Get-RepoPath 'Tasks/Active') -Directory -Filter "$Id-*" -ErrorAction SilentlyContinue)
if ($dirs.Count -gt 1) { throw "Task가 여러 개입니다: $Id" }
if ($dirs.Count) {
    $plan=Get-TaskPlanPath $dirs[0].FullName
    $branch=("$(Get-MetaField -MetaPath $plan -Field Branch)" -replace '\s*\(.*\)$','').Trim()
    if (-not $Base) { $Base=Get-MetaField -MetaPath $plan -Field Base }
} else {
    $candidates=@(Get-TaskRefMetadata -Id $Id | Select-Object -ExpandProperty Branch -Unique)
    if ($candidates.Count -ne 1) { throw 'Task를 찾을 수 없거나 브랜치가 여러 개입니다. Task 또는 -Fetch를 확인하세요.' }
    $branch=$candidates[0]
}
if ($branch -cnotmatch $script:TaskBranchPattern) { throw "작업 브랜치 이름 오류: $branch" }
$current=(& git -C $RepoRoot branch --show-current).Trim()
if ($current -ceq $branch) {
    Write-Output "이어 하기: $Id ($branch). plan.md → todo.md; 인계·구역 승인 없음."
    exit 0
}
$target=$null
foreach ($candidate in "refs/heads/$branch","refs/remotes/origin/$branch") {
    & git -C $RepoRoot rev-parse --verify --quiet "$candidate^{commit}" | Out-Null
    if ($LASTEXITCODE -eq 0) { $target=$candidate; break }
}
if (-not $target) {
    if (-not $dirs.Count) { throw '브랜치를 만들 Task 문서가 없습니다.' }
    if (-not $Base) { $Base=Get-BaseBranch }
    $baseRef=$null
    foreach ($candidate in "refs/remotes/origin/$Base","refs/heads/$Base") {
        & git -C $RepoRoot rev-parse --verify --quiet "$candidate^{commit}" | Out-Null
        if ($LASTEXITCODE -eq 0) { $baseRef=$candidate; break }
    }
    if (-not $baseRef) { throw "기준 브랜치가 없습니다: $Base" }
    # Task 3문서만 미추적이면 기준 브랜치에서 새 작업 브랜치를 열 수 있다.
    $dirty=@(& git -C $RepoRoot -c core.quotePath=false status --porcelain --untracked-files=all --ignore-submodules=all)
    if ($LASTEXITCODE -ne 0) { throw '작업 상태 확인 실패' }
    $prefix='Tasks/Active/'+$dirs[0].Name+'/'
    if (@($dirty | Where-Object { -not $_.StartsWith('?? '+$prefix) }).Count) { throw '다른 미저장 변경이 있습니다. 보존 후 별도 작업 공간을 사용하세요.' }
    & git -C $RepoRoot -c submodule.recurse=false switch --no-recurse-submodules -c $branch $baseRef
    if ($LASTEXITCODE -ne 0) { throw '새 작업 브랜치 생성 실패; 기존 파일을 보존했습니다.' }
} else {
    Assert-CleanTree -Hint '미저장 변경은 보존하고 별도 작업 공간을 사용하세요.'
    $tip=(& git -C $RepoRoot rev-parse "$target^{commit}").Trim()
    if ($target.StartsWith('refs/heads/')) { Invoke-SafeBranchTransition -Path $RepoRoot -TargetCommit $tip -Branch $branch }
    else { Invoke-SafeBranchTransition -Path $RepoRoot -TargetCommit $tip -Branch $branch -Track }
}
Write-Output "작업: $Id ($branch). plan.md → todo.md."
