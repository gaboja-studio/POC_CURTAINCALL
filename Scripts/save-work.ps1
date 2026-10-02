# 작업자의 "저장해줘". 구역 → 문서/규칙 → 컴파일 검사 후 커밋하고 내 작업 브랜치에 push.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Message,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$branch = (& git -C $RepoRoot branch --show-current).Trim()
if ($branch -match '^(dev|integration/.+|builds/.+)$') { throw "$branch 에는 이 방법으로 저장하지 않습니다(PM 절차 사용)." }
if ($branch -notmatch '^(feat|fix|refactor|resource|tests)/') { throw "규칙에 없는 브랜치입니다: $branch" }

$status = & git -C $RepoRoot status --porcelain
if (-not $status) { Write-Output '저장할 변경이 없습니다.'; exit 0 }
$before = Get-WorktreeFingerprint

try { Assert-CommitIdentity } catch { Write-Output "[위반] $($_.Exception.Message)"; exit 1 }
if (Test-VerificationEvidence -Branch $branch) {
    $editorOutput = @(& ([System.Environment]::ProcessPath) -NoProfile -File (Join-Path $PSScriptRoot 'verify-unity.ps1') -ProjectPath $RepoRoot 2>&1)
    $editorCode = $LASTEXITCODE
    if ($editorCode -ne 0) {
        $editorOutput | ForEach-Object { Write-Output $_ }
        Write-Output "검증 증거와 파일은 일치하지만 Unity 에디터 상태를 확인할 수 없습니다. 프로젝트 '$RepoRoot' 연결을 확인한 뒤 다시 저장하세요."
        exit $(if ($editorCode -eq 2) { 2 } else { 1 })
    }
    if ((Get-WorktreeFingerprint) -ne $before) { throw '에디터 확인 중 파일 내용이 바뀌었습니다. 다시 검증하세요.' }
    Write-Output '현재 파일·인덱스와 에디터 상태가 일치하는 검증 결과를 재사용합니다.'
} else {
    if ($branch -notlike 'tests/*') {
        & (Join-Path $PSScriptRoot 'verify-scope.ps1')
        if ($LASTEXITCODE -ne 0) { Write-Output '구역 검사 실패: 저장하지 않았습니다.'; exit 1 }
    }
    & (Join-Path $PSScriptRoot 'verify-fast.ps1')
    if ($LASTEXITCODE -ne 0) { Write-Output '문서/규칙 검사 실패: 저장하지 않았습니다.'; exit 1 }
    $compileOutput = @(& ([System.Environment]::ProcessPath) -NoProfile -File (Join-Path $PSScriptRoot 'verify-unity.ps1') -ProjectPath $RepoRoot -Compile 2>&1)
    $compileCode = $LASTEXITCODE
    $compileOutput | ForEach-Object { Write-Output $_ }
    if ($compileCode -eq 2) {
        Write-Output "컴파일 검증 환경 차단: Unity에서 프로젝트 '$RepoRoot'를 열고 연결을 확인한 뒤 다시 저장하세요."
        exit 2
    }
    if ($compileCode -ne 0 -or ($compileOutput -join "`n") -notmatch 'Compile: PASS') {
        Write-Output '컴파일 검증 실패: 저장하지 않았습니다.'
        exit 1
    }
    if ((Get-WorktreeFingerprint) -ne $before) { throw '검사 중 파일 내용이 바뀌었습니다. 다시 검증하세요.' }
    Save-VerificationEvidence -Branch $branch
}

Write-Output ''
Write-Output '저장할 변경:'
$status | ForEach-Object { Write-Output "  $_" }
if ($DryRun) { Write-Output "미리보기만 했습니다. 커밋 메시지: $Message (실제 저장 시 현재 변경을 다시 검사합니다)"; exit 0 }

Assert-CommitIdentity
if ((Get-WorktreeFingerprint) -ne $before) { throw '검사 중 파일 내용이 바뀌었습니다. 다시 검증하세요.' }
& git -C $RepoRoot add -A
& git -C $RepoRoot commit -m $Message
if ($LASTEXITCODE -ne 0) { throw 'git commit 실패' }
& git -C $RepoRoot push -u origin $branch
if ($LASTEXITCODE -ne 0) { throw 'git push 실패. 인터넷·GitHub 연결을 확인하세요(저장 기록은 내 PC에 남아 있음).' }
Write-Output "저장 완료: $branch — $Message"
