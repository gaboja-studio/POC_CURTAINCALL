# 명시적으로 완료한 Task와 통합 기록만 이동. 상태 gate·씬 삭제·commit/push·브랜치 삭제 없음.
[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')][string]$Slug,[string[]]$CompletedTasks=@(),[switch]$Cleanup,[switch]$NoPush)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')
if ($Cleanup) { throw '자동 브랜치 삭제는 폐지했습니다. 삭제 대상과 보존 상태를 별도로 확인하세요.' }
$moves=@()
foreach ($id in $CompletedTasks) {
    if ($id -notmatch '^Task-\d{8}-\d{3}$') { throw "Task ID 오류: $id" }
    $dirs=@(Get-ChildItem -LiteralPath (Get-RepoPath 'Tasks/Active') -Directory -Filter "$id-*")
    if ($dirs.Count -ne 1) { throw "Task가 없거나 중복: $id" }
    $moves+=@{ From=$dirs[0].FullName; To=(Get-RepoPath "Tasks/Done/$($dirs[0].Name)") }
}
$int=Get-RepoPath "Integrations/Active/Integration-$Slug"
if (Test-Path -LiteralPath $int) { $moves+=@{ From=$int; To=(Get-RepoPath "Integrations/Done/Integration-$Slug") } }
foreach ($move in $moves) {
    if (-not (Test-PathInside -Child $move.From -Parent $RepoRoot) -or -not (Test-PathInside -Child $move.To -Parent $RepoRoot)) { throw '저장소 밖 이동 거부' }
    if (Test-Path -LiteralPath $move.To) { throw "대상에 기록이 있습니다. 덮어쓰지 않습니다: $($move.To)" }
}
foreach ($move in $moves) {
    if ($PSCmdlet.ShouldProcess($move.From,"Done으로 이동: $($move.To)")) {
        New-Item -ItemType Directory -Path (Split-Path $move.To) -Force | Out-Null
        Move-Item -LiteralPath $move.From -Destination $move.To
    }
}
Write-Output '명시한 완료 기록만 이동합니다. 씬·브랜치 삭제와 commit/push는 하지 않습니다.'
