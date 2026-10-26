# Delete only receipt-owned, fully delivered helper branches. Backups and user task branches stay.
function Get-AhWorktrees([string]$Path) {
    $raw=Invoke-AhGit $Path @('worktree','list','--porcelain','-z')
    $item=@{}
    foreach ($line in $raw.Split([char]0)) {
        if (-not $line) { if ($item.path) { $item }; $item=@{}; continue }
        if ($line.StartsWith('worktree ')) { $item.path=$line.Substring(9) }
        elseif ($line.StartsWith('branch ')) { $item.branch=$line.Substring(7) }
        elseif ($line.StartsWith('HEAD ')) { $item.head=$line.Substring(5) }
        elseif ($line.StartsWith('locked')) { $item.locked=$true }
    }
    if ($item.path) { $item }
}
function Remove-AhDeliveredBranch([string]$Path,[string]$Branch,[string]$Expected,[string]$Destination,[switch]$Private,[string]$DeliveredAsset='') {
    if ($Expected -notmatch '^[a-f0-9]{40,64}$') { throw '정리할 브랜치의 저장 기록을 확인하지 못했습니다.' }
    if ((Invoke-AhGit $Path @('merge-base','--is-ancestor',$Expected,$Destination) -AllowFailure).Code) {
        # A public Squash/Rebase merge has different commit IDs. Verify its sole pointer change.
        $delivered=$false
        if (-not $Private -and $DeliveredAsset -match '^[a-f0-9]{40,64}$') {
            $paths=(Invoke-AhGit $Path @('diff','--name-only',"$Expected^1..$Expected")).Trim()
            $asset=(Invoke-AhGit $Path @('ls-tree',$Expected,'--',$script:AhSubPath)).Trim()
            if ($paths -ceq $script:AhSubPath -and $asset -match [regex]::Escape($DeliveredAsset)) {
                $delivered=Test-AhDevContainsAsset $Path $DeliveredAsset
            }
        }
        if (-not $delivered) { throw '통합되지 않은 작업을 보존했습니다.' }
    }
    $ref="refs/heads/$Branch"
    $local=Invoke-AhGit $Path @('rev-parse','--verify',$ref) -AllowFailure
    if (-not $local.Code -and $local.Output.Trim() -cne $Expected) { throw '추가 작업이 있는 브랜치를 보존했습니다.' }
    $remote=(Invoke-AhGit $Path @('ls-remote','--heads','origin',$ref)).Trim()
    if ($remote -and ($remote -split '\s+')[0] -cne $Expected) { throw '원격 브랜치에 추가 작업이 있어 보존했습니다.' }
    foreach ($tree in @(Get-AhWorktrees $Path | Where-Object { $_.branch -ceq $ref })) {
        if ($tree.locked) { throw '잠긴 작업 폴더를 보존했습니다.' }
        $same=[IO.Path]::GetFullPath($tree.path) -ieq [IO.Path]::GetFullPath($Path)
        if ($Private -and -not $same) {
            # Git reports a submodule's primary worktree using its modules/... git directory.
            $common=(Invoke-AhGit $Path @('rev-parse','--path-format=absolute','--git-common-dir')).Trim()
            $current=(Invoke-AhGit $Path @('symbolic-ref','--quiet','HEAD') -AllowFailure).Output.Trim()
            $same=([IO.Path]::GetFullPath($tree.path) -ieq [IO.Path]::GetFullPath($common)) -and $current -ceq $ref
        }
        if ($Private -and $same) {
            Assert-AhIdle $Path
            if ((Invoke-AhGit $Path @('status','--porcelain=v1','--untracked-files=all')).Trim()) { throw '수정 중인 에셋 브랜치를 보존했습니다.' }
            # The very same asset commit stays checked out; only the disposable branch name goes away.
            [void](Invoke-AhGit $Path @('checkout','--detach',$Expected))
        } else {
            $root=Get-AhLocalDir $Path
            $full=[IO.Path]::GetFullPath($tree.path)
            if ($Private -or -not (Test-PathInside -Child $full -Parent $root) -or (Split-Path $full -Leaf) -notmatch '^dev-pr-[a-f0-9]{32}$') { throw '사용 중인 작업 폴더를 보존했습니다.' }
            if ((Invoke-AhGit $full @('status','--porcelain=v1','--untracked-files=all','--ignored','--ignore-submodules=none')).Trim()) { throw '변경이 있는 임시 작업 폴더를 보존했습니다.' }
            [void](Invoke-AhGit $Path @('worktree','remove',$full))
        }
    }
    # Normal delete, never force-push. Recheck the remote immediately before deletion.
    if ($remote) {
        $now=(Invoke-AhGit $Path @('ls-remote','--heads','origin',$ref)).Trim()
        if ($now -and ($now -split '\s+')[0] -cne $Expected) { throw '원격 브랜치가 바뀌어 정리를 미뤘습니다.' }
        if ($now) { [void](Invoke-AhGit $Path @('push','origin','--delete',$Branch)) }
    }
    if (-not $local.Code) { [void](Invoke-AhGit $Path @('update-ref','-d',$ref,$Expected)) }
    $tracking="refs/remotes/origin/$Branch"
    $tracked=Invoke-AhGit $Path @('rev-parse','--verify',$tracking) -AllowFailure
    if (-not $tracked.Code -and $tracked.Output.Trim() -ceq $Expected) { [void](Invoke-AhGit $Path @('update-ref','-d',$tracking,$Expected)) }
}
function Invoke-AhShareCleanup([string]$ProjectPath,$Record) {
    $result=@{ done=$true; messages=@() }
    if (-not $Record.teamReady) { return @{ done=$false; messages=@('통합 확인 후 임시 항목을 정리합니다.') } }
    $sub=Join-Path $ProjectPath $script:AhSubPath
    # Each item can finish independently; failure is visible and retried on the next refresh.
    if ($Record.teamBranch -cne $Record.parentBranch -and $Record.teamBranch -match '^resource/asset-share-([a-f0-9]{12})-([a-f0-9]{12})$') {
        $assetPrefix=$Matches[1]; $basePrefix=$Matches[2]
        try {
            if (-not $Record.privateCommit.StartsWith($assetPrefix)) { throw '에셋 공유 브랜치 이름을 확인해 주세요.' }
            $tip=$Record.teamCommit
            if (-not $tip) {
                $found=Invoke-AhGit $ProjectPath @('rev-parse','--verify',"refs/heads/$($Record.teamBranch)") -AllowFailure
                if (-not $found.Code) { $tip=$found.Output.Trim() }
                else {
                    $row=(Invoke-AhGit $ProjectPath @('ls-remote','--heads','origin',"refs/heads/$($Record.teamBranch)")).Trim()
                    if ($row) { $tip=($row -split '\s+')[0]; [void](Invoke-AhGit $ProjectPath @('fetch','--no-recurse-submodules','origin',$tip)) }
                }
            }
            if ($tip) {
                $parent=(Invoke-AhGit $ProjectPath @('rev-parse',"$tip^1")).Trim()
                $paths=(Invoke-AhGit $ProjectPath @('diff','--name-only',"$parent..$tip")).Trim()
                $asset=(Invoke-AhGit $ProjectPath @('ls-tree',$tip,'--',$script:AhSubPath)).Trim()
                if (-not $parent.StartsWith($basePrefix) -or $paths -cne $script:AhSubPath -or $asset -notmatch [regex]::Escape($Record.privateCommit)) { throw '예상과 다른 공유 브랜치를 보존했습니다.' }
                $Record.teamCommit=$tip
                Remove-AhDeliveredBranch $ProjectPath $Record.teamBranch $tip 'refs/remotes/origin/dev' -DeliveredAsset $Record.privateCommit
            }
        } catch { $result.done=$false; $result.messages+=('게임 임시 항목: '+$_.Exception.Message) }
    }
    if ($Record.privateBranch -match '^asset-helper/saved-\d{8}-\d{6}-[a-f0-9]{6}$') {
        try {
            [void](Invoke-AhGit $sub @('fetch','--no-recurse-submodules','origin','+refs/heads/main:refs/remotes/origin/main'))
            Remove-AhDeliveredBranch $sub $Record.privateBranch $Record.privateCommit 'refs/remotes/origin/main' -Private
        } catch { $result.done=$false; $result.messages+=('에셋 임시 항목: '+$_.Exception.Message) }
    }
    if ($result.done) { $result.messages=@('완료된 도우미 임시 브랜치·작업 폴더 정리를 마쳤습니다. 작업 브랜치와 복구 기록은 보존합니다.') }
    return $result
}
function Update-AhOtherShareCleanup([string]$ProjectPath,[string]$CurrentBranch) {
    $current=Get-AhReceiptPath $ProjectPath $CurrentBranch
    $results=@()
    foreach ($file in @(Get-ChildItem -LiteralPath (Get-AhLocalDir $ProjectPath) -Filter 'publish-*.json' -File)) {
        if ($file.FullName -ieq $current) { continue }
        try {
            $record=Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json -AsHashtable
            if (-not $record.publicCommit -or $record.cleanup.done) { continue }
            if (-not (Test-AhDevContainsAsset $ProjectPath $record.privateCommit)) { continue }
            $record.teamReady=$true; $record.stage='team_shared'
            $record.cleanup=Invoke-AhShareCleanup $ProjectPath $record
            Save-AhReceipt $file.FullName $record
            $results+=@($record.cleanup)
        } catch { $results+=@(@{ done=$false; messages=@('이전 공유의 임시 항목을 확인하지 못했습니다. 다음 확인 때 재시도합니다.') }) }
    }
    if (-not $results.Count) { return $null }
    return @{ done=(@($results | Where-Object { -not $_.done }).Count -eq 0); messages=@($results | ForEach-Object { $_.messages }) }
}
