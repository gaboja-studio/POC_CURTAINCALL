# 예전 인계 명령 호환. 승인·문서 동기화 PR 없이 Task 경로만 안내한다.
[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][string]$Id,[switch]$UsePreparedBranch)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')
$dirs=@(Get-ChildItem -LiteralPath (Get-RepoPath 'Tasks/Active') -Directory -Filter "$Id-*")
if ($dirs.Count -ne 1) { throw 'Task가 없거나 여러 개입니다.' }
Write-Output "인계 승인 절차 없음: $($dirs[0].FullName)/plan.md → todo.md"
Write-Output "시작: pwsh -File Scripts/start-work.ps1 -Id $Id"
