# PM 전용. 현재 integration/* 브랜치 위에 Task 폴더를 만들고 번호를 발급한다(작업 브랜치는 assign-task.ps1이 인계 때 만든다).
# 번호: 작업 트리 + 로컬·원격 dev·integration/*의 Tasks 폴더 + 작업 브랜치 이름 중 가장 큰 번호 + 1. 잠금 파일로 동시 발급을 막는다.
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][ValidateSet('feat', 'fix', 'refactor')][string]$Type,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')][string]$Slug,
    [Parameter(Mandatory)][string]$Title,
    [Parameter(Mandatory)][string]$Assignee
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$current = (& git -C $RepoRoot branch --show-current).Trim()
if ($current -notmatch '^integration/(.+)$') { throw "통합 브랜치(integration/*)에서 실행하세요. 현재: $current" }
$integrationSlug = $Matches[1]
$integrationMeta = Get-RepoPath "Integrations/Active/Integration-$integrationSlug/meta.md"
$template = Get-RepoPath 'Harness/Core/Templates/Task'
if (-not $Assignee.StartsWith('@')) { $Assignee = "@$Assignee" }

& git -C $RepoRoot fetch origin --prune --quiet

$commonDir = (& git -C $RepoRoot rev-parse --path-format=absolute --git-common-dir).Trim()
$lockPath = Join-Path $commonDir 'harness-new-task.lock'
$lock = $null
for ($i = 0; $i -lt 20 -and -not $lock; $i++) {
    try { $lock = [System.IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None') } catch { Start-Sleep -Milliseconds 500 }
}
if (-not $lock) { throw "다른 new-task가 실행 중입니다(잠금: $lockPath)." }

try {
    $max = (@(Get-UsedTaskNumbers) | Measure-Object -Maximum).Maximum ?? 0
    $date = Get-Date -Format 'yyyyMMdd'
    $today = Get-Date -Format 'yyyy-MM-dd'
    $seq = '{0:D3}' -f ([int]$max + 1)
    $id = "Task-$date-$seq"
    $branch = "$Type/$date-$seq-$Slug"
    $destination = Get-RepoPath "Tasks/Active/$id-$Slug"
    if (Test-Path -LiteralPath $destination) { throw "Task 폴더가 이미 있음: $destination" }

    Write-Output "ID:       $id"
    Write-Output "제목:     $Title ($Type, 담당 $Assignee)"
    Write-Output "폴더:     Tasks/Active/$id-$Slug/"
    Write-Output "브랜치:   $branch (인계 때 생성, 소속 integration/$integrationSlug)"
    if (-not (Test-Path -LiteralPath $integrationMeta)) { Write-Output "[경고] 통합 기록이 없음: $integrationMeta" }

    if (-not $PSCmdlet.ShouldProcess($destination, 'Task 생성')) { return }

    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $template -File) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $text = $text.Replace('BRANCH_NAME', $branch).Replace('INTEGRATION_BRANCH', "integration/$integrationSlug")
        $text = $text.Replace('TASK_TYPE', $Type).Replace('ASSIGNEE', $Assignee)
        $text = $text.Replace('Task-YYYYMMDD-NNN', $id).Replace('YYYY-MM-DD', $today)
        $text = $text.Replace('- **Title:**', "- **Title:** $Title")
        Set-Content -LiteralPath (Join-Path $destination $file.Name) -Value $text -Encoding utf8NoBOM -NoNewline
    }
    [void](Set-IntegrationTaskRow -MetaPath $integrationMeta -Columns @($id, $Type, $Assignee, '(인계 때 생성)', '-', 'scaffolded'))

    Write-Output ''
    Write-Output "생성: Tasks/Active/$id-$Slug/"
    Write-Output '다음: Unity에서 테스트 씬(Assets/_Sandbox/<Feature>/)과 스크립트 폴더(Assets/Scripts/<Feature>/)를 만들고'
    Write-Output '      setup.md에 경로·JIRA를 적은 뒤 plan.md 공동 테스트 항목을 합의하세요. 인계: assign-task.ps1'
} finally {
    $lock.Dispose()
    Remove-Item -LiteralPath $lockPath -ErrorAction SilentlyContinue -WhatIf:$false
}
