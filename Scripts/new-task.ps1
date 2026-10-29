# 누구나 현재 작업 브랜치 또는 선택 기준에서 사용할 3문서를 만든다. commit/push/checkout 없음.
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][ValidateSet('feat','fix','refactor')][string]$Type,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')][string]$Slug,
    [Parameter(Mandatory)][string]$Title,
    [Parameter(Mandatory)][string]$Assignee,
    [string]$Base,
    [switch]$UseCurrentBranch,
    [switch]$Fetch
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')
if (-not $Base) { $Base=Get-BaseBranch }
if ($Fetch -and -not $WhatIfPreference) {
    & git -C $RepoRoot -c submodule.recurse=false fetch --no-recurse-submodules origin --prune --quiet
    if ($LASTEXITCODE -ne 0) { throw '원격 ref 확인 실패' }
}
$branch=if ($UseCurrentBranch) { (& git -C $RepoRoot branch --show-current).Trim() } else { "$Type/$Slug" }
$branchPattern=if ($UseCurrentBranch) { '^(feat|fix|refactor)/(?:[a-z0-9]+(?:-[a-z0-9]+)*/)*[a-z0-9]+(?:-[a-z0-9]+)*$' } else { $script:TaskBranchPattern }
if ($branch -cnotmatch $branchPattern -or ($branch -split '/')[0] -cne $Type) { throw 'Task 종류와 작업 브랜치를 맞추세요.' }
if (-not $Assignee.StartsWith('@')) { $Assignee="@$Assignee" }
$registered=@(Get-TaskRefMetadata -Ref HEAD | Where-Object Branch -CEQ $branch)
$working=@(Get-AllTaskFolders | Where-Object { $_.State -eq 'Active' -and (Get-MetaField -MetaPath (Get-TaskPlanPath $_.Path) -Field Branch) -ceq $branch })
if ($registered.Count -or $working.Count) { throw "이미 Task에 연결된 브랜치: $branch" }
if (-not $UseCurrentBranch) {
    $refs=@(& git -C $RepoRoot for-each-ref --format='%(refname)' refs/heads refs/remotes)
    if ($LASTEXITCODE -ne 0) { throw '브랜치 목록 조회 실패' }
    if (@($refs | Where-Object { $_ -ceq "refs/heads/$branch" -or $_ -cmatch "^refs/remotes/[^/]+/$([regex]::Escape($branch))$" }).Count) {
        throw '이미 있는 브랜치입니다. 기존 Task를 확인하거나 해당 브랜치에서 -UseCurrentBranch로 연결하세요.'
    }
}
$lockDir=Get-RepoPath '.harness/local'
New-Item -ItemType Directory -Path $lockDir -Force -WhatIf:$false | Out-Null
$lockPath=Join-Path $lockDir 'new-task.lock'
$lock=$null
try {
    $lock=[IO.File]::Open($lockPath,'OpenOrCreate','ReadWrite','None')
    $max=(@(Get-UsedTaskNumbers) | Measure-Object -Maximum).Maximum ?? 0
    $id='Task-{0}-{1:D3}' -f (Get-Date -Format yyyyMMdd),([int]$max+1)
    $destination=Get-RepoPath "Tasks/Active/$id-$Slug"
    if (Test-Path -LiteralPath $destination) { throw '같은 Task 폴더가 있습니다.' }
    Write-Output "Task: $id / $Title / $Assignee / $branch → $Base"
    if (-not $PSCmdlet.ShouldProcess($destination,'Task 3문서 생성')) { return }
    New-Item -ItemType Directory -Path $destination | Out-Null
    foreach ($name in 'plan.md','todo.md','log.md') {
        $text=Get-Content -LiteralPath (Get-RepoPath "Harness/Core/Templates/Task/$name") -Raw
        $text=$text.Replace('Task-YYYYMMDD-NNN',$id).Replace('TASK_TYPE',$Type).Replace('ASSIGNEE',$Assignee).Replace('BRANCH_NAME',$branch).Replace('BASE_BRANCH',$Base)
        $text=$text.Replace('- **Title:**',"- **Title:** $Title")
        Set-Content -LiteralPath (Join-Path $destination $name) -Value $text -Encoding utf8NoBOM -NoNewline
    }
    Write-Output "생성: $destination (plan·todo는 작업 중 갱신; PM 인계 승인 없음)"
} finally {
    # 잠금 파일은 유지한다. 다른 실행이 연 파일을 삭제하는 경쟁을 피한다.
    if ($lock) { $lock.Dispose() }
}
