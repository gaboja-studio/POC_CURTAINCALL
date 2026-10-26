# 사용자 저장 요청: 필요한 검사 후 현재 작업 브랜치에 commit/push. 자동 테스트 반복 없음.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Message,[switch]$DryRun)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')
$branch=(& git -C $RepoRoot branch --show-current).Trim()
if ($branch -notmatch '^(feat|fix|refactor|resource|integration|tests)/') { throw '작업 브랜치에서 저장하세요. dev는 PR로 반영합니다.' }
$status=@(& git -C $RepoRoot status --porcelain --untracked-files=all --ignore-submodules=all)
if ($LASTEXITCODE -ne 0) { throw '작업 상태 확인 실패' }
if (-not $status.Count) { Write-Output '저장할 변경이 없습니다.'; exit 0 }
Assert-CommitIdentity
if (-not (Test-VerificationEvidence -Branch $branch)) {
    & ([Environment]::ProcessPath) -NoProfile -File (Join-Path $PSScriptRoot 'check-work.ps1') -NoRecord
    if ($LASTEXITCODE -ne 0) { throw '필요한 검사 실패·생략으로 저장하지 않았습니다.' }
} else { Write-Output '현재 파일과 일치하는 검사 결과를 재사용합니다.' }
if (-not (Test-VerificationEvidence -Branch $branch)) { throw '검사 결과 이후 파일이 바뀌었습니다. 현재 변경을 다시 검사하세요.' }
$before=Get-WorktreeFingerprint
Write-Output '저장할 변경:'
$status | ForEach-Object { Write-Output "  $_" }
if ($DryRun) { Write-Output "미리보기: $Message (commit/push 없음)"; exit 0 }
Assert-CommitIdentity
if ((Get-WorktreeFingerprint) -ne $before) { throw '검사 이후 파일이 바뀌었습니다.' }
& git -C $RepoRoot add -A
if ($LASTEXITCODE -ne 0) { throw 'git add 실패' }
& git -C $RepoRoot commit -m $Message
if ($LASTEXITCODE -ne 0) { throw 'git commit 실패' }
& git -C $RepoRoot push -u origin $branch
if ($LASTEXITCODE -ne 0) { throw 'push 실패. 로컬 저장 기록은 남아 있습니다.' }
Write-Output "저장 완료: $branch"
