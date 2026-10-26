# 이전 명령 이름 호환. 선택한 두 브랜치 사이 PR 생성/재사용. 계층·PM gate 없음.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Child,[string]$Parent,[switch]$DryRun)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')
if (-not $Parent) { $Parent=Get-BaseBranch }
$method=Get-PrFlow -Head $Parent -Base $Child
$gh=Get-GhState
if ($gh.State -ne 'Ready') { throw "gh 연결 필요: $($gh.Detail)" }
$repo=(Get-GitHubRepo).Slug
$refs=@{}
foreach ($branch in $Parent,$Child) {
    $rows=@(& git -C $RepoRoot ls-remote --heads origin "refs/heads/$branch")
    if ($LASTEXITCODE -ne 0 -or $rows.Count -ne 1 -or $rows[0] -notmatch '^([a-f0-9]{40,64})\s+') { throw "원격 브랜치 확인 실패: $branch" }
    $refs[$branch]=$Matches[1]
}
$raw=@(& $gh.Gh pr list --repo $repo --head $Parent --base $Child --state open --json url)
if ($LASTEXITCODE -ne 0) { throw 'PR 조회 실패' }
$existing=@(($raw -join "`n") | ConvertFrom-Json)
if ($existing.Count -gt 1) { throw '같은 방향 PR이 여러 개입니다.' }
if ($existing.Count) { [pscustomobject]@{ Status='Reused'; Url=$existing[0].url; DryRun=[bool]$DryRun }; return }
$summary="선택한 브랜치의 변경을 공유합니다.`n- 출발: $Parent ($($refs[$Parent]))`n- 대상: $Child ($($refs[$Child]))`n- 계층·인계 단계 없음. 대상 방향과 실제 변경을 검토합니다."
$body=New-PrTemplateBody -Summary $summary -MergeNote "병합 방식: $method. dev 대상은 지정 팀원 검토 후 반영."
if ($DryRun) { Write-Output "$Parent → $Child`n$body"; [pscustomobject]@{ Status='WouldCreate'; Url=$null; DryRun=$true }; return }
foreach ($branch in $Parent,$Child) {
    $latest=@(& git -C $RepoRoot ls-remote --heads origin "refs/heads/$branch")
    if ($LASTEXITCODE -ne 0 -or $latest.Count -ne 1 -or ($latest[0] -split '\s+')[0] -cne $refs[$branch]) { throw '브랜치가 바뀌었습니다. 최신 변경을 확인하세요.' }
}
$tmp=New-TemporaryFile
try {
    Set-Content -LiteralPath $tmp -Value $body -Encoding utf8NoBOM
    $url=& $gh.Gh pr create --repo $repo --head $Parent --base $Child --title "[sync] $Parent → $Child" --body-file $tmp
    if ($LASTEXITCODE -ne 0) { throw 'PR 생성 실패' }
    [pscustomobject]@{ Status='Created'; Url="$url".Trim(); DryRun=$false }
} finally { Remove-Item -LiteralPath $tmp }
