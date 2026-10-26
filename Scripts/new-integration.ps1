# 선택 통합. 지정 기준에서 브랜치와 간단한 계획만 만든다. commit/push 없음.
[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')][string]$Slug,[Parameter(Mandatory)][string]$Feature,[string]$Base,[switch]$NoPush)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')
if (-not $Base) { $Base=Get-BaseBranch }
$branch="integration/$Slug"
Assert-CleanTree
$target=$null
foreach ($ref in "refs/remotes/origin/$Base","refs/heads/$Base") {
    & git -C $RepoRoot rev-parse --verify --quiet "$ref^{commit}" | Out-Null
    if ($LASTEXITCODE -eq 0) { $target=$ref; break }
}
if (-not $target) { throw "기준 브랜치 없음: $Base. 최신 ref를 받으세요." }
& git -C $RepoRoot show-ref --verify --quiet "refs/heads/$branch"
if ($LASTEXITCODE -eq 0) { throw "이미 있는 브랜치: $branch" }
if (-not $PSCmdlet.ShouldProcess($branch,"선택 통합 생성 (기준 $Base)")) { return }
$tip=(& git -C $RepoRoot rev-parse "$target^{commit}").Trim()
Invoke-SafeBranchTransition -Path $RepoRoot -TargetCommit $tip -Branch $branch -CreateBranch
$dir=Get-RepoPath "Integrations/Active/Integration-$Slug"
New-Item -ItemType Directory -Path $dir -Force | Out-Null
Set-Content -LiteralPath (Join-Path $dir 'plan.md') -Encoding utf8NoBOM -Value "# $Feature`n`n- **Branch:** $branch`n- **Base:** $Base`n`n## Context`n$Feature`n`n필요한 작업을 선택해 공유한다. 소유 구역·부모 자식·순차 인계 제한 없음."
Write-Output "생성: $branch. 저장 요청 시 save-work를 사용하세요."
