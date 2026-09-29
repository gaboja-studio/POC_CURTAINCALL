# PM 전용. 원격 최신 dev에서 integration/<slug> 브랜치와 Integrations/Active/Integration-<slug>/ 기록을 만든다.
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')][string]$Slug,
    [Parameter(Mandatory)][string]$Feature,
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$base = Get-BaseBranch
$branch = "integration/$Slug"
$dir = Get-RepoPath "Integrations/Active/Integration-$Slug"
$template = Get-RepoPath 'Harness/Core/Templates/Integration'

Assert-CleanTree -Hint '통합 브랜치를 만들기 전에 현재 작업을 저장하거나 정리하세요.'
& git -C $RepoRoot fetch origin --prune --quiet
if ($LASTEXITCODE -ne 0) { throw 'git fetch 실패' }
& git -C $RepoRoot rev-parse --verify --quiet "refs/heads/$branch" | Out-Null
if ($LASTEXITCODE -eq 0) { throw "로컬에 이미 있는 브랜치: $branch" }
& git -C $RepoRoot rev-parse --verify --quiet "refs/remotes/origin/$branch" | Out-Null
if ($LASTEXITCODE -eq 0) { throw "원격에 이미 있는 브랜치: $branch" }
$startPoint = "origin/$base"
& git -C $RepoRoot rev-parse --verify --quiet "refs/remotes/$startPoint" | Out-Null
if ($LASTEXITCODE -ne 0) { $startPoint = $base }

$pm = Get-CurrentGitHubUser
Write-Output "브랜치:   $branch (시작: $startPoint)"
Write-Output "기록 폴더: Integrations/Active/Integration-$Slug/"
Write-Output "PM:       $(if ($pm) { "@$pm" } else { '(확인 불가 — gh 미연결)' })"
if ($pm -and $pm -ne (Get-PmAccount)) { Write-Output "[경고] 현재 GitHub 계정(@$pm)이 Facts/team.md의 PM과 다릅니다." }

if (-not $PSCmdlet.ShouldProcess($branch, '통합 브랜치 생성')) { return }
Assert-CommitIdentity

& git -C $RepoRoot switch -c $branch $startPoint
if ($LASTEXITCODE -ne 0) { throw 'git switch 실패' }

New-Item -ItemType Directory -Path $dir -Force | Out-Null
$today = Get-Date -Format 'yyyy-MM-dd'
foreach ($file in Get-ChildItem -LiteralPath $template -File) {
    $text = (Get-Content -LiteralPath $file.FullName -Raw).Replace('FEATURE_SLUG', $Slug).Replace('YYYY-MM-DD', $today)
    $text = $text.Replace('- **Feature:**', "- **Feature:** $Feature")
    if ($pm) { $text = $text.Replace('- **PM:**', "- **PM:** @$pm") }
    Set-Content -LiteralPath (Join-Path $dir $file.Name) -Value $text -Encoding utf8NoBOM -NoNewline
}

& git -C $RepoRoot add -- $dir
& git -C $RepoRoot commit -m "chore: start integration $Slug" -- $dir
if ($LASTEXITCODE -ne 0) { throw 'git commit 실패' }
if (-not $NoPush) {
    & git -C $RepoRoot push -u origin $branch
    if ($LASTEXITCODE -ne 0) { throw 'git push 실패 (브랜치 보호 우회 목록에 PM 계정이 있는지 확인)' }
}
Write-Output ''
Write-Output "완료: $branch"
Write-Output "다음: Integration meta.md의 목표·병합 예정 시각을 채우고, Task를 준비하세요 (scaffold-task)."
