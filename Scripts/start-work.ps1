# 작업자의 "시작해줘"·"이어서 해줘". 원격 최신 받기 → Task 작업 브랜치로 이동 → 최신 반영 → Status를 working으로.
[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^Task-\d{8}-\d{3}$')][string]$Id)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

Assert-CleanTree -Hint 'Unity에서 저장한 뒤 "저장해줘"로 먼저 저장하거나, PM에게 알려 주세요.'
& git -C $RepoRoot fetch origin --prune --quiet
if ($LASTEXITCODE -ne 0) { throw '원격 저장소에서 최신 내용을 받지 못했습니다. 인터넷 연결이나 GitHub 연결을 확인하세요.' }

$dirs = @(Get-ChildItem -LiteralPath (Get-RepoPath 'Tasks/Active') -Directory -Filter "$Id-*" -ErrorAction SilentlyContinue)
if ($dirs.Count -gt 1) { throw "$Id Task 폴더가 여러 개입니다. PM에게 알려 주세요." }
if ($dirs.Count -eq 1) {
    $candidates = @(((Get-MetaField -MetaPath (Join-Path $dirs[0].FullName 'meta.md') -Field 'Branch') -replace '\s*\(.*\)$', '').Trim())
} else {
    $candidates = @(Get-TaskRefMetadata -Id $Id | ForEach-Object { $_.Branch } | Sort-Object -Unique)
}
if ($candidates.Count -eq 0) { throw "$Id Task 메타데이터가 없습니다. PM에게 인계가 끝났는지 확인하세요." }
if ($candidates.Count -gt 1) { throw "$Id Branch 값이 서로 다릅니다: $($candidates -join ', '). PM에게 알려 주세요." }
$branch = $candidates[0]
if ($branch -cnotmatch $script:TaskBranchPattern) { throw "$Id Branch 값이 올바르지 않습니다: $branch" }
& git -C $RepoRoot show-ref --verify --quiet "refs/heads/$branch"
$hasLocal = $LASTEXITCODE -eq 0
& git -C $RepoRoot show-ref --verify --quiet "refs/remotes/origin/$branch"
if (-not $hasLocal -and $LASTEXITCODE -ne 0) { throw "$Id 작업 브랜치가 없습니다: $branch. PM에게 알려 주세요." }

& git -C $RepoRoot show-ref --verify --quiet "refs/heads/$branch"
if ($LASTEXITCODE -eq 0) { & git -C $RepoRoot switch $branch } else { & git -C $RepoRoot switch --track "origin/$branch" }
if ($LASTEXITCODE -ne 0) { throw "브랜치 이동 실패: $branch" }
& git -C $RepoRoot rev-parse --verify --quiet "refs/remotes/origin/$branch" | Out-Null
if ($LASTEXITCODE -eq 0) {
    & git -C $RepoRoot pull --ff-only --quiet
    if ($LASTEXITCODE -ne 0) { throw '최신 내용을 자동으로 합치지 못했습니다. PM에게 알려 주세요.' }
}
Update-ThirdParty

$task = Find-CurrentTask
if (-not $task) { throw "브랜치 $branch 에 Task 폴더(Tasks/Active/$Id-*)가 없습니다. PM에게 알려 주세요." }
$meta = Join-Path $task.Path 'meta.md'
if ((Get-MetaField -MetaPath $meta -Field 'Status') -eq 'assigned') {
    Set-MetaField -MetaPath $meta -Field 'Status' -Value 'working'
    Set-MetaField -MetaPath $meta -Field 'Updated' -Value (Get-Date -Format 'yyyy-MM-dd')
    Write-Output '상태를 working으로 바꿨습니다(다음 "저장해줘" 때 함께 저장됩니다).'
}

$areas = Get-SetupAreas -SetupPath (Join-Path $task.Path 'setup.md')
Write-Output ''
Write-Output "Task:     $($task.Id) — $(Get-MetaField -MetaPath $meta -Field 'Title')"
Write-Output "브랜치:   $branch → 제출 대상 $(Get-MetaField -MetaPath $meta -Field 'Integration')"
Write-Output "작업 구역: $($areas.Allowed -join ', ')"
Write-Output "읽을 문서: $($task.Rel)handoff.md → meta.md → setup.md → todo.md → plan.md"
