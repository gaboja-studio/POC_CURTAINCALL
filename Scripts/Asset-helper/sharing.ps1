# A push to a work branch is not team delivery. Verify origin/dev and prepare a pointer-only PR.
. (Join-Path $PSScriptRoot 'cleanup.ps1')
function Get-AhReceiptPath([string]$ProjectPath,[string]$Branch) {
    $id=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Branch)))
    return Join-Path (Get-AhLocalDir $ProjectPath) ("publish-$id.json")
}
function Save-AhReceipt([string]$Path,$Record) {
    $temp=$Path+'.tmp'
    $Record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $temp -Encoding utf8NoBOM
    Move-Item -LiteralPath $temp -Destination $Path -Force
}
function Invoke-AhGh([string[]]$Arguments) {
    $gh=Get-GhCli
    if (-not $gh) { throw 'GitHub 연결이 필요합니다.' }
    $psi=[Diagnostics.ProcessStartInfo]::new($gh)
    $psi.UseShellExecute=$false; $psi.CreateNoWindow=$true
    $psi.RedirectStandardOutput=$true; $psi.RedirectStandardError=$true
    $psi.Environment['GH_PROMPT_DISABLED']='1'
    foreach ($arg in $Arguments) { $psi.ArgumentList.Add($arg) }
    $process=[Diagnostics.Process]::Start($psi)
    try {
        $out=$process.StandardOutput.ReadToEndAsync(); $err=$process.StandardError.ReadToEndAsync()
        $process.WaitForExit(); [void]$err.GetAwaiter().GetResult()
        if ($process.ExitCode) { throw 'GitHub 검토 요청을 만들거나 확인하지 못했습니다. 로그인·저장소 권한을 확인한 뒤 다시 눌러 주세요.' }
        return $out.GetAwaiter().GetResult().Trim()
    } finally { $process.Dispose() }
}
function New-AhPullRequest([string]$Repository,[string]$Branch,[string]$Base,[string]$Title,[string]$Body) {
    $raw=Invoke-AhGh @('pr','list','--repo',$Repository,'--head',$Branch,'--base',$Base,'--state','open','--json','url')
    $existing=@($raw | ConvertFrom-Json)
    if ($existing.Count -eq 1) { return $existing[0].url }
    if ($existing.Count -gt 1) { throw '같은 방향 검토 요청이 여러 개입니다. GitHub에서 먼저 확인해 주세요.' }
    $file=New-TemporaryFile
    try {
        Set-Content -LiteralPath $file -Value $Body -Encoding utf8NoBOM
        return Invoke-AhGh @('pr','create','--repo',$Repository,'--head',$Branch,'--base',$Base,'--title',$Title,'--body-file',"$file")
    } finally { Remove-Item -LiteralPath $file }
}
function New-AhPrivateReview([string]$Branch,[string]$Message) {
    New-AhPullRequest 'gaboja-studio/CURTAINCALL_PaidAssets' $Branch 'main' "asset: $Message" "에디터에서 확인한 에셋 변경입니다. 기존 에셋 변경과 검토 후 Merge commit 방식으로 통합해 주세요. 이 단계만으로 게임 origin/dev에 공유된 것은 아닙니다."
}
function New-AhDevReview([string]$Branch,[string]$Message) {
    New-AhPullRequest 'gaboja-studio/POC_CURTAINCALL' $Branch 'dev' "[resource] 에셋 공유: $Message" "비공개 저장소에 업로드된 에셋 연결 표시(Assets/_ThirdParty)만 변경합니다. 다른 게임 코드는 포함하지 않습니다. 지정 팀원 검토·필수 검사 후 병합해 주세요. origin/dev에서 반영을 확인할 때까지 팀 공유 대기입니다."
}
function Test-AhDevContainsAsset([string]$ProjectPath,[string]$Commit) {
    [void](Invoke-AhGit $ProjectPath @('fetch','--no-recurse-submodules','origin','+refs/heads/dev:refs/remotes/origin/dev'))
    $dev=(Invoke-AhGit $ProjectPath @('rev-parse','refs/remotes/origin/dev')).Trim()
    $row=(Invoke-AhGit $ProjectPath @('ls-tree',$dev,'--',$script:AhSubPath)).Trim()
    if ($row -notmatch '^160000 commit ([a-f0-9]{40,64})\s+') { return $false }
    $asset=$Matches[1]
    if ($asset -ceq $Commit) { return $true }
    $sub=Join-Path $ProjectPath $script:AhSubPath
    if ((Invoke-AhGit $sub @('cat-file','-e',"$asset^{commit}") -AllowFailure).Code) {
        [void](Invoke-AhGit $sub @('fetch','--no-recurse-submodules','origin',$asset))
    }
    return (Invoke-AhGit $sub @('merge-base','--is-ancestor',$Commit,$asset) -AllowFailure).Code -eq 0
}
function New-AhAssetShareBranch([string]$ProjectPath,[string]$Commit,[string]$Message,$Identity) {
    $dev=(Invoke-AhGit $ProjectPath @('rev-parse','refs/remotes/origin/dev')).Trim()
    $branch='resource/asset-share-'+$Commit.Substring(0,12)+'-'+$dev.Substring(0,12)
    $remote=(Invoke-AhGit $ProjectPath @('ls-remote','--heads','origin',"refs/heads/$branch")).Trim()
    if ($remote) {
        [void](Invoke-AhGit $ProjectPath @('fetch','--no-recurse-submodules','origin',"refs/heads/$branch"))
        $tip=(Invoke-AhGit $ProjectPath @('rev-parse','FETCH_HEAD')).Trim()
    } else {
        $local=Invoke-AhGit $ProjectPath @('rev-parse','--verify',"refs/heads/$branch") -AllowFailure
        if ($local.Code -eq 0) { $tip=$local.Output.Trim() }
        else {
            # A sparse temporary worktree checks out only root files and hooks, never Unity/paid assets.
            $temp=Join-Path (Get-AhLocalDir $ProjectPath) ('dev-pr-'+[guid]::NewGuid().ToString('N'))
            if (-not (Test-PathInside -Child $temp -Parent (Get-AhLocalDir $ProjectPath))) { throw '임시 작업 경로를 확인해 주세요.' }
            [void](Invoke-AhGit $ProjectPath @('worktree','add','--detach','--no-checkout',$temp,$dev))
            try {
                [void](Invoke-AhGit $temp @('sparse-checkout','init','--cone'))
                [void](Invoke-AhGit $temp @('sparse-checkout','set','.githooks'))
                # Populate the sparse index AND files together. read-tree without -u leaves
                # root files absent, so worktree remove sees deletions and refuses cleanup.
                [void](Invoke-AhGit $temp @('read-tree','-mu','HEAD'))
                [void](Invoke-AhGit $temp @('switch','-c',$branch))
                [void](Invoke-AhGit $temp @('update-index','--add','--cacheinfo',"160000,$Commit,$($script:AhSubPath)"))
                [void](Invoke-AhGit $temp @('update-index','--skip-worktree',$script:AhSubPath))
                $options=Get-AhCommitOptions $Identity
                $hooks=Join-Path $ProjectPath '.githooks'
                if (Test-Path -LiteralPath $hooks) { $options+=@('-c',"core.hooksPath=$hooks") }
                [void](Invoke-AhGit $temp ($options+@('commit','-m',"resource: 에셋 연결 공유 - $Message")))
                $tip=(Invoke-AhGit $temp @('rev-parse','HEAD')).Trim()
            } finally {
                # No force, reset, file deletion, or user-worktree cleanup.
                $removed=Invoke-AhGit $ProjectPath @('worktree','remove',$temp) -AllowFailure
                if ($removed.Code) { throw '임시 작업 폴더를 정리하지 못했습니다. 변경을 보존했으며, 폴더 상태를 확인한 뒤 다시 눌러 주세요.' }
            }
        }
    }
    $parent=(Invoke-AhGit $ProjectPath @('rev-parse',"$tip^1")).Trim()
    $paths=(Invoke-AhGit $ProjectPath @('diff','--name-only',"$parent..$tip")).Trim()
    $row=(Invoke-AhGit $ProjectPath @('ls-tree',$tip,'--',$script:AhSubPath)).Trim()
    if ($parent -cne $dev -or $paths -cne $script:AhSubPath -or $row -notmatch [regex]::Escape($Commit)) { throw '에셋 전용 공유 브랜치에 예상 밖 변경이 있습니다. 덮어쓰지 않고 검토를 기다립니다.' }
    if (-not $remote) { [void](Invoke-AhGit $ProjectPath @('push','origin',"${tip}:refs/heads/$branch")) }
    return $branch
}
function Complete-AhDevShare([string]$ProjectPath,$Record,[string]$Message,[string]$ReceiptPath) {
    $Record.taskUploaded=$true
    $Record.teamReady=$false
    $Record.stage='dev_pending'
    Save-AhReceipt $ReceiptPath $Record
    try {
        Write-AhProgress '공용 origin/dev까지 반영됐는지 확인하고 있습니다.'
        if (Test-AhDevContainsAsset $ProjectPath $Record.privateCommit) {
            $Record.teamReady=$true; $Record.stage='team_shared'
            $Record.cleanup=Invoke-AhShareCleanup $ProjectPath $Record
            Save-AhReceipt $ReceiptPath $Record
            return @{ kind='team_shared'; title='팀 공유 완료'; message=('비공개 에셋과 게임 origin/dev의 연결 표시를 확인했습니다. '+($Record.cleanup.messages -join ' ')); share=$Record }
        }
        if (-not $Record.teamPr) {
            Write-AhProgress '다른 게임 변경을 섞지 않는 에셋 전용 dev 검토 요청을 만들고 있습니다.'
            $dev=(Invoke-AhGit $ProjectPath @('rev-parse','refs/remotes/origin/dev')).Trim()
            $paths=(Invoke-AhGit $ProjectPath @('diff','--name-only',"$dev...$($Record.publicCommit)")).Trim()
            if ($paths -ceq $script:AhSubPath) {
                # Reuse the user's uploaded branch when its PR already contains only the pointer.
                $branch=$Record.parentBranch
            } else {
                $branch=New-AhAssetShareBranch $ProjectPath $Record.privateCommit $Message (Get-AhIdentity)
            }
            $Record.teamBranch=$branch
            if ($branch -cne $Record.parentBranch) {
                $row=(Invoke-AhGit $ProjectPath @('ls-remote','--heads','origin',"refs/heads/$branch")).Trim()
                $Record.teamCommit=($row -split '\s+')[0]
            }
            Save-AhReceipt $ReceiptPath $Record
            $Record.teamPr=New-AhDevReview $branch $Message
            Save-AhReceipt $ReceiptPath $Record
        }
        return @{ kind='dev_pending'; title='팀 공유 대기 · dev 검토 필요'; message='에셋과 현재 작업 브랜치 업로드는 끝났습니다. 에셋 전용 dev PR을 만들었습니다. 검토·병합 후 여기서 다시 확인해야 팀 공유 완료가 됩니다.'; url=$Record.teamPr; share=$Record }
    } catch {
        $Record.stage='dev_pending'
        $Record.teamReady=$false
        $Record.lastError=$_.Exception.Message
        Save-AhReceipt $ReceiptPath $Record
        return @{ kind='dev_pending'; title='팀 공유 미완료 · dev 확인 필요'; message=('에셋과 작업 브랜치는 업로드됐지만 공용 dev 반영은 확인되지 않았습니다. '+$_.Exception.Message); url=$Record.teamPr; share=$Record }
    }
}
function Get-AhShareReport([string]$ProjectPath,[string]$Branch,$State=$null) {
    $path=Get-AhReceiptPath $ProjectPath $Branch
    if (-not (Test-Path -LiteralPath $path)) {
        if (-not $State) { return $null }
        $report=@{ privateUploaded=$false; taskUploaded=$false; teamReady=$false; stage='unknown'; url=$null }
        if ($State.changes.Count) { $report.stage='local_changes'; return $report }
        if ($State.privateHead -cne $State.target) { $report.stage='version_mismatch'; return $report }
        try {
            $sub=Join-Path $ProjectPath $script:AhSubPath
            [void](Invoke-AhGit $sub @('fetch','--no-recurse-submodules','origin','+refs/heads/main:refs/remotes/origin/main'))
            $report.privateUploaded=(Invoke-AhGit $sub @('merge-base','--is-ancestor',$State.target,'refs/remotes/origin/main') -AllowFailure).Code -eq 0
            $tip=(Invoke-AhGit $ProjectPath @('ls-remote','--heads','origin',"refs/heads/$Branch")).Trim()
            $report.taskUploaded=($tip -split '\s+')[0] -ceq $State.head
            $report.teamReady=Test-AhDevContainsAsset $ProjectPath $State.target
            $report.stage=if ($report.teamReady) { 'team_shared' } else { 'dev_pending' }
        } catch { $report.stage='unknown'; $report.teamReady=$false }
        return $report
    }
    $record=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
    $report=@{ privateUploaded=[bool]$record.privateUploaded; taskUploaded=[bool]$record.taskUploaded; teamReady=$false; stage=$record.stage; url=$record.teamPr }
    if ($State -and $State.changes.Count) { $report.stage='local_changes'; $report.url=$null; return $report }
    if ($State -and $record.publicCommit -and ($State.privateHead -cne $record.privateCommit -or $State.target -cne $record.privateCommit)) { $report.stage='version_mismatch'; $report.url=$null; return $report }
    if (-not $record.publicCommit) {
        # Older helper versions did not record the upload flag. Verify the saved remote branch instead of guessing.
        if (-not $report.privateUploaded -and $record.privateBranch -match '^asset-helper/saved-[a-z0-9-]+$') {
            try {
                $sub=Join-Path $ProjectPath $script:AhSubPath
                $remote=(Invoke-AhGit $sub @('ls-remote','--heads','origin',"refs/heads/$($record.privateBranch)")).Trim()
                $report.privateUploaded=($remote -split '\s+')[0] -ceq $record.privateCommit
            } catch {}
        }
        $report.stage='private_pending'; $report.url=$record.privatePr
        return $report
    }
    try {
        $report.teamReady=Test-AhDevContainsAsset $ProjectPath $record.privateCommit
        $report.stage=if ($report.teamReady) { 'team_shared' } else { 'dev_pending' }
        if ($report.teamReady) {
            $record.teamReady=$true; $record.stage='team_shared'
            $record.cleanup=Invoke-AhShareCleanup $ProjectPath $record
            $report.cleanup=$record.cleanup
            Save-AhReceipt $path $record
        }
    } catch { $report.stage='unknown' }
    return $report
}
