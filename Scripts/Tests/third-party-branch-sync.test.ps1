# 실제 private 에셋 대신 작은 로컬 Git/LFS fixture로 전환·실패·보존을 검증한다.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ScratchRoot)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scratch = [IO.Path]::GetFullPath($ScratchRoot)
if ($scratch.StartsWith($source + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $scratch -eq $source) { throw 'ScratchRoot는 프로젝트 밖이어야 합니다.' }
. (Join-Path $source 'Scripts/Lib/common.ps1')
$root = Join-Path $scratch ('branch-sync-' + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $root)
$subSource = Join-Path $root 'private source'
$parentSource = Join-Path $root 'public source'
$work = Join-Path $root '작업 폴더'
$script:cases = 0
function Git([string]$At, [string[]]$Arguments) { return (Invoke-TransitionGit $At $Arguments).Trim() }
function Put([string]$At, [string]$Name, [string]$Content) {
    $full = Join-Path $At $Name
    [void](New-Item -ItemType Directory -Path (Split-Path $full) -Force)
    [IO.File]::WriteAllText($full, $Content, [Text.UTF8Encoding]::new($false))
}
function Init([string]$At) {
    [void](New-Item -ItemType Directory -Path $At)
    [void](Git $At @('init', '-b', 'main'))
    [void](Git $At @('config', 'user.name', 'Transition Fixture'))
    [void](Git $At @('config', 'user.email', 'fixture@example.invalid'))
    [void](Git $At @('config', 'core.autocrlf', 'false'))
    [void](Git $At @('lfs', 'install', '--local'))
    Put $At '.gitattributes' "*.bin filter=lfs diff=lfs merge=lfs -text`n"
}
function Commit([string]$At, [string]$Message) {
    [void](Git $At @('add', '--all'))
    [void](Git $At @('commit', '-m', $Message))
    return Git $At @('rev-parse', 'HEAD')
}
function Check([string]$Name, [scriptblock]$Body) {
    & $Body
    $script:cases++
    Write-Output "PASS $Name"
}
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Reject([scriptblock]$Body, [string]$Pattern) {
    $message = ''
    try { & $Body } catch { $message = $_.Exception.Message }
    Assert ($message -match $Pattern) "예상 차단 '$Pattern', 실제 '$message'"
}
function Fingerprint {
    $index = Git $work @('rev-parse', '--path-format=absolute', '--git-path', 'index')
    return @((Git $work @('rev-parse', 'HEAD')), (Git $work @('branch', '--show-current')), (Get-FileHash -LiteralPath $index).Hash, (Get-FileHash -LiteralPath (Join-Path $work 'public.bin')).Hash) -join ':'
}
function Hydrate([string]$At) {
    [void](Git $At @('lfs', 'fetch', 'origin', 'HEAD'))
    [void](Git $At @('lfs', 'checkout'))
}

Init $subSource
Put $subSource '한글 파일.bin' ('private-one-' * 100)
Put $subSource 'asset.meta' 'guid: fixture-private'
$s1 = Commit $subSource 'private one'
Init $parentSource
Put $parentSource 'public.bin' ('public-one-' * 100)
Put $parentSource '.gitignore' "ignored/`nAssets/Plugins/`n"
[void](Git $parentSource @('-c', 'protocol.file.allow=always', 'submodule', 'add', $subSource, 'Assets/_ThirdParty'))
Hydrate (Join-Path $parentSource 'Assets/_ThirdParty')
$p1 = Commit $parentSource 'public one'
[void](Git $root @('clone', '--no-local', $parentSource, $work))
[void](Git $work @('-c', 'protocol.file.allow=always', 'submodule', 'update', '--init'))
$sub = Join-Path $work 'Assets/_ThirdParty'
Hydrate $work
Hydrate $sub
[void](Git $work @('config', 'submodule.recurse', 'true'))
[void](Git $work @('config', 'core.autocrlf', 'false'))
[void](Git $work @('config', 'user.name', 'Transition Fixture'))
[void](Git $work @('config', 'user.email', 'fixture@example.invalid'))
[void](Git $work @('branch', 'old', $p1))
Put $subSource '한글 파일.bin' ('private-two-' * 100)
$s2 = Commit $subSource 'private two'
[void](Git (Join-Path $parentSource 'Assets/_ThirdParty') @('fetch', '--no-recurse-submodules', 'origin'))
[void](Git (Join-Path $parentSource 'Assets/_ThirdParty') @('checkout', '--detach', $s2))
Hydrate (Join-Path $parentSource 'Assets/_ThirdParty')
Put $parentSource 'public.bin' ('public-two-' * 100)
Put $parentSource 'ignored/collision.txt' 'tracked target'
[void](Git $parentSource @('add', '-f', 'ignored/collision.txt'))
$p2 = Commit $parentSource 'public two'
[void](Git $work @('fetch', '--no-recurse-submodules', 'origin'))
[void](Git $work @('branch', 'next', $p2))

Check '정상 초기 상태' { Assert ((Get-ThirdPartyState $work -VerifyHash).State -eq 'Ready') '초기 상태' }
Check 'LFS 수신 실패는 public HEAD 이전 차단' {
    $before = Fingerprint
    [void](Git $work @('remote', 'set-url', 'origin', (Join-Path $root 'missing-remote')))
    try { Reject { Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch next } 'Git 명령 실패' }
    finally { [void](Git $work @('remote', 'set-url', 'origin', $parentSource)) }
    Assert ((Fingerprint) -eq $before) '실패가 출발 상태를 변경함'
}
Check 'private Git 객체 수신 실패는 전환 전 차단' {
    $before = Fingerprint
    [void](Git $sub @('remote', 'set-url', 'origin', (Join-Path $root 'missing-private')))
    try { Reject { Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch next } 'Git 명령 실패' }
    finally { [void](Git $sub @('remote', 'set-url', 'origin', $subSource)) }
    Assert ((Fingerprint) -eq $before) 'private 실패가 출발 상태를 변경함'
}
Check 'public untracked 보존' {
    Put $work 'keep.txt' '사용자 데이터'
    $before = Fingerprint
    try { Reject { Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch next } '저장하지 않은 변경'; Assert ((Fingerprint) -eq $before) 'HEAD 변경'; Assert ([IO.File]::ReadAllText((Join-Path $work 'keep.txt')) -eq '사용자 데이터') '파일 보존' }
    finally { Remove-Item -LiteralPath (Join-Path $work 'keep.txt') }
}
Check 'private staged 변경 보존' {
    Put $sub 'asset.meta' '사용자 meta'
    [void](Git $sub @('add', 'asset.meta'))
    try { Reject { Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch next } '저장하지 않은 변경'; Assert ([IO.File]::ReadAllText((Join-Path $sub 'asset.meta')) -eq '사용자 meta') 'meta 보존' }
    finally { [void](Git $sub @('restore', '--staged', '--worktree', 'asset.meta')) }
}
Check 'ignored 대상 충돌 보존' {
    Put $work 'ignored/collision.txt' 'ignored 사용자 파일'
    $before = Fingerprint
    try { Reject { Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch next } '충돌'; Assert ((Fingerprint) -eq $before) 'ignored 충돌 뒤 HEAD 변경'; Assert ([IO.File]::ReadAllText((Join-Path $work 'ignored/collision.txt')) -eq 'ignored 사용자 파일') 'ignored 보존' }
    finally { Remove-Item -LiteralPath (Join-Path $work 'ignored/collision.txt') }
}
Check '기존 index lock 보존' {
    $file = Join-Path (Git $work @('rev-parse', '--absolute-git-dir')) 'index.lock'
    [IO.File]::WriteAllText($file, 'fixture lock')
    try { Reject { Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch next } 'Git 잠금'; Assert (Test-Path -LiteralPath $file) '잠금 삭제됨' }
    finally { Remove-Item -LiteralPath $file }
}
Check '진행 중 merge 보존' {
    $file = Join-Path (Git $work @('rev-parse', '--absolute-git-dir')) 'MERGE_HEAD'
    [IO.File]::WriteAllText($file, $p2)
    try { Reject { Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch next } '진행 중 Git'; Assert (Test-Path -LiteralPath $file) 'MERGE_HEAD 삭제됨' }
    finally { Remove-Item -LiteralPath $file }
}
Check '협조적 동시 전환 잠금' {
    $file = Join-Path (Git $work @('rev-parse', '--absolute-git-dir')) 'harness-transition.guard'
    $held = [IO.File]::Open($file, 'OpenOrCreate', 'ReadWrite', 'None')
    try { Reject { Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch next } '다른 안전 전환' } finally { $held.Dispose() }
}
Check 'legacy 폴더 자동 삭제 없음' {
    Put $work 'Assets/Plugins/keep.txt' 'legacy 사용자 파일'
    try { Reject { Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch next } '잔여 폴더'; Reject { Remove-ThirdPartyLeftovers $work } '자동 잔여물'; Assert (Test-Path -LiteralPath (Join-Path $work 'Assets/Plugins/keep.txt')) 'legacy 삭제됨' }
    finally { Remove-Item -LiteralPath (Join-Path $work 'Assets/Plugins') -Recurse }
}
Check '대상 SHA 전환 및 제외 설정 무시' {
    [void](Git $sub @('config', 'lfs.fetchinclude', 'not-present/*'))
    [void](Git $work @('config', 'lfs.fetchexclude', '*'))
    Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch next
    Assert ((Git $work @('rev-parse', 'HEAD')) -eq $p2) 'public target'
    Assert ((Git $sub @('rev-parse', 'HEAD')) -eq $s2) 'private target'
    Assert ((Get-ThirdPartyState $work -VerifyHash).State -eq 'Ready') 'target ready'
    Assert ((Git $work @('config', '--local', 'submodule.recurse')) -eq 'true') '지속 재귀 설정 변경'
    Assert ((Git $work @('config', 'lfs.fetchexclude')) -eq '*') '사용자 제외 설정 변경'
}
Check '추적 파일 누락 판정' {
    $file = Join-Path $sub 'asset.meta'; $text = [IO.File]::ReadAllText($file)
    Remove-Item -LiteralPath $file
    try { Assert ((Get-ThirdPartyState $work).State -eq 'MissingFiles') '누락 판정' }
    finally { [IO.File]::WriteAllText($file, $text) }
}
Check 'LFS 포인터 잔존 판정' {
    $file = Join-Path $sub '한글 파일.bin'; $bytes = [IO.File]::ReadAllBytes($file)
    [IO.File]::WriteAllText($file, (Git $sub @('show', 'HEAD:한글 파일.bin')) + "`n")
    try { Assert ((Get-ThirdPartyState $work).State -eq 'LfsIncomplete') '포인터 판정' }
    finally { [IO.File]::WriteAllBytes($file, $bytes) }
}
Check 'private HEAD 불일치 판정' {
    [void](Git $sub @('checkout', '--detach', $s1))
    try { Assert ((Get-ThirdPartyState $work).State -eq 'HeadMismatch') 'HEAD 불일치' }
    finally { [void](Git $sub @('checkout', '--detach', $s2)); [void](Git $sub @('-c', 'lfs.fetchinclude=', 'lfs', 'checkout')) }
}
Check '미초기화 폴더의 상위 저장소 오인 방지' {
    $gitfile = Join-Path $sub '.git'; $held = Join-Path $root 'sub-git-held'
    Move-Item -LiteralPath $gitfile -Destination $held
    try { Assert (-not (Test-ThirdPartyRepository $work)) '상위 저장소를 private로 오인'; Assert ((Get-ThirdPartyState $work).State -eq 'Uninitialized') '미초기화 판정' }
    finally { Move-Item -LiteralPath $held -Destination $gitfile }
}
Check '전환 snapshot은 외부 ref 변경 감지' {
    $before = Get-TransitionSnapshot $work
    [void](Git $work @('update-ref', 'refs/heads/next', $p1, $p2))
    try { Reject { Assert-TransitionSnapshot $work $before } '변경|저장하지 않은' }
    finally { [void](Git $work @('update-ref', 'refs/heads/next', $p2, $p1)) }
}
Check '자식 Git 환경 격리' {
    $old = $env:GIT_DIR; $oldIndex = $env:GIT_INDEX_FILE
    try { $env:GIT_DIR = Join-Path $parentSource '.git'; $env:GIT_INDEX_FILE = Join-Path $root 'wrong-index'; Assert ((Git $work @('rev-parse', 'HEAD')) -eq $p2) 'Git 환경 오염'; Assert ($env:GIT_DIR -eq (Join-Path $parentSource '.git')) '부모 환경 변경' }
    finally { $env:GIT_DIR = $old; $env:GIT_INDEX_FILE = $oldIndex }
}
Check '캐시 손상은 checkout 전 차단' {
    $prepared = Prepare-TransitionRepository $sub $s1
    $file = $prepared.Files[0]
    $media = ((Git $sub @('lfs', 'env')) -split '\r?\n' | Where-Object { $_.StartsWith('LocalMediaDir=') }).Substring(14)
    $cache = Join-Path $media "$($file.oid.Substring(0,2))/$($file.oid.Substring(2,2))/$($file.oid)"
    $bytes = [IO.File]::ReadAllBytes($cache)
    $bad = [byte[]]$bytes.Clone(); $bad[0] = $bad[0] -bxor 1
    [IO.File]::WriteAllBytes($cache, $bad)
    $before = Fingerprint
    try { Reject { Invoke-SafeBranchTransition -Path $work -TargetCommit $p1 -Branch old } 'hash 불일치'; Assert ((Fingerprint) -eq $before) '손상 캐시로 전환됨' }
    finally { [IO.File]::WriteAllBytes($cache, $bytes) }
}
Check '새 브랜치 생성 경로' {
    Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch created -CreateBranch
    Assert ((Git $work @('branch', '--show-current')) -eq 'created') '새 branch'
}
Check '현재 branch FF 경로' {
    Invoke-SafeBranchTransition -Path $work -TargetCommit $p1 -Branch old
    Invoke-SafeBranchTransition -Path $work -TargetCommit $p2
    Assert ((Git $work @('branch', '--show-current')) -eq 'old') 'FF branch 변경'
}
Check '중간 로컬 tip 경유 뒤 FF' {
    [void](Git $work @('branch', 'stale', $p1))
    Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch stale
    Assert ((Git $work @('rev-parse', 'HEAD')) -eq $p2) '중간 tip FF 실패'
    Assert ((Get-ThirdPartyState $work -VerifyHash).State -eq 'Ready') '중간 tip 뒤 private 상태'
}
Check '원격 추적 브랜치 생성 경로' {
    [void](Git $work @('update-ref', 'refs/remotes/origin/tracked', $p2))
    Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 -Branch tracked -Track
    Assert ((Git $work @('rev-parse', '--abbrev-ref', '@{upstream}')) -eq 'origin/tracked') 'upstream 누락'
    Assert ((Get-ThirdPartyState $work -VerifyHash).State -eq 'Ready') '추적 브랜치 private 상태'
}
Check '사후 실패는 rollback·사용자 파일 삭제 없이 현재 HEAD 보고' {
    [void](Git $work @('branch', 'post-failure', $p1))
    Invoke-SafeBranchTransition -Path $work -TargetCommit $p1 -Branch post-failure
    Put $work 'ignored/keep.txt' '사후 실패 보존'
    $complete = ${function:Complete-TransitionRepository}
    function Complete-TransitionRepository {
        param($Preparation)
        & $complete $Preparation
        if ($Preparation.Path -eq $work) { throw 'fixture 사후 검증 실패' }
    }
    try {
        Reject { Invoke-SafeBranchTransition -Path $work -TargetCommit $p2 } "캐시 반영·사후 검증.*fixture 사후 검증 실패.*public=$p2, private=$s2"
        Assert ((Git $work @('rev-parse', 'HEAD')) -eq $p2) '실패 뒤 자동 rollback'
        Assert ((Get-ThirdPartyState $work -VerifyHash).State -eq 'Ready') '실패 뒤 private 변경'
        Assert ([IO.File]::ReadAllText((Join-Path $work 'ignored/keep.txt')) -eq '사후 실패 보존') '사후 실패 파일 삭제'
    } finally { Remove-Item -LiteralPath (Join-Path $work 'ignored/keep.txt') }
}
Check '사전 수신 이후에는 오프라인 캐시만 사용' {
    $prepared = Prepare-TransitionRepository $sub $s2
    $public = Prepare-TransitionRepository $work $p2
    [void](Git $sub @('remote', 'set-url', 'origin', (Join-Path $root 'offline')))
    [void](Git $work @('remote', 'set-url', 'origin', (Join-Path $root 'offline')))
    try { Update-ThirdParty -Path $work -Preparation $prepared; Complete-TransitionRepository $public }
    finally { [void](Git $sub @('remote', 'set-url', 'origin', $subSource)); [void](Git $work @('remote', 'set-url', 'origin', $parentSource)) }
}
Write-Output "Third-party branch sync: PASS ($script:cases cases)"
Write-Output "Fixture: $root"
