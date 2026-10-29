# Shared implementation for the local asset helper. No operation executes when dot-sourced.
. (Join-Path $PSScriptRoot '../Lib/common.ps1')
$script:AhSubPath = 'Assets/_ThirdParty'
$script:AhPrivateUrl = 'https://github.com/gaboja-studio/CURTAINCALL_PaidAssets.git'

function Write-AhProgress([string]$Message) {
    if ($script:AhProgressPath) {
        @{ message=$Message; updated=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json |
            Set-Content -LiteralPath $script:AhProgressPath -Encoding utf8NoBOM
    }
}

function Invoke-AhGit {
    param([string]$Path, [string[]]$Arguments, [switch]$AllowFailure, [hashtable]$Environment=@{})
    $exe=(Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $psi=[Diagnostics.ProcessStartInfo]::new($exe)
    $psi.WorkingDirectory=$Path
    $psi.UseShellExecute=$false
    $psi.CreateNoWindow=$true
    $psi.RedirectStandardOutput=$true
    $psi.RedirectStandardError=$true
    foreach ($key in 'GIT_DIR','GIT_INDEX_FILE','GIT_WORK_TREE','GIT_PREFIX','GIT_COMMON_DIR','GIT_OBJECT_DIRECTORY','GIT_ALTERNATE_OBJECT_DIRECTORIES','GIT_AUTHOR_NAME','GIT_AUTHOR_EMAIL','GIT_COMMITTER_NAME','GIT_COMMITTER_EMAIL') { [void]$psi.Environment.Remove($key) }
    $psi.Environment['GIT_LFS_SKIP_SMUDGE']='1'
    $psi.Environment['GIT_TERMINAL_PROMPT']='0'
    $psi.Environment['GCM_INTERACTIVE']='never'
    $psi.Environment['GIT_OPTIONAL_LOCKS']='0'
    $credentialOptions=@()
    $gh=Get-GhCli
    if ($gh) {
        # Use the browser login from this helper; do not rewrite the user's global Git configuration.
        $psi.Environment['PATH']=(Split-Path $gh)+[IO.Path]::PathSeparator+$psi.Environment['PATH']
        $credentialOptions=@('-c','credential.https://github.com.helper=','-c','credential.https://github.com.helper=!gh auth git-credential')
    }
    foreach ($key in $Environment.Keys) { $psi.Environment[$key]=$Environment[$key] }
    foreach ($arg in (@('-c','submodule.recurse=false','-c','core.quotePath=false','-c','core.safecrlf=false')+$credentialOptions+@('-C',$Path)+$Arguments)) { $psi.ArgumentList.Add($arg) }
    $psi.StandardOutputEncoding=[Text.UTF8Encoding]::new($false)
    $psi.StandardErrorEncoding=[Text.UTF8Encoding]::new($false)
    $process=[Diagnostics.Process]::Start($psi)
    try {
        $stdout=$process.StandardOutput.ReadToEndAsync()
        $stderr=$process.StandardError.ReadToEndAsync()
        # Git/LFS is never killed halfway through an upload.
        $process.WaitForExit()
        $out=$stdout.GetAwaiter().GetResult()
        [void]$stderr.GetAwaiter().GetResult() # Do not expose credentials or raw remote messages.
        if ($AllowFailure) { return [pscustomobject]@{ Code=$process.ExitCode; Output=$out } }
        if ($process.ExitCode) { throw "Git 처리에 실패했습니다 ($($Arguments[0])). 연결·저장소 권한과 GitHub Desktop/Fork의 진행 작업을 확인하고 다시 시도해 주세요. 저장한 기록은 삭제하지 않습니다." }
        return $out
    } finally { $process.Dispose() }
}

function Assert-AhRemote([string]$ProjectPath) {
    $configured=(Invoke-AhGit $ProjectPath @('config','--file','.gitmodules','--get','submodule.Assets/_ThirdParty.url')).Trim()
    if ($configured -notin @($script:AhPrivateUrl,'git@github.com:gaboja-studio/CURTAINCALL_PaidAssets.git')) { throw '이 프로젝트의 비공개 에셋 저장소 연결을 확인해 주세요.' }
    $sub=Join-Path $ProjectPath $script:AhSubPath
    if (Test-Path -LiteralPath (Join-Path $sub '.git')) {
        $top=(Invoke-AhGit $sub @('rev-parse','--show-toplevel')).Trim()
        if ([IO.Path]::GetFullPath($top) -ine [IO.Path]::GetFullPath($sub)) { throw '에셋 폴더가 독립 저장소로 연결돼 있지 않습니다.' }
        $origin=(Invoke-AhGit $sub @('remote','get-url','origin')).Trim()
        if ($origin -notin @($script:AhPrivateUrl,'git@github.com:gaboja-studio/CURTAINCALL_PaidAssets.git')) { throw '에셋 폴더의 저장 대상이 비공개 에셋 저장소와 다릅니다.' }
    }
}

function Get-AhTarget([string]$ProjectPath) {
    $row=(Invoke-AhGit $ProjectPath @('ls-tree','HEAD','--',$script:AhSubPath)).Trim()
    if ($row -notmatch '^160000 commit ([a-f0-9]{40,64})\s+Assets/_ThirdParty$') { throw '현재 브랜치에 에셋 연결 기록이 없습니다.' }
    return $Matches[1]
}

function Get-AhChanges([string]$Path) {
    $raw=Invoke-AhGit $Path @('status','--porcelain=v1','-z','--untracked-files=all','--ignore-submodules=all')
    $entries=$raw.Split([char]0,[StringSplitOptions]::RemoveEmptyEntries)
    $result=[Collections.Generic.List[object]]::new()
    for ($i=0;$i -lt $entries.Count;$i++) {
        $entry=$entries[$i]
        if ($entry.Length -lt 4) { continue }
        $code=$entry.Substring(0,2)
        $name=$entry.Substring(3)
        $from=$null
        if ($code -match '[RC]') { $i++; $from=$entries[$i] }
        $full=Join-Path $Path $name
        $hash=if (Test-Path -LiteralPath $full -PathType Leaf) { (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash } else { 'MISSING' }
        $result.Add([pscustomobject]@{ path=$name; code=$code; from=$from; hash=$hash; label=$(if ($code -eq '??' -or $code -match 'A') { '추가' } elseif ($code -match 'D') { '삭제' } elseif ($code -match 'R') { '이동' } else { '수정' }) })
    }
    return $result.ToArray()
}

function Get-AhIndexHash([string]$Path) {
    $index=(Invoke-AhGit $Path @('rev-parse','--path-format=absolute','--git-path','index')).Trim()
    if (Test-Path -LiteralPath $index) { return (Get-FileHash -LiteralPath $index -Algorithm SHA256).Hash }
    return ''
}

function Get-AhState([string]$ProjectPath) {
    $project=[IO.Path]::GetFullPath($ProjectPath)
    $top=(Invoke-AhGit $project @('rev-parse','--show-toplevel')).Trim()
    if ([IO.Path]::GetFullPath($top) -ine $project) { throw '게임 프로젝트 루트에서 도우미를 열어 주세요.' }
    Assert-AhRemote $project
    $head=(Invoke-AhGit $project @('rev-parse','HEAD')).Trim()
    $branch=(Invoke-AhGit $project @('symbolic-ref','--quiet','--short','HEAD') -AllowFailure).Output.Trim()
    $target=Get-AhTarget $project
    $index=(Invoke-AhGit $project @('ls-files','--stage','--',$script:AhSubPath)).Trim()
    $sub=Join-Path $project $script:AhSubPath
    $initialized=Test-Path -LiteralPath (Join-Path $sub '.git')
    $privateHead=''; $privateBranch=''; $privateChanges=@(); $privateIndex=''
    if ($initialized) {
        $privateHead=(Invoke-AhGit $sub @('rev-parse','HEAD')).Trim()
        $privateBranch=(Invoke-AhGit $sub @('symbolic-ref','--quiet','--short','HEAD') -AllowFailure).Output.Trim()
        $privateChanges=@(Get-AhChanges $sub)
        $privateIndex=Get-AhIndexHash $sub
    }
    $other=@(Get-AhChanges $project | Where-Object path -NE $script:AhSubPath)
    $snapshot=[ordered]@{ head=$head; branch=$branch; target=$target; index=$index; parentIndex=(Get-AhIndexHash $project); privateHead=$privateHead; privateBranch=$privateBranch; privateIndex=$privateIndex; changes=$privateChanges; other=$other }
    $bytes=[Text.Encoding]::UTF8.GetBytes(($snapshot | ConvertTo-Json -Depth 12 -Compress))
    $snapshotHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
    $incomplete=$false
    if ($initialized -and -not $privateChanges.Count -and $privateHead -eq $target) {
        try { Assert-TransitionTrackedFiles $sub HEAD; Assert-TransitionLfsFiles $sub @(Get-TransitionLfsFiles $sub HEAD) } catch { $incomplete=$true }
    }
    $backups=@()
    $backupDir=Join-Path $project '.harness/local/asset-helper'
    if (Test-Path -LiteralPath $backupDir) {
        $backups=@(Get-ChildItem -LiteralPath $backupDir -Filter 'backup-*.json' -File | Sort-Object Name -Descending | ForEach-Object {
            try { $item=Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json; if (-not $item.restored) { [pscustomobject]@{ id=$_.BaseName; branch=$item.branch; date=$item.date; projectBranch=$item.projectBranch } } } catch {}
        })
    }
    $status=if (-not $initialized) { 'missing' } elseif ($privateChanges.Count) { 'edited' } elseif ($privateHead -ne $target) { 'mismatch' } elseif ($incomplete) { 'incomplete' } else { 'ready' }
    return [pscustomobject]@{
        project=$project; branch=$branch; head=$head; target=$target; index=$index; initialized=$initialized
        privateHead=$privateHead; privateBranch=$privateBranch; changes=$privateChanges; otherCount=$other.Count
        snapshot=$snapshotHash; status=$status; parentIndex=$snapshot.parentIndex; backups=$backups
    }
}

function Assert-AhSnapshot([string]$ProjectPath,[string]$Expected) {
    $state=Get-AhState $ProjectPath
    if (-not $Expected -or $state.snapshot -cne $Expected) { throw '미리보기 이후 작업 내용이나 브랜치가 바뀌었습니다. 화면을 새로 확인하고 다시 눌러 주세요.' }
    return $state
}

function Assert-AhIdle([string]$Path) {
    $dir=(Invoke-AhGit $Path @('rev-parse','--absolute-git-dir')).Trim()
    $common=(Invoke-AhGit $Path @('rev-parse','--path-format=absolute','--git-common-dir')).Trim()
    foreach ($item in @($dir,$common) | Select-Object -Unique) {
        foreach ($name in 'index.lock','HEAD.lock','packed-refs.lock','MERGE_HEAD','CHERRY_PICK_HEAD','REVERT_HEAD','rebase-merge','rebase-apply','sequencer') {
            if (Test-Path -LiteralPath (Join-Path $item $name)) { throw 'GitHub Desktop 또는 Fork에서 진행 중인 저장·병합을 먼저 마쳐 주세요.' }
        }
    }
    if ((Invoke-AhGit $Path @('ls-files','--unmerged')).Trim()) { throw '해결되지 않은 충돌이 있습니다. Fork에서 충돌 파일을 먼저 확인해 주세요.' }
    if ((Invoke-AhGit $Path @('ls-files','-v')) -cmatch '(?m)^[a-zS] ') { throw 'Git에서 숨긴 변경이 있어 안전하게 처리할 수 없습니다. 담당자에게 확인해 주세요.' }
}

function Assert-AhUnityClosed([string]$ProjectPath) {
    $file=Join-Path $ProjectPath 'Temp/UnityLockfile'
    if (-not (Test-Path -LiteralPath $file)) { return }
    try { $handle=[IO.File]::Open($file,'Open','ReadWrite','None'); $handle.Dispose() }
    catch { throw 'Unity를 닫은 뒤 다시 눌러 주세요. 에셋을 바꾸는 동안 에디터가 파일을 덮어쓰지 않도록 하기 위해서입니다.' }
}

function Get-AhIdentity {
    $identity=Get-GitHubIdentity
    if (-not $identity) { throw 'GitHub 연결이 필요합니다. 화면의 GitHub 연결 버튼으로 로그인해 주세요.' }
    return $identity
}

function Get-AhCommitOptions($Identity) {
    return @('-c',"user.name=$($Identity.Name)",'-c',"user.email=$($Identity.Email)")
}

function Assert-AhFiles([string]$Sub,$Changes) {
    if ((Invoke-AhGit $Sub @('ls-files','--stage')) -match '(?m)^160000 ') { throw '에셋 안에 또 다른 서브모듈이 있습니다. 일반 에셋 파일만 저장할 수 있습니다.' }
    foreach ($change in $Changes) {
        $name=$change.path
        if ($name -match '(^|/)(\.env(?:\..+)?|credentials\.json|token\.json|.*\.(pem|p12|pfx|key))$' -and $name -notmatch '\.env\.(example|sample)$') { throw "개인 인증 파일로 보이는 항목이 있습니다: $name. 공유 목록에서 빼고 다시 확인해 주세요." }
        if ($name -match '(^|/)\.' -or $name -match '\.meta$|\.md$') { continue }
        $full=Join-Path $Sub $name
        if (Test-Path -LiteralPath $full) {
            $parts=$name.Split('/')
            for ($i=1;$i -le $parts.Count;$i++) {
                $meta=Join-Path $Sub (($parts[0..($i-1)] -join '/')+'.meta')
                if (-not (Test-Path -LiteralPath $meta)) { throw "Unity 연결 파일이 없습니다: $name. 에디터에서 저장을 마쳐 주세요." }
            }
        } elseif (Test-Path -LiteralPath ($full+'.meta')) { throw "삭제한 에셋의 연결 파일이 남았습니다: $name. Unity에서 함께 정리해 주세요." }
    }
}

function Get-AhLocalDir([string]$ProjectPath) {
    $dir=Join-Path $ProjectPath '.harness/local/asset-helper'
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    return $dir
}

function New-AhKeepRef([string]$Sub,[string]$Head,[string]$Prefix='kept') {
    $name='asset-helper/'+$Prefix+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6)
    [void](Invoke-AhGit $Sub @('branch',$name,$Head))
    return $name
}

function Assert-AhParent($Original) {
    $current=Get-AhState $Original.project
    if ($current.head -cne $Original.head -or $current.branch -cne $Original.branch -or $current.parentIndex -cne $Original.parentIndex) { throw '처리 중 게임 브랜치나 저장 준비 목록이 바뀌었습니다. 에셋 기록은 보존했습니다. 화면을 확인하고 다시 눌러 주세요.' }
}

function Invoke-AhSync([string]$ProjectPath,[string]$Expected,[bool]$KeepChanges=$false) {
    Assert-AhUnityClosed $ProjectPath
    $state=Assert-AhSnapshot $ProjectPath $Expected
    Assert-AhIdle $ProjectPath
    if ($state.index -notmatch ('^160000 '+[regex]::Escape($state.target)+' 0\s')) { throw '게임 저장소에 다른 에셋 버전이 저장 준비돼 있습니다. GitHub Desktop/Fork에서 그 변경을 먼저 확인해 주세요.' }
    $sub=Join-Path $ProjectPath $script:AhSubPath
    $kept=$null; $stash=$null
    if (-not $state.initialized) {
        if ((Test-Path -LiteralPath $sub) -and @(Get-ChildItem -LiteralPath $sub -Force).Count) { throw '아직 연결되지 않은 에셋 폴더에 파일이 있습니다. 기존 파일을 보존한 채 연결하려면 담당자 확인이 필요합니다.' }
        Write-AhProgress '비공개 에셋을 처음 받는 중입니다. 용량에 따라 시간이 걸릴 수 있습니다.'
        [void](Invoke-AhGit $ProjectPath @('submodule','update','--init','--checkout','--',$script:AhSubPath))
    } else {
        Assert-AhIdle $sub
        if ($state.changes.Count -and -not $KeepChanges) { throw '저장하지 않은 에셋 수정이 있습니다. 내 변경을 보관하고 맞추기를 선택하거나 취소해 주세요.' }
    }
    Write-AhProgress '현재 브랜치가 사용하는 에셋과 큰 파일을 준비하고 있습니다.'
    # Prepare-TransitionRepository fetches and validates LFS before touching existing files.
    # Dirty changes are preserved before this call because untracked paths must not be overwritten.
    if ($state.initialized -and $state.changes.Count) {
        Assert-AhSnapshot $ProjectPath $Expected | Out-Null
        $identity=Get-AhIdentity
        $kept=New-AhKeepRef $sub $state.privateHead
        [void](Invoke-AhGit $sub ((Get-AhCommitOptions $identity)+@('stash','push','--include-untracked','-m',"에셋 도우미 보관 $kept")))
        $stash=(Invoke-AhGit $sub @('rev-parse','refs/stash')).Trim()
        [void](Invoke-AhGit $sub @('update-ref',"refs/asset-helper/backups/$($kept -replace '/','-')",$stash))
        @{ branch=$kept; stash=$stash; date=[DateTime]::UtcNow.ToString('o'); projectBranch=$state.branch } | ConvertTo-Json |
            Set-Content -LiteralPath (Join-Path (Get-AhLocalDir $ProjectPath) ('backup-'+[guid]::NewGuid().ToString('N')+'.json')) -Encoding utf8NoBOM
    }
    if ($state.initialized -and $state.privateHead -ne $state.target -and -not $kept) { $kept=New-AhKeepRef $sub $state.privateHead }
    $preparation=Prepare-TransitionRepository $sub $state.target
    Assert-AhParent $state
    Assert-AhUnityClosed $ProjectPath
    if (@(Get-AhChanges $sub).Count) { throw '준비 중 에셋 수정이 생겼습니다. 덮어쓰지 않고 멈췄습니다.' }
    Write-AhProgress '준비한 에셋 버전을 적용하고 있습니다.'
    [void](Invoke-AhGit $sub @('checkout','--detach','--no-overwrite-ignore',$state.target))
    Complete-TransitionRepository $preparation
    $after=Get-AhState $ProjectPath
    if ($after.head -cne $state.head -or $after.branch -cne $state.branch -or $after.privateHead -cne $after.target -or $after.changes.Count) { throw '적용 뒤 상태가 달라졌습니다. 보관 기록은 유지됩니다. 다시 상태를 확인해 주세요.' }
    return @{ kind='synced'; message='현재 브랜치에 에셋을 맞췄습니다. Unity를 열어 작업하세요.'; kept=$kept; stash=$stash }
}

function Invoke-AhRestore([string]$ProjectPath,[string]$Expected,[string]$BackupId) {
    Assert-AhUnityClosed $ProjectPath
    $state=Assert-AhSnapshot $ProjectPath $Expected
    if ($BackupId -notmatch '^backup-[a-f0-9]{32}$') { throw '보관 기록을 확인해 주세요.' }
    $file=Join-Path (Get-AhLocalDir $ProjectPath) ($BackupId+'.json')
    if (-not (Test-Path -LiteralPath $file)) { throw '보관 기록이 없습니다.' }
    $backup=Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    if ($backup.restored) { throw '이미 복구한 보관 기록입니다.' }
    $sub=Join-Path $ProjectPath $script:AhSubPath
    Assert-AhIdle $ProjectPath
    Assert-AhIdle $sub
    if ($state.changes.Count) { throw '현재 에셋 수정이 있습니다. 저장·공유하거나 보관한 뒤 복구해 주세요.' }
    $base=(Invoke-AhGit $sub @('rev-parse',"$($backup.stash)^1")).Trim()
    Write-AhProgress '보관했던 에셋 작업의 원래 버전을 준비하고 있습니다.'
    $prep=Prepare-TransitionRepository $sub $base
    Assert-AhSnapshot $ProjectPath $Expected | Out-Null
    [void](New-AhKeepRef $sub $state.privateHead 'before-restore')
    [void](Invoke-AhGit $sub @('checkout','--detach','--no-overwrite-ignore',$base))
    Complete-TransitionRepository $prep
    Write-AhProgress '보관한 내 에셋 수정을 복구하고 있습니다.'
    # Stash records LFS pointers; smudge restores their actual cached bytes, not pointer text.
    [void](Invoke-AhGit $sub @('stash','apply','--index',$backup.stash) -Environment @{ GIT_LFS_SKIP_SMUDGE='0' })
    $backup | Add-Member -NotePropertyName restored -NotePropertyValue $true -Force
    $backup | ConvertTo-Json | Set-Content -LiteralPath $file -Encoding utf8NoBOM
    return @{ kind='restored'; message='보관한 에셋 작업을 원래 버전 위에 복구했습니다. Unity에서 확인한 뒤 저장·공유해 주세요. 게임 브랜치는 바꾸지 않았습니다.'; kept=$backup.branch }
}

function Invoke-AhPrivatePublish([string]$Sub,[string]$Commit,[string]$Branch,[string]$Message) {
    Write-AhProgress '비공개 에셋 저장소에 업로드하고 있습니다.'
    $main=(Invoke-AhGit $Sub @('rev-parse','refs/remotes/origin/main')).Trim()
    if ((Invoke-AhGit $Sub @('merge-base','--is-ancestor',$Commit,$main) -AllowFailure).Code -eq 0) { return @{ ready=$true; commit=$Commit } }
    if ((Invoke-AhGit $Sub @('merge-base','--is-ancestor',$main,$Commit) -AllowFailure).Code -eq 0) {
        [void](Invoke-AhGit $Sub @('push','origin',"${Commit}:refs/heads/main"))
        [void](Invoke-AhGit $Sub @('fetch','--no-recurse-submodules','origin','+refs/heads/main:refs/remotes/origin/main'))
        if ((Invoke-AhGit $Sub @('merge-base','--is-ancestor',$Commit,'refs/remotes/origin/main') -AllowFailure).Code -ne 0) { throw '에셋 업로드 확인이 필요합니다. 로컬 기록은 보존됐습니다. 다시 눌러 주세요.' }
        return @{ ready=$true; commit=$Commit }
    }
    # Existing asset work was checked in Unity against its original version. Do not silently merge newer assets into it.
    [void](Invoke-AhGit $Sub @('push','origin',"${Commit}:refs/heads/$Branch"))
    $url='https://github.com/gaboja-studio/CURTAINCALL_PaidAssets/compare/main...'+[uri]::EscapeDataString($Branch)+'?expand=1'
    try { $url=New-AhPrivateReview $Branch $Message } catch {}
    return @{ ready=$false; commit=$Commit; url=$url; message='팀 공유 미완료입니다. 에셋은 origin의 별도 브랜치에 업로드했고, 비공개 main 통합을 기다립니다. 검토 화면에서 Merge commit 방식으로 통합한 뒤 같은 버튼을 다시 눌러 주세요. 게임 origin/dev는 아직 바뀌지 않았습니다.' }
}

function Get-AhParentPushTarget([string]$ProjectPath,[string]$Branch) {
    $remote=(Invoke-AhGit $ProjectPath @('remote','get-url','origin')).Trim()
    if ($remote -notmatch '^https://github\.com/gaboja-studio/POC_CURTAINCALL(?:\.git)?$|^git@github\.com:gaboja-studio/POC_CURTAINCALL(?:\.git)?$') { throw '게임 저장소의 업로드 대상을 확인해 주세요.' }
    return 'origin'
}

function Invoke-AhPublish([string]$ProjectPath,[string]$Expected,[string]$Message) {
    $state=Assert-AhSnapshot $ProjectPath $Expected
    if (-not $state.initialized) { throw '에셋을 먼저 받아야 합니다. 현재 브랜치에 에셋 맞추기를 눌러 주세요.' }
    if ($state.branch -notmatch '^(feat|fix|refactor|resource|integration)/') { throw 'GitHub Desktop 또는 Fork에서 작업 브랜치로 이동한 뒤 저장·공유해 주세요. dev에는 직접 저장하지 않습니다.' }
    if ($Message.Trim().Length -lt 2 -or $Message.Length -gt 160 -or $Message -match '[\r\n]|Co-Authored-By|Generated with') { throw '바뀐 내용을 2~160자의 한 줄로 적어 주세요.' }
    $remote=Get-AhParentPushTarget $ProjectPath $state.branch
    $identity=Get-AhIdentity
    $sub=Join-Path $ProjectPath $script:AhSubPath
    Assert-AhIdle $ProjectPath
    Assert-AhIdle $sub
    Assert-AhFiles $sub $state.changes
    if ($state.index -notmatch ('^160000 '+[regex]::Escape($state.target)+' 0\s')) { throw '게임 저장소에 에셋 연결 변경이 이미 저장 준비돼 있습니다. GitHub Desktop/Fork에서 확인해 주세요.' }
    Write-AhProgress '공유 대상과 변경 목록을 확인하고 있습니다.'
    [void](Invoke-AhGit $sub @('fetch','--no-recurse-submodules','origin','+refs/heads/main:refs/remotes/origin/main'))
    # Avoid accidentally uploading unrelated public commits made before the asset helper was used.
    $localDir=Get-AhLocalDir $ProjectPath
    $receiptPath=Join-Path $localDir ('publish-'+[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($state.branch)))+'.json')
    $receipt=if (Test-Path -LiteralPath $receiptPath) { Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json -AsHashtable }
    # Older helper releases deleted the receipt after pushing the work branch. Recover only a verified pointer-only save.
    if (-not $receipt -and -not $state.changes.Count -and $state.privateHead -ceq $state.target) {
        $parent=Invoke-AhGit $ProjectPath @('rev-parse',"$($state.head)^1") -AllowFailure
        if ($parent.Code -eq 0) {
            $paths=(Invoke-AhGit $ProjectPath @('diff','--name-only',"$($parent.Output.Trim())..$($state.head)")).Trim()
            $uploaded=(Invoke-AhGit $ProjectPath @('ls-remote','--heads',$remote,"refs/heads/$($state.branch)")).Trim()
            if ($paths -ceq $script:AhSubPath -and ($uploaded -split '\s+')[0] -ceq $state.head -and (Invoke-AhGit $sub @('merge-base','--is-ancestor',$state.privateHead,'refs/remotes/origin/main') -AllowFailure).Code -eq 0) {
                $receipt=@{ parentHead=$parent.Output.Trim(); parentBranch=$state.branch; privateCommit=$state.privateHead; privateBranch=$state.privateBranch; publicCommit=$state.head; privateUploaded=$true; taskUploaded=$true; teamReady=$false; stage='dev_pending' }
                Save-AhReceipt $receiptPath $receipt
            }
        }
    }
    $receiptMatches=$receipt -and $receipt.privateCommit -ceq $state.privateHead -and $(if ($receipt.publicCommit) { $state.head -ceq $receipt.publicCommit } else { $state.head -ceq $receipt.parentHead })
    if (-not $state.changes.Count -and -not $receiptMatches) {
        return @{ kind='unchanged'; message=$(if ($state.privateHead -ne $state.target) { '새로 저장할 에셋 변경은 없습니다. 브랜치 이동 뒤라면 현재 브랜치에 에셋 맞추기를 사용해 주세요.' } else { '새로 저장할 에셋 변경이 없습니다.' }) }
    }
    $resuming=$receipt -and $receipt.publicCommit -and $state.head -ceq $receipt.publicCommit -and $state.privateHead -ceq $receipt.privateCommit -and -not $state.changes.Count
    $remoteTip=(Invoke-AhGit $ProjectPath @('ls-remote','--heads',$remote,"refs/heads/$($state.branch)")).Trim()
    if ($remoteTip) {
        $remoteSha=($remoteTip -split '\s+')[0]
        if ($remoteSha -cne $state.head -and -not ($resuming -and $remoteSha -ceq $receipt.parentHead)) { throw '게임 브랜치에 아직 공유하지 않은 저장 기록 또는 새 원격 변경이 있습니다. GitHub Desktop/Fork에서 먼저 동기화해 주세요. 에셋은 아직 저장하지 않았습니다.' }
    }
    elseif (-not $resuming) {
        [void](Invoke-AhGit $ProjectPath @('fetch','--no-recurse-submodules',$remote))
        $unpublished=(Invoke-AhGit $ProjectPath @('rev-list','--count','HEAD','--not','--remotes=origin')).Trim()
        if ($unpublished -ne '0') { throw '현재 브랜치에 아직 공유하지 않은 게임 저장 기록이 있습니다. GitHub Desktop/Fork에서 브랜치를 먼저 게시해 주세요. 도우미는 다른 게임 기록을 함께 올리지 않습니다.' }
    }
    # Resume a private upload or a public-pointer push without creating duplicate commits.
    if ($resuming) {
        Write-AhProgress '이전에 저장한 게임 연결 표시의 업로드를 이어갑니다.'
        [void](Invoke-AhGit $ProjectPath @('push',$remote,"HEAD:refs/heads/$($state.branch)"))
        return Complete-AhDevShare $ProjectPath $receipt $Message $receiptPath
    }
    Assert-AhSnapshot $ProjectPath $Expected | Out-Null
    $privateBranch=$null
    if ($state.changes.Count) {
        Write-AhProgress '확인한 에셋 변경을 로컬에 저장하고 있습니다.'
        $privateBranch=New-AhKeepRef $sub $state.privateHead 'saved'
        [void](Invoke-AhGit $sub @('switch',$privateBranch))
        [void](Invoke-AhGit $sub @('add','--all'))
        $now=@(Get-AhChanges $sub)
        if (($now | Select-Object path,hash | ConvertTo-Json -Compress) -cne ($state.changes | Select-Object path,hash | ConvertTo-Json -Compress)) { throw '저장 중 에셋 파일이 바뀌었습니다. 저장 준비 목록을 보존했습니다. Unity 저장이 끝났는지 확인해 주세요.' }
        [void](Invoke-AhGit $sub ((Get-AhCommitOptions $identity)+@('commit','-m',"asset: $Message")))
    } else {
        if ($state.privateHead -ceq $state.target -and -not $receipt) { return @{ kind='unchanged'; message='새로 저장할 에셋 변경이 없습니다.' } }
        $privateBranch=if ($receipt -and $receipt.privateCommit -ceq $state.privateHead) { $receipt.privateBranch } else { New-AhKeepRef $sub $state.privateHead 'saved' }
    }
    $commit=(Invoke-AhGit $sub @('rev-parse','HEAD')).Trim()
    $record=@{ parentHead=$state.head; parentBranch=$state.branch; privateCommit=$commit; privateBranch=$privateBranch; publicCommit=$null; privateUploaded=$false; taskUploaded=$false; teamReady=$false; stage='local_saved' }
    $record | ConvertTo-Json | Set-Content -LiteralPath $receiptPath -Encoding utf8NoBOM
    $published=Invoke-AhPrivatePublish $sub $commit $privateBranch $Message
    $record.privateUploaded=$true
    if (-not $published.ready) {
        $record.stage='private_pending'; $record.privatePr=$published.url
        Save-AhReceipt $receiptPath $record
        return @{ kind='private_pending'; title='팀 공유 미완료 · 에셋 통합 대기'; message=$published.message; url=$published.url; kept=$privateBranch; share=$record }
    }
    $record.stage='private_ready'
    Save-AhReceipt $receiptPath $record
    Assert-AhParent $state
    if (@(Get-AhChanges $sub).Count -or (Invoke-AhGit $sub @('rev-parse','HEAD')).Trim() -cne $commit) { throw '업로드 중 에셋 수정이 생겼습니다. 에셋 저장 기록은 보존했고 게임 연결 표시는 바꾸지 않았습니다.' }
    # Use a separate index so unrelated staged game changes remain staged and untouched.
    Write-AhProgress '게임 저장소에 에셋 연결 표시만 저장하고 있습니다.'
    $index=Join-Path $localDir ('index-'+[guid]::NewGuid().ToString('N'))
    $environment=@{ GIT_INDEX_FILE=$index }
    try {
        [void](Invoke-AhGit $ProjectPath @('read-tree','HEAD') -Environment $environment)
        [void](Invoke-AhGit $ProjectPath @('update-index','--add','--cacheinfo',"160000,$commit,$($script:AhSubPath)") -Environment $environment)
        [void](Invoke-AhGit $ProjectPath ((Get-AhCommitOptions $identity)+@('commit','-m',"resource: $Message")) -Environment $environment)
        $record.publicCommit=(Invoke-AhGit $ProjectPath @('rev-parse','HEAD')).Trim()
        $record | ConvertTo-Json | Set-Content -LiteralPath $receiptPath -Encoding utf8NoBOM
        [void](Invoke-AhGit $ProjectPath @('update-index','--cacheinfo',"160000,$commit,$($script:AhSubPath)"))
    } finally { if (Test-Path -LiteralPath $index) { Remove-Item -LiteralPath $index } }
    Write-AhProgress '게임 연결 표시를 현재 작업 브랜치에 업로드하고 있습니다.'
    [void](Invoke-AhGit $ProjectPath @('push',$remote,"HEAD:refs/heads/$($state.branch)"))
    return Complete-AhDevShare $ProjectPath $record $Message $receiptPath
}

. (Join-Path $PSScriptRoot 'sharing.ps1')
