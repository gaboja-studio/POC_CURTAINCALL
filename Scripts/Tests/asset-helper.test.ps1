# Local Git/LFS fixtures only. Never modifies the project's actual repositories or GitHub.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ScratchRoot)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $source 'Scripts/Asset-helper/core.ps1')
if (Test-PathInside -Child $ScratchRoot -Parent $source) { throw 'ScratchRoot must be outside project.' }
$fixture=Join-Path $ScratchRoot ('asset-helper-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$private=Join-Path $fixture 'private.git'
$public=Join-Path $fixture 'public.git'
$author=Join-Path $fixture 'author'
$work=Join-Path $fixture 'work'
$git=(Get-Command git -CommandType Application | Select-Object -First 1).Source
$script:cases=0
function Git([string]$Path,[string[]]$Options) { return (Invoke-AhGit $Path $Options).Trim() }
function Put([string]$Path,[string]$Name,[string]$Text) {
    $full=Join-Path $Path $Name
    New-Item -ItemType Directory -Path (Split-Path $full) -Force | Out-Null
    [IO.File]::WriteAllText($full,$Text,[Text.UTF8Encoding]::new($false))
}
function Check([string]$Name,[scriptblock]$Action) { & $Action; $script:cases++; Write-Output "PASS $Name" }
function Assert([bool]$Ok,[string]$Message) { if (-not $Ok) { throw $Message } }
function Reject([scriptblock]$Action,[string]$Pattern) {
    $message=''
    try { & $Action | Out-Null } catch { $message=$_.Exception.Message }
    Assert ($message -match $Pattern) "expected rejection $Pattern; actual $message"
}
function Commit([string]$Path,[string]$Text='fixture') {
    [void](Git $Path @('add','--all'))
    [void](Git $Path @('commit','-m',$Text))
    return Git $Path @('rev-parse','HEAD')
}
function Configure([string]$Path) {
    [void](Git $Path @('config','user.name','Asset Fixture'))
    [void](Git $Path @('config','user.email','fixture@example.invalid'))
    [void](Git $Path @('config','core.autocrlf','false'))
    [void](Git $Path @('config','protocol.file.allow','always'))
    [void](Git $Path @('lfs','install','--local'))
}
# The production core verifies the private repository destination and gh identity.
# Replace only those integration boundaries in this local fixture, retaining Git operations.
function Assert-AhRemote([string]$ProjectPath) {
    $origin=Git (Join-Path $ProjectPath 'Assets/_ThirdParty') @('remote','get-url','origin')
    Assert ($origin -eq $private) 'fixture private destination changed'
}
function Get-AhIdentity { return [pscustomobject]@{ Name='Asset Fixture'; Email='fixture@example.invalid'; Login='fixture' } }
function Get-AhParentPushTarget([string]$ProjectPath,[string]$Branch) { return 'origin' }
function New-AhDevReview([string]$Branch,[string]$Message) { return 'https://github.com/fixture/game/pull/1' }
function New-AhPrivateReview([string]$Branch,[string]$Message) { return 'https://github.com/fixture/assets/pull/1' }
& $git init --bare -b main $private | Out-Null
& $git init --bare -b dev $public | Out-Null
New-Item -ItemType Directory -Path $author,$work | Out-Null
[void](Git $author @('init','-b','main'))
Configure $author
Put $author '.gitattributes' "*.bin filter=lfs diff=lfs merge=lfs -text`n"
Put $author 'asset.bin' ('version one '*100)
Put $author 'asset.bin.meta' 'guid: original'
$s1=Commit $author 'asset one'
[void](Git $author @('remote','add','origin',$private))
[void](Git $author @('push','-u','origin','main'))
[void](Git $work @('init','-b','dev'))
Configure $work
Put $work '.gitignore' ".harness/`nTemp/`n"
Put $work '.env.example' "FIXTURE=true`n"
Put $work 'AGENTS.md' "fixture root file`n"
Put $work 'game.txt' 'original game'
[void](Git $work @('remote','add','origin',$public))
[void](Git $work @('-c','protocol.file.allow=always','submodule','add',$private,'Assets/_ThirdParty'))
$sub=Join-Path $work 'Assets/_ThirdParty'
Configure $sub
[void](Git $sub @('lfs','fetch','origin','HEAD'))
[void](Git $sub @('lfs','checkout'))
[void](Git $sub @('restore','.gitattributes')) # Fixture setup must leave the cloned attributes unchanged.
$p1=Commit $work 'game one'
[void](Git $work @('push','-u','origin','dev'))
[void](Git $work @('switch','-c','resource/test'))
[void](Git $work @('push','-u','origin','resource/test'))
Check 'preview is read only; detached private HEAD is normal' {
    [void](Git $sub @('checkout','--detach',$s1))
    $index=Get-AhIndexHash $work
    $a=Get-AhState $work; $b=Get-AhState $work
    Assert ($a.status -eq 'ready' -and $a.privateBranch -eq '') 'detached is treated as error'
    Assert ($a.snapshot -eq $b.snapshot -and (Get-AhIndexHash $work) -eq $index) 'status mutated index or snapshot unstable'
}
Check 'stale preview refuses mutations' {
    $before=Get-AhState $work
    Put $sub 'asset.bin' 'different local file'
    Reject { Invoke-AhSync $work $before.snapshot } '미리보기'
    [void](Git $sub @('restore','asset.bin')) # Fixture only, intentionally restore known synthetic content.
    [void](Git $sub @('lfs','checkout'))
}
Check 'publish preserves unrelated staged and unstaged public changes' {
    Put $work 'game.txt' 'staged game'
    [void](Git $work @('add','game.txt'))
    Put $work 'game.txt' 'unstaged game'
    $staged=Git $work @('diff','--cached','--binary')
    Put $sub 'asset.bin' ('version two '*100)
    $state=Get-AhState $work
    $result=Invoke-AhPublish $work $state.snapshot '에셋 재질 변경'
    Assert ($result.kind -eq 'dev_pending') 'work branch upload was called team delivery'
    Assert ([bool]$result.share.teamBranch -and [bool]$result.share.teamPr) "asset-only dev PR was not prepared: $($result.message)"
    Assert ($result.share.teamBranch -eq 'resource/test') 'pointer-only task branch was not reused'
    Assert ((Git $work @('diff','--name-only',"$p1..HEAD")) -eq 'Assets/_ThirdParty') 'other game files committed'
    Assert ((Git $work @('diff','--cached','--binary')) -eq $staged) 'unrelated index changed'
    Assert ((Get-Content -LiteralPath (Join-Path $work 'game.txt') -Raw) -eq 'unstaged game') 'unstaged file overwritten'
    $head=Git $sub @('rev-parse','HEAD')
    Assert ((Git $sub @('ls-remote','origin','refs/heads/main')) -match $head) 'private not published first'
    # Also model the saved remote branch produced by a private integration review.
    [void](Git $sub @('push','origin',"${head}:refs/heads/$($result.share.privateBranch)"))
}
$p2=Git $work @('rev-parse','HEAD')
$s2=Git $sub @('rev-parse','HEAD')
Check 'isolated PR preparation materializes root files and removes its temporary worktree' {
    $before=@(Get-AhWorktrees $work).Count
    $index=Get-AhIndexHash $work
    $branch=New-AhAssetShareBranch $work $s2 '별도 공유 브랜치 검증' (Get-AhIdentity)
    Assert (@(Get-AhWorktrees $work).Count -eq $before) 'temporary worktree leaked'
    Assert (@(Get-ChildItem (Get-AhLocalDir $work) -Directory -Filter 'dev-pr-*').Count -eq 0) 'temporary directory leaked'
    Assert ((Get-AhIndexHash $work) -eq $index) 'user index changed during isolated PR preparation'
    # Retain the helper-owned receipt for the merge/cleanup checks below.
    $path=Get-AhReceiptPath $work 'resource/test'
    $record=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
    $record.teamBranch=$branch
    $record.teamCommit=Git $work @('rev-parse',"refs/heads/$branch")
    Save-AhReceipt $path $record
}
Check 'work branch upload remains team pending and its dev PR contains only the asset pointer' {
    $report=Get-AhShareReport $work 'resource/test'
    Assert (-not $report.teamReady -and $report.stage -eq 'dev_pending') 'unmerged work was called team delivery'
    $receipt=Get-Content -LiteralPath (Get-AhReceiptPath $work 'resource/test') -Raw | ConvertFrom-Json
    $tip=(Git $work @('ls-remote','origin',"refs/heads/$($receipt.teamBranch)")) -split '\s+' | Select-Object -First 1
    $paths=Git $work @('diff','--name-only',"$p1..$tip")
    Assert ($paths -eq 'Assets/_ThirdParty') 'non-asset code included in team PR'
    Assert ([bool](Git $work @('ls-remote','origin',"refs/heads/$($receipt.teamBranch)"))) 'pending review branch deleted'
}
Check 'team completion is reported only after the exact asset pointer reaches remote dev' {
    $receipt=Get-Content -LiteralPath (Get-AhReceiptPath $work 'resource/test') -Raw | ConvertFrom-Json
    $script:deliveredReceipt=$receipt
    [void](Git $work @('push','origin',"$($receipt.teamBranch):refs/heads/dev")) # A simulated reviewed PR merge in the local bare fixture.
    $report=Get-AhShareReport $work 'resource/test'
    Assert ($report.teamReady -and $report.stage -eq 'team_shared') 'verified dev pointer not recognized'
    $result=Invoke-AhPublish $work (Get-AhState $work).snapshot '팀 반영 확인'
    Assert ($result.kind -eq 'team_shared') 'verified team delivery not reported'
}
Check 'merged helper branches are cleaned locally and remotely while task branch and assets stay' {
    $r=$script:deliveredReceipt
    Assert (-not (Git $work @('ls-remote','origin',"refs/heads/$($r.teamBranch)"))) 'merged public helper branch still remote'
    Assert ((Invoke-AhGit $work @('show-ref','--verify',"refs/heads/$($r.teamBranch)") -AllowFailure).Code -ne 0) 'public helper branch still local'
    Assert ((Invoke-AhGit $sub @('show-ref','--verify',"refs/heads/$($r.privateBranch)") -AllowFailure).Code -ne 0) 'private saved branch still local'
    Assert (-not (Git $sub @('ls-remote','origin',"refs/heads/$($r.privateBranch)"))) 'private saved branch still remote'
    Assert ((Git $work @('symbolic-ref','--short','HEAD')) -eq 'resource/test') 'user task branch changed'
    Assert ([bool](Git $work @('ls-remote','origin','refs/heads/resource/test'))) 'user task branch deleted'
    Assert ((Git $sub @('rev-parse','HEAD')) -eq $s2) 'asset version changed during cleanup'
    Assert ((Get-Content (Join-Path $work 'game.txt') -Raw) -eq 'unstaged game') 'user game edits lost during cleanup'
}
Check 'cleanup preserves additional commits, dirty helper folders and recovery branches' {
    # Recreate a helper branch, then give it extra work. Even a completed receipt cannot delete it.
    $r=$script:deliveredReceipt
    [void](Git $work @('branch',$r.teamBranch,$r.teamCommit))
    [void](Git $work @('update-ref',"refs/heads/$($r.teamBranch)",$p2,$r.teamCommit))
    Reject { Remove-AhDeliveredBranch $work $r.teamBranch $r.teamCommit 'refs/remotes/origin/dev' } '추가 작업'
    Assert ((Git $work @('rev-parse',"refs/heads/$($r.teamBranch)")) -eq $p2) 'advanced helper branch deleted'
    # Put the known synthetic ref back for a dirty-folder check. Never do this to user refs.
    [void](Git $work @('update-ref',"refs/heads/$($r.teamBranch)",$r.teamCommit,$p2))
    $temp=Join-Path (Get-AhLocalDir $work) ('dev-pr-'+[guid]::NewGuid().ToString('N'))
    [void](Git $work @('worktree','add',$temp,$r.teamBranch))
    Put $temp 'my-unfinished.txt' 'keep me'
    Reject { Remove-AhDeliveredBranch $work $r.teamBranch $r.teamCommit 'refs/remotes/origin/dev' } '변경이 있는'
    Assert ((Get-Content (Join-Path $temp 'my-unfinished.txt') -Raw) -eq 'keep me') 'dirty worktree content deleted'
    Remove-Item -LiteralPath (Join-Path $temp 'my-unfinished.txt') # Known fixture-only file.
    Remove-AhDeliveredBranch $work $r.teamBranch $r.teamCommit 'refs/remotes/origin/dev'
    Assert (-not (Test-Path -LiteralPath $temp)) 'clean helper worktree not removed'
    $backup=New-AhKeepRef $sub $s2 'kept'
    $safe=@{ teamReady=$true; parentBranch='resource/test'; privateBranch=$backup; privateCommit=$s2 }
    Assert ((Invoke-AhShareCleanup $work $safe).done) 'backup-only cleanup failed'
    Assert ((Git $sub @('rev-parse',$backup)) -eq $s2) 'recovery branch deleted'
}
Check 'cleanup preserves a remote helper branch that advanced after its recorded upload' {
    $r=$script:deliveredReceipt
    [void](Git $work @('branch',$r.teamBranch,$r.teamCommit))
    [void](Git $work @('push','origin',"${p2}:refs/heads/$($r.teamBranch)"))
    Reject { Remove-AhDeliveredBranch $work $r.teamBranch $r.teamCommit 'refs/remotes/origin/dev' } '원격 브랜치에 추가'
    Assert ((Git $work @('ls-remote','origin',"refs/heads/$($r.teamBranch)")) -match $p2) 'advanced remote helper branch deleted'
    [void](Git $work @('push','origin','--delete',$r.teamBranch)) # Synthetic test teardown only.
    [void](Git $work @('update-ref','-d',"refs/heads/$($r.teamBranch)",$r.teamCommit))
}
Check 'pointer-only helper branch is cleaned after a squash merge with different commit IDs' {
    $r=$script:deliveredReceipt
    $tree=Git $work @('rev-parse',"$($r.teamCommit)^{tree}")
    $different=Git $work @('commit-tree',$tree,'-p',$p1,'-m','different synthetic commit for squash cleanup')
    Assert ($different -ne $r.teamCommit) 'fixture did not change commit ID'
    [void](Git $work @('branch',$r.teamBranch,$different))
    [void](Git $work @('push','origin',"${different}:refs/heads/$($r.teamBranch)"))
    $record=@{ teamReady=$true; parentBranch='resource/test'; teamBranch=$r.teamBranch; teamCommit=$different; privateCommit=$s2 }
    $clean=Invoke-AhShareCleanup $work $record
    Assert ($clean.done) "squash delivery cleanup failed: $($clean.messages)"
    Assert (-not (Git $work @('ls-remote','origin',"refs/heads/$($r.teamBranch)"))) 'squashed helper remote still present'
}
Check 'refresh after switching branches also finishes cleanup for a previous share' {
    $path=Get-AhReceiptPath $work 'resource/previous-share'
    try {
        $record=@{ parentBranch='resource/previous-share'; publicCommit=$p2; privateCommit=$s2; teamReady=$false; cleanup=@{ done=$false } }
        Save-AhReceipt $path $record
        $result=Update-AhOtherShareCleanup $work 'resource/test'
        $saved=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
        Assert ($result.done -and $saved.teamReady -and $saved.cleanup.done) 'previous share cleanup was not resumed'
    } finally { Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue }
}
Check 'unreachable dev is never shown as team complete even after a previous successful check' {
    $original=(Get-Item Function:Test-AhDevContainsAsset).ScriptBlock
    try {
        function Test-AhDevContainsAsset([string]$ProjectPath,[string]$Commit) { throw 'fixture disconnected' }
        $report=Get-AhShareReport $work 'resource/test'
        Assert (-not $report.teamReady -and $report.stage -eq 'unknown') 'stale success reused when dev could not be verified'
    } finally { Set-Item Function:Test-AhDevContainsAsset $original }
}
Check 'legacy work-branch upload without a receipt still checks dev and can finish the team check' {
    $path=Get-AhReceiptPath $work 'resource/test'
    Remove-Item -LiteralPath $path # Simulate the old helper deleting its completed-push receipt.
    $report=Get-AhShareReport $work 'resource/test' (Get-AhState $work)
    Assert ($report.teamReady) 'legacy status did not verify actual dev'
    $result=Invoke-AhPublish $work (Get-AhState $work).snapshot '이전 도우미 공유 이어 확인'
    Assert ($result.kind -eq 'team_shared') 'legacy pointer-only save could not resume team confirmation'
}
Check 'after public branch changes, match recorded version rather than latest main' {
    # Keep unrelated game edits in the fixture so their preservation is exercised during sync too.
    [void](Git $work @('update-ref','refs/heads/resource/test',$p1,$p2))
    [void](Git $work @('update-index','--cacheinfo',"160000,$s1,Assets/_ThirdParty"))
    $state=Get-AhState $work
    Assert ($state.status -eq 'mismatch') 'fixture not mismatched'
    $before=Git $work @('rev-parse','HEAD')
    $unchanged=Invoke-AhPublish $work $state.snapshot '버전 불일치를 저장으로 처리하지 않기'
    Assert ($unchanged.kind -eq 'unchanged' -and (Git $work @('rev-parse','HEAD')) -eq $before) 'clean mismatch was published as a pointer downgrade'
    $result=Invoke-AhSync $work $state.snapshot
    Assert ((Git $sub @('rev-parse','HEAD')) -eq $s1) 'sync selected latest instead of pinned'
    Assert ($result.kept -and (Git $sub @('rev-parse',$result.kept)) -eq $s2) 'old HEAD not preserved'
    Assert ((Get-Content -LiteralPath (Join-Path $work 'game.txt') -Raw) -eq 'unstaged game') 'game file changed during sync'
}
Check 'dirty sync needs explicit keep choice and includes untracked files' {
    Put $sub 'asset.bin' 'my unfinished edit'
    Put $sub 'new asset.txt' 'untracked content'
    $state=Get-AhState $work
    Reject { Invoke-AhSync $work $state.snapshot } '저장하지 않은'
    $result=Invoke-AhSync $work $state.snapshot $true
    Assert ([bool]$result.stash) 'no backup for edits'
    $expectedHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes('my unfinished edit'))).ToLowerInvariant()
    Assert ((Git $sub @('show',"$($result.stash):asset.bin")) -match $expectedHash) 'tracked LFS edit not backed up'
    Assert ((Git $sub @('show',"$($result.stash)^3:new asset.txt")) -eq 'untracked content') 'untracked edit not backed up'
    Assert (@(Get-AhChanges $sub).Count -eq 0) 'sync did not clean working files after backup'
    Assert ((Git $sub @('rev-parse','HEAD')) -eq $s1) 'wrong sync commit'
}
Check 'saved work can be restored by a backup ID without terminal commands' {
    $state=Get-AhState $work
    Assert ($state.backups.Count -eq 1) 'backup not listed'
    $restored=Invoke-AhRestore $work $state.snapshot $state.backups[0].id
    Assert ($restored.kind -eq 'restored') 'restore incomplete'
    Assert ((Get-Content -LiteralPath (Join-Path $sub 'asset.bin') -Raw) -eq 'my unfinished edit') 'tracked restore failed'
    Assert ((Get-Content -LiteralPath (Join-Path $sub 'new asset.txt') -Raw) -eq 'untracked content') 'untracked restore failed'
    # Preserve once more for subsequent fixture checks; no manual file deletion needed.
    Invoke-AhSync $work (Get-AhState $work).snapshot $true | Out-Null
}
Check 'open Unity lock blocks sync before changing files' {
    $lockPath=Join-Path $work 'Temp/UnityLockfile'
    New-Item -ItemType Directory -Path (Split-Path $lockPath) -Force | Out-Null
    $handle=[IO.File]::Open($lockPath,'OpenOrCreate','ReadWrite','None')
    try { Reject { Invoke-AhSync $work (Get-AhState $work).snapshot } 'Unity를 닫은' }
    finally { $handle.Dispose() }
}
Check 'new public branch with unrelated unpublished commits is refused' {
    [void](Git $work @('switch','-c','resource/unpublished'))
    Put $work 'new-game.txt' 'not shared game commit'
    [void](Commit $work 'unrelated synthetic game change')
    Put $sub 'asset.bin' 'new asset edit'
    $head=Git $sub @('rev-parse','HEAD')
    Reject { Invoke-AhPublish $work (Get-AhState $work).snapshot '다른 변경' } '아직 공유하지 않은'
    Assert ((Git $sub @('rev-parse','HEAD')) -eq $head) 'private commit created before public refusal'
}
Check 'missing meta refuses publish before any private save' {
    [void](Git $work @('push','-u','origin','resource/unpublished'))
    Put $sub 'missing.txt' 'new asset'
    Reject { Invoke-AhPublish $work (Get-AhState $work).snapshot '새 에셋' } '연결 파일'
}
Check 'divergent private work is preserved in a review branch without public pointer change' {
    Put $sub 'missing.txt.meta' 'guid: missing-now-present'
    $before=Git $work @('rev-parse','HEAD')
    $result=Invoke-AhPublish $work (Get-AhState $work).snapshot '이전 버전에서 에셋 추가'
    Assert ($result.kind -eq 'private_pending' -and -not $result.share.teamReady) 'divergent assets were called team delivery'
    Assert ((Git $work @('rev-parse','HEAD')) -eq $before) 'public pointer changed before review'
    Assert ([bool](Git $sub @('ls-remote','origin',"refs/heads/$($result.kept)"))) 'review branch not uploaded'
}
# Separate clone for a failure after public commit, before push. This is the important retry boundary.
$retry=Join-Path $fixture 'retry'
[void](Git $fixture @('-c','core.autocrlf=false','clone','--branch','resource/test',$public,$retry))
Configure $retry
[void](Git $retry @('-c','protocol.file.allow=always','-c','core.autocrlf=false','submodule','update','--init'))
$retrySub=Join-Path $retry 'Assets/_ThirdParty'
Configure $retrySub
[void](Git $retrySub @('restore','.gitattributes'))
[void](Git $retrySub @('lfs','fetch','origin','HEAD'))
[void](Git $retrySub @('lfs','checkout'))
[void](Git $retry @('switch','-c','resource/retry'))
[void](Git $retry @('push','-u','origin','resource/retry'))
Put $retrySub 'retry.txt' 'new asset to publish'
Put $retrySub 'retry.txt.meta' 'guid: retry'
$script:AhRealGit=(Get-Item Function:Invoke-AhGit).ScriptBlock
$script:AhFailPush=$true
function Invoke-AhGit {
    param([string]$Path,[string[]]$Arguments,[switch]$AllowFailure,[hashtable]$Environment=@{})
    if ($script:AhFailPush -and $Path -eq $retry -and $Arguments[0] -eq 'push') { $script:AhFailPush=$false; throw 'fixture upload interruption' }
    & $script:AhRealGit -Path $Path -Arguments $Arguments -AllowFailure:$AllowFailure -Environment $Environment
}
Check 'public push failure leaves the private asset and local pointer commit recoverable' {
    Reject { Invoke-AhPublish $retry (Get-AhState $retry).snapshot '이어 올리기 확인' } 'fixture upload interruption'
    $pointer=Git $retry @('rev-parse','HEAD')
    $asset=Git $retrySub @('rev-parse','HEAD')
    Assert ((Git $retrySub @('ls-remote','origin','refs/heads/main')) -match $asset) 'private upload missing after public failure'
    Assert ((Get-AhTarget $retry) -eq $asset) 'local pointer not saved'
    $script:retryPointer=$pointer
}
Check 'retry completes upload without duplicate commits or bundling other changes' {
    $result=Invoke-AhPublish $retry (Get-AhState $retry).snapshot '같은 저장 이어 올리기'
    Assert ($result.kind -eq 'dev_pending' -and -not $result.share.teamReady) 'retry was called team delivery before dev'
    Assert ((Git $retry @('rev-parse','HEAD')) -eq $script:retryPointer) 'retry created duplicate commit'
    Assert ((Git $retry @('ls-remote','origin','refs/heads/resource/retry')) -match $script:retryPointer) 'pointer did not reach remote'
}
Write-Output "Asset helper: PASS ($script:cases cases). Fixture: $fixture"
