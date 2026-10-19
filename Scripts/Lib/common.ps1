# Shared helpers for harness scripts. Dot-source: . (Join-Path $PSScriptRoot 'Lib/common.ps1')

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$script:TaskIdPattern = '^Task-(\d{8})-(\d{3})'
$script:TaskBranchPattern = '^(feat|fix|refactor)/[a-z0-9]+(?:-[a-z0-9]+)*$'

function Get-RepoPath {
    param([Parameter(Mandatory)][string]$Relative)
    return Join-Path $script:RepoRoot $Relative
}

function Get-EnvValue {
    param([Parameter(Mandatory)][string]$Key)
    $envPath = Get-RepoPath '.env'
    if (-not (Test-Path -LiteralPath $envPath)) { return $null }
    $line = Get-Content -LiteralPath $envPath | Where-Object { $_ -match "^$Key=" } | Select-Object -First 1
    if (-not $line) { return $null }
    return $line.Substring($Key.Length + 1).Trim()
}

function Get-Worktrees {
    # Returns [pscustomobject]@{ Path; Branch } for every registered worktree. First entry is the main checkout.
    $raw = & git -C $script:RepoRoot worktree list --porcelain
    $items = [System.Collections.Generic.List[object]]::new()
    $current = $null
    foreach ($line in $raw) {
        if ($line -like 'worktree *') {
            if ($current) { $items.Add([pscustomobject]$current) }
            $current = @{ Path = $line.Substring(9); Branch = '' }
        } elseif ($line -like 'branch refs/heads/*' -and $current) {
            $current.Branch = $line.Substring(18)
        }
    }
    if ($current) { $items.Add([pscustomobject]$current) }
    return $items
}

function Get-MainCheckout {
    return (Get-Worktrees | Select-Object -First 1).Path
}

function Get-WorktreeRoot {
    $configured = Get-EnvValue 'WORKTREE_ROOT'
    if ($configured) { return [System.IO.Path]::GetFullPath($configured.Replace('~', $HOME)) }
    $main = Get-MainCheckout
    return [System.IO.Path]::GetFullPath((Join-Path (Split-Path $main -Parent) 'github-worktrees' (Split-Path $main -Leaf)))
}

function Get-BaseBranch {
    # Single source: Harness/Project/Facts/repository.md
    $facts = Get-RepoPath 'Harness/Project/Facts/repository.md'
    if (Test-Path -LiteralPath $facts) {
        $m = Select-String -LiteralPath $facts -Pattern 'Base\) 브랜치: `([^`]+)`' | Select-Object -First 1
        if ($m) { return $m.Matches[0].Groups[1].Value }
    }
    return 'dev'
}

function Get-AllTaskFolders {
    # Task folders across every worktree: [pscustomobject]@{ Id; Number; State; Path; Worktree }
    $result = [System.Collections.Generic.List[object]]::new()
    foreach ($wt in Get-Worktrees) {
        foreach ($state in 'Active', 'Done') {
            $dir = Join-Path $wt.Path 'Tasks' $state
            if (-not (Test-Path -LiteralPath $dir)) { continue }
            foreach ($folder in Get-ChildItem -LiteralPath $dir -Directory) {
                if ($folder.Name -match $script:TaskIdPattern) {
                    $result.Add([pscustomobject]@{
                        Id = "Task-$($Matches[1])-$($Matches[2])"
                        Number = [int]$Matches[2]
                        State = $state
                        Path = $folder.FullName
                        Worktree = $wt.Path
                    })
                }
            }
        }
    }
    return $result
}

function Get-TaskRefMetadata {
    # checkout 없이 로컬·원격 ref의 Task 메타데이터를 읽는다.
    param([string]$Id, [switch]$IncludeDone, [string]$Ref)
    $refs = if ($Ref) { @($Ref) } else { @(& git -C $script:RepoRoot for-each-ref --format='%(refname)' refs/heads refs/remotes) }
    if (-not $Ref -and $LASTEXITCODE -ne 0) { throw 'Task 조회용 Git ref 목록을 읽지 못했습니다.' }
    foreach ($ref in $refs) {
        $states = @('Tasks/Active/')
        if ($IncludeDone) { $states += 'Tasks/Done/' }
        $folders = @(& git -C $script:RepoRoot ls-tree -d --name-only $ref -- @states)
        if ($LASTEXITCODE -ne 0) { throw "Task 폴더 조회 실패: $ref" }
        foreach ($folder in $folders) {
            if ((Split-Path $folder -Leaf) -notmatch $script:TaskIdPattern) { continue }
            $taskId = "Task-$($Matches[1])-$($Matches[2])"
            $number = [int]$Matches[2]
            if ($Id -and $Id -ne $taskId) { continue }
            $text = @(& git -C $script:RepoRoot show "${ref}:$folder/meta.md")
            if ($LASTEXITCODE -ne 0) { throw "Task 메타데이터 조회 실패: $ref / $folder" }
            $line = $text | Where-Object { $_ -match '^\s*-\s*\*\*Branch:\*\*\s*(.*)$' } | Select-Object -First 1
            $branch = if ($line) { ($line -replace '^\s*-\s*\*\*Branch:\*\*\s*', '' -replace '\s*\(.*\)$', '').Trim() } else { '' }
            [pscustomobject]@{ Id = $taskId; Number = $number; Branch = $branch; Ref = $ref; Rel = $folder }
        }
    }
}

function Get-MetaField {
    param([string]$MetaPath, [Parameter(Mandatory)][string]$Field, [string]$Text)
    if ($MetaPath) {
        if (-not (Test-Path -LiteralPath $MetaPath)) { return $null }
        $Text = Get-Content -LiteralPath $MetaPath -Raw
    }
    $m = [regex]::Match("$Text", "(?m)^\s*-\s*\*\*$([regex]::Escape($Field)):\*\*[^\S\r\n]*(.*)$")
    if (-not $m.Success) { return $null }
    return $m.Groups[1].Value.Trim()
}

# ---------- 부모 통합·준비 브랜치 (checkout 없이 원격 문서 확인) ----------

function Get-RefFile {
    param([Parameter(Mandatory)][string]$Ref, [Parameter(Mandatory)][string]$Path)
    $lines = @(& git -C $script:RepoRoot show "${Ref}:$Path" 2>$null)
    if ($LASTEXITCODE -ne 0) { throw "Git 문서를 읽지 못했습니다: $Ref / $Path" }
    return $lines -join "`n"
}

function Get-IntegrationMetadata {
    param([Parameter(Mandatory)][string]$Branch, [string]$Ref = "refs/remotes/origin/$Branch", [switch]$AllowDone)
    if ($Branch -cnotmatch '^integration/([a-z0-9]+(?:-[a-z0-9]+)*)$') { throw "통합 브랜치가 아닙니다: $Branch" }
    $slug = $Matches[1]
    $path = "Integrations/Active/Integration-$slug/meta.md"
    $closed = $false
    try { $text = Get-RefFile -Ref $Ref -Path $path } catch {
        # close-integration이 Done으로 옮긴 뒤 Base로 올리는 PR만 마감 메타를 읽는다.
        if (-not $AllowDone) { throw }
        $path = "Integrations/Done/Integration-$slug/meta.md"
        $text = Get-RefFile -Ref $Ref -Path $path
        $closed = $true
    }
    if ((Get-MetaField -Text $text -Field 'Branch') -cne $Branch) { throw "통합 메타 Branch 불일치: $Branch" }
    return [pscustomobject]@{ Branch = $Branch; Base = (Get-MetaField -Text $text -Field 'Base'); ActiveTask = (Get-MetaField -Text $text -Field 'Active Task'); Text = $text; Path = $path; Ref = $Ref; Closed = $closed }
}

function Assert-IntegrationLineage {
    param([Parameter(Mandatory)][string]$Branch, [string]$Ref, [string]$Child, [switch]$AllowDone,
        [System.Collections.IDictionary]$PinnedRefs, [switch]$RequireActive)
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    if ($Child) { [void]$seen.Add($Child) }
    $first = $true
    while ($Branch -cne (Get-BaseBranch)) {
        if (-not $seen.Add($Branch)) { throw "통합 Base 자기참조/순환: $Branch" }
        $readRef = if ($null -ne $PinnedRefs) {
            if (-not $PinnedRefs.Contains($Branch)) { throw "고정된 계보 SHA 누락: $Branch" }
            $PinnedRefs[$Branch]
        } elseif ($first -and $Ref) { $Ref } else { "refs/remotes/origin/$Branch" }
        $info = Get-IntegrationMetadata -Branch $Branch -Ref $readRef -AllowDone:($first -and $AllowDone)
        $first = $false
        if ($RequireActive -and (Get-MetaField -Text $info.Text -Field 'Status') -cnotin @('preparing', 'working', 'merging', 'testing', 'to-dev', 'blocked')) {
            throw "활성 통합 상태가 아닙니다: $Branch"
        }
        if (-not $info.Base) { throw "통합 Base 누락: $Branch" }
        $Branch = $info.Base
    }
    $baseRef = if ($null -ne $PinnedRefs) {
        if (-not $PinnedRefs.Contains($Branch)) { throw "고정된 계보 SHA 누락: $Branch" }
        $PinnedRefs[$Branch]
    } else { "refs/remotes/origin/$Branch" }
    & git -C $script:RepoRoot rev-parse --verify --quiet "$baseRef^{commit}" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "원격 부모 브랜치가 없습니다: $Branch" }
}

function Get-RefTask {
    param([Parameter(Mandatory)][string]$Ref, [string]$Id, [string]$Branch)
    $items = @(Get-TaskRefMetadata -Id $Id -Ref $Ref | Where-Object { -not $Branch -or $_.Branch -ceq $Branch })
    if ($items.Count -ne 1) { throw "Task 문서가 없거나 여러 개입니다: $Ref / $Id $Branch" }
    $item = $items[0]
    $text = Get-RefFile -Ref $Ref -Path "$($item.Rel)/meta.md"
    return [pscustomobject]@{ Id = $item.Id; Rel = $item.Rel; Ref = $Ref; Text = $text; Branch = $item.Branch; Type = (Get-MetaField -Text $text -Field 'Type'); Integration = (Get-MetaField -Text $text -Field 'Integration'); Status = (Get-MetaField -Text $text -Field 'Status') }
}

function Assert-PreparedBranch {
    param([Parameter(Mandatory)][string]$Integration, [Parameter(Mandatory)][string]$Branch, [Parameter(Mandatory)][string]$Id, [string]$Type,
        [string]$IntegrationRef = "refs/remotes/origin/$Integration", [string]$BranchRef = "refs/remotes/origin/$Branch", [string]$MetadataText)
    $info = Get-IntegrationMetadata -Branch $Integration -Ref $IntegrationRef
    if (-not $MetadataText) { $MetadataText = $info.Text }
    $section = Get-MarkdownSection -Text $MetadataText -Heading '준비 브랜치'
    $rows = @($section -split '\r?\n' | Where-Object { $_ -match "^\|\s*$([regex]::Escape($Id))\s*\|" })
    if ($rows.Count -ne 1) { throw "준비 브랜치 표에 Task가 없거나 중복입니다: $Id" }
    $cols = @($rows[0].Trim('|').Split('|') | ForEach-Object { $_.Trim().Trim('`') })
    if ($cols.Count -ne 4 -or $cols[1] -cne $Branch -or $cols[2] -notmatch '^[a-f0-9]{40}$' -or $cols[3] -notmatch '^[a-f0-9]{40}$') { throw "준비 브랜치/SHA 표 불일치: $Id" }
    $basis, $approval = $cols[2], $cols[3]
    foreach ($pair in @(@($basis, $approval), @($approval, $IntegrationRef), @($basis, $BranchRef))) {
        & git -C $script:RepoRoot merge-base --is-ancestor $pair[0] $pair[1] 2>$null
        if ($LASTEXITCODE -ne 0) { throw "승인된 부모 준비 이력이 아닙니다: $($pair -join ' → ')" }
    }
    foreach ($ref in $basis, $approval, $BranchRef) {
        $task = Get-RefTask -Ref $ref -Id $Id
        if ($task.Branch -cne $Branch -or $task.Integration -cne $Integration -or $task.Type -cne ($Branch -split '/')[0] -or ($Type -and $task.Type -cne $Type)) { throw "준비 branch Task/Type/Integration 불일치: $ref / $Id" }
    }
    # 부모에서 내려온 커밋은 허용. 부모와 동일 tree인 2-parent sync merge 외 고유 커밋은 구현 이력으로 거부한다.
    $unique = @(& git -C $script:RepoRoot rev-list $BranchRef --not $IntegrationRef)
    if ($LASTEXITCODE -ne 0) { throw '준비 브랜치 이력 조회 실패' }
    foreach ($commit in $unique) {
        $parents = ((& git -C $script:RepoRoot rev-list --parents -n 1 $commit) -split ' ')
        if ($parents.Count -ne 3) { throw "준비 브랜치에 독립 구현 커밋이 있습니다: $commit" }
        & git -C $script:RepoRoot merge-base --is-ancestor $parents[2] $IntegrationRef
        if ($LASTEXITCODE -ne 0) { throw "준비 sync의 부모가 소속 통합이 아닙니다: $commit" }
        & git -C $script:RepoRoot diff --quiet $commit $parents[2]
        if ($LASTEXITCODE -ne 0) { throw "준비 sync에 독립 변경이 있습니다: $commit" }
    }
}

function Get-PrFlow {
    param([Parameter(Mandatory)][string]$Head, [Parameter(Mandatory)][string]$Base)
    if ($Head -ceq $Base) { throw "같은 브랜치로 PR 불가: $Head" }
    if ($Head -like 'integration/*') {
        Assert-IntegrationLineage -Branch $Head -Ref HEAD -AllowDone
        $headInfo = Get-IntegrationMetadata -Branch $Head -Ref HEAD -AllowDone
        if ($Base -ceq $headInfo.Base) { return 'merge' }
        if ($headInfo.Closed) { throw "마감된 통합은 Base($($headInfo.Base))로만 PR 가능: $Head → $Base" }
        if ($Base -like 'integration/*') {
            Assert-IntegrationLineage -Branch $Base
            if ((Get-IntegrationMetadata -Branch $Base).Base -ceq $Head) { return 'merge' }
        } elseif ($Base -cmatch $script:TaskBranchPattern) {
            $task = Get-RefTask -Ref "refs/remotes/origin/$Base" -Branch $Base
            Assert-PreparedBranch -Integration $Head -IntegrationRef HEAD -Branch $Base -Id $task.Id -Type $task.Type
            return 'merge'
        }
        throw "선언된 통합 부모/자식 또는 소속 준비 Task sync만 허용: $Head → $Base"
    }
    if ($Head -ceq (Get-BaseBranch)) {
        if ($Base -like 'builds/*') { return 'merge' }
        if ($Base -like 'integration/*') {
            Assert-IntegrationLineage -Branch $Base
            if ((Get-IntegrationMetadata -Branch $Base).Base -ceq $Head) { return 'merge' }
        }
        throw "dev sync는 Base가 dev인 통합에만 허용: $Base"
    }
    if (($Head -cmatch '^(feat|refactor)/' -and $Base -like 'integration/*') -or
        ($Head -cmatch '^(fix|resource)/' -and ($Base -ceq (Get-BaseBranch) -or $Base -like 'integration/*'))) { return 'squash' }
    # PM이 단일 Task로 승인해 meta Integration을 dev로 선언한 feat/refactor만 dev로 바로 간다.
    if ($Head -cmatch '^(feat|refactor)/' -and $Base -ceq (Get-BaseBranch)) {
        & git -C $script:RepoRoot show-ref --verify --quiet "refs/heads/$Head"
        $declared = if ($LASTEXITCODE -eq 0) { @(Get-TaskRefMetadata -Ref "refs/heads/$Head" | Where-Object { $_.Branch -ceq $Head }) } else { @() }
        if ($declared.Count -eq 1 -and (Get-RefTask -Ref "refs/heads/$Head" -Branch $Head).Integration -ceq $Base) { return 'squash' }
        throw "feat/refactor는 integration/*로만 제출합니다. dev 직행은 Task meta Integration이 $Base 로 선언된 경우만 허용: $Head"
    }
    throw "대상 브랜치 규칙 위반: $Head → $Base"
}

function Assert-TaskAssignmentGate {
    param([Parameter(Mandatory)][string]$Id, [Parameter(Mandatory)][string]$TaskPath, [Parameter(Mandatory)][string]$IntegrationText)
    $active = Get-MetaField -Text $IntegrationText -Field 'Active Task'
    if (-not $active) { return }
    if ($active -cne $Id) { throw "Active Task 불일치: $active / $Id" }
    $meta = Join-Path $TaskPath 'meta.md'
    $setup = Join-Path $TaskPath 'setup.md'
    $jiraSection = Get-MarkdownSection -Path $setup -Heading 'JIRA'
    $jira = (($jiraSection -split '\r?\n') | Where-Object { $_ -match '^\s*- 관련 JIRA:' }) -join ' '
    if ($jira -notmatch 'https://(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}/browse/CC-[1-9]\d*\b' -or $jira -match '없음|미정') { throw '순차 Task에는 실제 https://.../browse/CC-번호 JIRA URL이 필요합니다.' }
    $previous = Get-MetaField -MetaPath $meta -Field 'Previous Task'
    if ($previous) {
        if ($previous -notmatch '^Task-\d{8}-\d{3}$' -or $previous -ceq $Id) { throw "Previous Task 값 오류: $previous" }
        $dirs = @(Get-ChildItem -LiteralPath (Get-RepoPath 'Tasks/Active') -Directory -Filter "$previous-*" -ErrorAction SilentlyContinue)
        if ($dirs.Count -ne 1 -or (Get-MetaField -MetaPath (Join-Path $dirs[0].FullName 'meta.md') -Field 'Status') -cne 'integrated') { throw "앞 단계가 integrated가 아닙니다: $previous" }
        $handoff = Join-Path $dirs[0].FullName 'handoff.md'
        $verification = Get-MarkdownSection -Path $handoff -Heading 'Verification'
        $evidence = Get-MarkdownSection -Path $handoff -Heading '공동 테스트 결과'
        if ($verification -notmatch '(?m)^- 5 공동 테스트:[^\S\r\n]*PASS[^\S\r\n]*—[^\S\r\n]*\S+' -or $evidence -match '미실행|미정|예정|미확인') { throw '앞 단계 handoff 실제 공동검증 근거가 필요합니다.' }
        foreach ($label in '일시', '참여자', '재현', '예상', '실제') {
            if ($evidence -notmatch "(?m)^- ${label}:[^\S\r\n]*\S+") { throw "공동 테스트 결과의 $label 근거가 없습니다." }
        }
    }
    $allowed = (Get-SetupAreas -SetupPath $setup).Allowed
    foreach ($other in Get-ChildItem -LiteralPath (Get-RepoPath 'Tasks/Active') -Directory) {
        if ($other.FullName -eq $TaskPath) { continue }
        $otherMeta = Join-Path $other.FullName 'meta.md'
        if ((Get-MetaField -MetaPath $otherMeta -Field 'Integration') -ceq (Get-MetaField -MetaPath $meta -Field 'Integration') -and (Get-MetaField -MetaPath $otherMeta -Field 'Status') -in 'assigned', 'working', 'submitted', 'blocked') { throw "다른 활성 단계가 있습니다: $($other.Name)" }
        $otherAreas = Get-SetupAreas -SetupPath (Join-Path $other.FullName 'setup.md')
        foreach ($a in $allowed) {
            foreach ($b in $otherAreas.Allowed) {
                if ((Test-PathInArea -Path $a.TrimEnd('/') -Areas @($b)) -or (Test-PathInArea -Path $b.TrimEnd('/') -Areas @($a))) { throw "Allowed 소유권 충돌: $($other.Name) / $a / $b" }
            }
        }
    }
}

function Assert-TaskStartGate {
    param([Parameter(Mandatory)]$Task)
    if ($Task.Status -cnotin 'assigned', 'working') { throw "착수는 assigned/working만 허용: $($Task.Id) / $($Task.Status)" }
    # 기존 dev/fix 또는 아직 원격에 없는 일반 통합에는 새 gate를 소급하지 않는다.
    if ($Task.Integration -notlike 'integration/*') { return }
    $localInfo = Get-IntegrationMetadata -Branch $Task.Integration -Ref $Task.Ref
    & git -C $script:RepoRoot show-ref --verify --quiet "refs/remotes/origin/$($Task.Integration)"
    if ($LASTEXITCODE -ne 0) {
        if ($localInfo.ActiveTask) { throw '순차 gate 통합의 최신 원격 문서가 없습니다.' }
        return
    }
    $info = Get-IntegrationMetadata -Branch $Task.Integration
    if (-not $info.ActiveTask) { return }
    if ($info.ActiveTask -cne $Task.Id) { throw "원격 Active Task 불일치: $($info.ActiveTask) / $($Task.Id)" }
    $assigned = Get-RefTask -Ref $info.Ref -Id $Task.Id
    if ($assigned.Status -cnotin 'assigned', 'working') { throw '원격 통합에 배정 Task 문서가 없습니다.' }
    # Status/작업 단계/갱신일만 작업자가 바꿀 수 있다. 나머지 메타와 배정 setup·plan은 실제 수신본이어야 한다.
    $mutable = '(?m)^- \*\*(Status|Current Skill|Updated):\*\*[^\r\n]*(?:\r?\n)?'
    if (([regex]::Replace($Task.Text, $mutable, '')).Trim() -cne ([regex]::Replace($assigned.Text, $mutable, '')).Trim()) { throw '배정 meta.md 문서 미수신' }
    foreach ($file in 'setup.md', 'plan.md') {
        $received = Get-RefFile -Ref $Task.Ref -Path "$($Task.Rel)/$file"
        $expected = Get-RefFile -Ref $info.Ref -Path "$($assigned.Rel)/$file"
        if ($received.Trim() -cne $expected.Trim()) { throw "배정 $file 문서 미수신: sync PR을 먼저 반영하세요." }
    }
    foreach ($source in $Task, $assigned) {
        $todo = Get-RefFile -Ref $source.Ref -Path "$($source.Rel)/todo.md"
        if ($todo -notmatch '(?m)^- \[x\] 담당자에게 실제 전달 — PM 확인\s*$') { throw '담당자에게 실제 전달 — PM 확인이 완료되지 않았습니다.' }
    }
}

function Assert-WorkPrHead {
    param([Parameter(Mandatory)][string]$Head)
    if ($Head -cnotmatch '^(feat|fix|refactor|resource)/') { throw "dev/integration 등 통합 head는 Squash 금지: $Head. 정식 Merge commit 절차를 사용하세요." }
}

function Get-UnityCli {
    # 순서: .env UNITY_CLI → 공식 설치 경로(~/.unity/bin) → PATH.
    # PATH에 다른 버전(예: Homebrew)이 먼저 잡힐 수 있다(Harness/Project/Pitfalls/duplicate-unity-cli.md).
    $configured = Get-EnvValue 'UNITY_CLI'
    if ($configured) { return $configured.Replace('~', $HOME) }
    $official = Join-Path $HOME '.unity/bin/unity'
    if (Test-Path -LiteralPath $official) { return $official }
    $cmd = Get-Command unity -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Invoke-NativeWithTimeout {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @(),
        [int]$TimeoutSeconds = 20
    )
    $psi = [System.Diagnostics.ProcessStartInfo]::new($FilePath)
    foreach ($a in $Arguments) { $psi.ArgumentList.Add($a) }
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $process = [System.Diagnostics.Process]::Start($psi)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { $process.Kill($true) } catch { }
        return [pscustomobject]@{ TimedOut = $true; ExitCode = $null; StdOut = ''; StdErr = '' }
    }
    $process.WaitForExit()
    return [pscustomobject]@{
        TimedOut = $false
        ExitCode = $process.ExitCode
        StdOut = $stdout.Result
        StdErr = $stderr.Result
    }
}

function ConvertFrom-UnityJson {
    # Unity CLI may print banner/progress lines; parse from the first '{'.
    param([string]$Text)
    if (-not $Text) { return $null }
    $start = $Text.IndexOf('{')
    if ($start -lt 0) { return $null }
    try { return $Text.Substring($start) | ConvertFrom-Json -Depth 20 } catch { return $null }
}

function Test-PathInside {
    param([Parameter(Mandatory)][string]$Child, [Parameter(Mandatory)][string]$Parent)
    $c = [System.IO.Path]::GetFullPath($Child).TrimEnd('/', '\') + [System.IO.Path]::DirectorySeparatorChar
    $p = [System.IO.Path]::GetFullPath($Parent).TrimEnd('/', '\') + [System.IO.Path]::DirectorySeparatorChar
    return $c.StartsWith($p, [System.StringComparison]::OrdinalIgnoreCase)
}

# ---------- GitHub ----------

function Get-GhCli {
    $cmd = Get-Command gh -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($p in '/opt/homebrew/bin/gh', '/usr/local/bin/gh', 'C:\Program Files\GitHub CLI\gh.exe') {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    return $null
}

function Get-GitHubRepo {
    # origin URL에서 owner/repo를 읽는다. gh 없이도 동작해야 한다.
    $url = (& git -C $script:RepoRoot remote get-url origin 2>$null)
    if ($url -notmatch 'github\.com[:/]([^/]+)/([^/]+?)(?:\.git)?/?$') { throw "origin이 GitHub 저장소가 아님: $url" }
    return [pscustomobject]@{ Owner = $Matches[1]; Name = $Matches[2]; Slug = "$($Matches[1])/$($Matches[2])" }
}

function Get-GhState {
    # Ready / NotInstalled / NotLoggedIn / NoRepoAccess
    $gh = Get-GhCli
    if (-not $gh) { return [pscustomobject]@{ State = 'NotInstalled'; Gh = $null; Detail = 'gh가 설치되어 있지 않음' } }
    $auth = Invoke-NativeWithTimeout -FilePath $gh -Arguments @('auth', 'status') -TimeoutSeconds 15
    if ($auth.TimedOut -or $auth.ExitCode -ne 0) {
        return [pscustomobject]@{ State = 'NotLoggedIn'; Gh = $gh; Detail = 'gh 로그인이 필요함' }
    }
    $repo = try { Get-GitHubRepo } catch { $null }
    if (-not $repo) { return [pscustomobject]@{ State = 'NoRepo'; Gh = $gh; Detail = 'origin이 GitHub 저장소가 아님' } }
    $view = Invoke-NativeWithTimeout -FilePath $gh -Arguments @('repo', 'view', $repo.Slug, '--json', 'name') -TimeoutSeconds 15
    if ($view.TimedOut -or $view.ExitCode -ne 0) {
        return [pscustomobject]@{ State = 'NoRepoAccess'; Gh = $gh; Detail = "$($repo.Slug) 접근 권한 없음 (PM에게 저장소 초대 요청)" }
    }
    return [pscustomobject]@{ State = 'Ready'; Gh = $gh; Detail = "gh 준비됨 ($($repo.Slug))" }
}

function New-PrTemplateBody {
    param([Parameter(Mandatory)][string]$Summary, [string]$Jira = '없음', [string]$Issues = '없음',
        [string]$ScopeCheck = '미실행', [string]$CompileCheck = '미실행', [string]$TestCheck = '미실행',
        [string]$PlayCheck = '미실행', [string]$MergeNote)
    $template = Get-Content -LiteralPath (Get-RepoPath '.github/PULL_REQUEST_TEMPLATE.md') -Raw
    $body = [System.Text.StringBuilder]::new()
    foreach ($part in [regex]::Split($template, '(?m)^(?=## )')) {
        if ($part -notmatch '^## (.+)') { [void]$body.Append($part); continue }
        $heading = $Matches[1].Trim()
        $value = switch -Regex ($heading) {
            '요약' { $Summary }
            'JIRA' { $Jira }
            'Issue' { $Issues }
            '검사' {
                $lines = foreach ($line in ($part -split "`n")) {
                    if ($line -notmatch '^\s*-\s*([^(:]+)') { continue }
                    $label = $Matches[1].Trim()
                    $check = switch -Regex ($label) {
                        '구역' { $ScopeCheck }
                        '컴파일' { $CompileCheck }
                        '테스트' { $TestCheck }
                        '플레이' { $PlayCheck }
                        default { '미실행' }
                    }
                    "- ${label}: $check"
                }
                $lines -join "`n"
            }
            default { $null }
        }
        if ($null -eq $value) { [void]$body.Append($part) }
        else { [void]$body.Append("## $heading`n`n$value`n`n") }
    }
    $text = $body.ToString().Trim() + "`n"
    if ($MergeNote) { $text += "`n$MergeNote`n" }
    return $text
}

function Open-Url {
    param([Parameter(Mandatory)][string]$Url)
    if ($IsMacOS) { & open $Url }
    elseif ($IsWindows) { Start-Process $Url }
    else { & xdg-open $Url }
}

function Get-MarkdownSection {
    # '## 제목' 아래 본문(다음 '## ' 전까지)을 돌려준다. 제목은 부분 일치.
    param([string]$Path, [Parameter(Mandatory)][string]$Heading, [string]$Text)
    if ($Path) {
        if (-not (Test-Path -LiteralPath $Path)) { return $null }
        $Text = Get-Content -LiteralPath $Path -Raw
    }
    $lines = "$Text" -split '\r?\n'
    $inside = $false
    $buffer = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        if ($line -match '^##\s+(.*)$') {
            if ($inside) { break }
            $inside = $Matches[1].Contains($Heading)
            continue
        }
        if ($inside) { $buffer.Add($line) }
    }
    if (-not $inside -and $buffer.Count -eq 0) { return $null }
    return ($buffer -join "`n").Trim()
}

function Find-CurrentTask {
    # 현재 checkout의 Active Task를 meta.md Branch 값으로 연결한다.
    param([string]$Branch)
    if (-not $Branch) { $Branch = (& git -C $script:RepoRoot branch --show-current).Trim() }
    if ($Branch -cnotmatch $script:TaskBranchPattern) { return $null }
    $type = $Matches[1]
    $dirs = @(Get-ChildItem -LiteralPath (Get-RepoPath 'Tasks/Active') -Directory -ErrorAction SilentlyContinue | Where-Object {
        $value = Get-MetaField -MetaPath (Join-Path $_.FullName 'meta.md') -Field 'Branch'
        ($value -replace '\s*\(.*\)$', '').Trim() -ceq $Branch
    })
    if ($dirs.Count -eq 0) { return $null }
    if ($dirs.Count -gt 1) { throw "브랜치에 연결된 Task가 여러 개입니다: $Branch" }
    $dir = $dirs[0]
    if ($dir.Name -notmatch $script:TaskIdPattern) { throw "Task 폴더 이름이 올바르지 않습니다: $($dir.Name)" }
    $id = "Task-$($Matches[1])-$($Matches[2])"
    if ((Get-MetaField -MetaPath (Join-Path $dir.FullName 'meta.md') -Field 'Type') -cne $type) { throw "Task 종류와 브랜치가 다릅니다: $id / $Branch" }
    return [pscustomobject]@{ Id = $id; Type = $type; Branch = $Branch; Path = $dir.FullName; Rel = "Tasks/Active/$($dir.Name)/" }
}

function Get-UsedTaskNumbers {
    # 작업 트리와 모든 로컬·원격 ref의 Active/Done Task ID로 번호를 보존한다.
    $numbers = [System.Collections.Generic.List[int]]::new()
    foreach ($t in Get-AllTaskFolders) { $numbers.Add($t.Number) }
    foreach ($t in Get-TaskRefMetadata -IncludeDone) { $numbers.Add($t.Number) }
    return @($numbers | Sort-Object -Unique)
}

function Get-SetupAreas {
    # setup.md의 백틱 경로: Allowed(작업 구역 + 배정된 공용 파일), Forbidden(수정 금지). Scripts/·Harness/는 하네스 Task용(PM 전용 경로 검사는 따로 한다)
    param([string]$SetupPath, [string]$Text)
    if ($SetupPath) { $Text = Get-Content -LiteralPath $SetupPath -Raw }
    $pick = { param($text) @([regex]::Matches("$text", '`([^`]+)`') | ForEach-Object { $_.Groups[1].Value.Trim() } | Where-Object { $_ -match '^(Assets|ProjectSettings|Packages|Docs|Scripts|Harness)/' }) }
    return [pscustomobject]@{
        Allowed = @(& $pick (Get-MarkdownSection -Text $Text -Heading '작업 구역')) + @(& $pick (Get-MarkdownSection -Text $Text -Heading '배정된 공용 파일'))
        Forbidden = @(& $pick (Get-MarkdownSection -Text $Text -Heading '수정 금지'))
    }
}

function Test-PathInArea {
    # area가 '/'로 끝나면 폴더(하위 전체), 아니면 파일. .meta는 대상 파일·폴더의 짝으로 본다.
    param([Parameter(Mandatory)][string]$Path, [string[]]$Areas)
    $base = $Path -replace '\.meta$', ''
    foreach ($a in $Areas) {
        if ($a.EndsWith('/')) {
            if ($Path.StartsWith($a) -or "$base/" -eq $a) { return $true }
        } elseif ($Path -eq $a -or $base -eq $a) { return $true }
    }
    return $false
}

function Get-CodeOwnerPatterns {
    # .github/CODEOWNERS의 경로 패턴(PM 전용 경로)을 정규식으로 바꿔 돌려준다.
    $file = Get-RepoPath '.github/CODEOWNERS'
    if (-not (Test-Path -LiteralPath $file)) { return @() }
    return @(Get-Content -LiteralPath $file | Where-Object { $_ -match '^\s*[^#\s]' } | ForEach-Object {
        $pattern = ($_ -split '\s+')[0].TrimStart('/')
        $regex = [regex]::Escape($pattern).Replace('\*\*/', '(?:.*/)?').Replace('\*', '[^/]*')
        if ($pattern.EndsWith('/')) { "^$regex" } else { "^$regex$" }
    })
}

function Get-PmAccount {
    $team = Get-RepoPath 'Harness/Project/Facts/team.md'
    $m = Select-String -LiteralPath $team -Pattern '개발 PM\s*\|\s*`@([^`]+)`' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($m) { return $m.Matches[0].Groups[1].Value }
    return $null
}

function Test-ThirdPartyAssetManager {
    param([string]$Actor)
    if (-not $Actor) { return $false }
    $team = Get-RepoPath 'Harness/Project/Facts/team.md'
    if (-not (Test-Path -LiteralPath $team)) { return $false }
    $accounts = @(Select-String -LiteralPath $team -Pattern '^\|\s*에셋 관리자\s*\|\s*`@([^`]+)`\s*\|' |
        ForEach-Object { $_.Matches[0].Groups[1].Value })
    return $accounts -contains $Actor
}

function Get-SharedAllowance {
    # team-roles.md "모든 작업자에게 허용": setup.md와 무관하게 통과시키는 경로. 해당하면 이유 문자열, 아니면 $null.
    param([Parameter(Mandatory)][string]$Path, [bool]$IsNew, [bool]$HasTask, [bool]$CanEditAssets)
    if ($Path -eq 'ProjectSettings/EditorBuildSettings.asset') { return '빌드 씬 목록' }
    if ($Path -like 'Assets/Resources/Settings/GameSettings/*' -or $Path -eq 'Assets/Resources/Settings/GameSettings.meta') { return '게임 세팅 값' }
    if ($HasTask -and ($Path -like 'Assets/Scripts/Systems/Settings/*' -or $Path -eq 'Assets/Scripts/Systems/Settings.meta')) { return '게임 세팅 연결 기능' }
    # 에셋 = Assets/ 아래 코드·씬이 아닌 파일(폴더 .meta 포함). 추가는 모두, 수정·삭제는 PM·에셋 관리자.
    $base = $Path -replace '\.meta$', ''
    if ($Path -notlike 'Assets/*' -or $base -eq 'Assets/_ThirdParty' -or $base -match '\.(cs|asmdef|asmref|unity)$') { return $null }
    if ($IsNew) { return '에셋 추가' }
    if ($CanEditAssets) { return '에셋 수정·삭제(PM·에셋 관리자)' }
    return $null
}

function Get-UrgentException {
    # team-roles.md "급한 예외": Task 폴더 exception.md의 승인자·사유가 모두 있으면 예외 정보를 돌려준다.
    param([Parameter(Mandatory)][string]$TaskPath)
    $file = Join-Path $TaskPath 'exception.md'
    if (-not (Test-Path -LiteralPath $file)) { return $null }
    $approvedBy = Get-MetaField -MetaPath $file -Field 'Approved by'
    $reason = Get-MetaField -MetaPath $file -Field 'Reason'
    if (-not $approvedBy -or -not $reason) { Write-Host '[주의] exception.md에 Approved by·Reason이 없어 급한 예외를 적용하지 않습니다.'; return $null }
    return [pscustomobject]@{ ApprovedBy = $approvedBy; Reason = $reason }
}

function Get-CurrentGitHubUser {
    if ($env:GITHUB_ACTOR) { return $env:GITHUB_ACTOR }
    $gh = Get-GhCli
    if (-not $gh) { return $null }
    $r = Invoke-NativeWithTimeout -FilePath $gh -Arguments @('api', 'user', '--jq', '.login') -TimeoutSeconds 15
    if ($r.TimedOut -or $r.ExitCode -ne 0) { return $null }
    return $r.StdOut.Trim()
}

function Set-MetaField {
    param([Parameter(Mandatory)][string]$MetaPath, [Parameter(Mandatory)][string]$Field, [Parameter(Mandatory)][AllowEmptyString()][string]$Value)
    $text = Get-Content -LiteralPath $MetaPath -Raw
    $text = [regex]::Replace($text, "(?m)^(\s*-\s*\*\*$([regex]::Escape($Field)):\*\*).*$", "`$1 $Value")
    Set-Content -LiteralPath $MetaPath -Value $text -Encoding utf8NoBOM -NoNewline
}

function Set-TodoChecked {
    # todo.md에서 글자가 $Text로 시작하는 첫 미완료 항목(- [ ])을 [x]로 바꾼다. 항목 글자는 그대로 둔다(패널이 글자로 비교).
    param([Parameter(Mandatory)][string]$TodoPath, [Parameter(Mandatory)][string]$Text)
    if (-not (Test-Path -LiteralPath $TodoPath)) { return $false }
    $content = Get-Content -LiteralPath $TodoPath -Raw
    $regex = [regex]::new("(?m)^(\s*[-*]\s+)\[ \](\s+$([regex]::Escape($Text)))")
    if (-not $regex.IsMatch($content)) { return $false }
    Set-Content -LiteralPath $TodoPath -Value $regex.Replace($content, '$1[x]$2', 1) -Encoding utf8NoBOM -NoNewline
    return $true
}

function Set-IntegrationTaskRow {
    # Integration meta.md Tasks 표에서 Task 행을 추가하거나 갱신한다. $Columns: Task, 종류, 담당, 브랜치, PR, 상태
    param([Parameter(Mandatory)][string]$MetaPath, [Parameter(Mandatory)][AllowEmptyString()][string[]]$Columns)
    if (-not (Test-Path -LiteralPath $MetaPath)) { return $false }
    $lines = [System.Collections.Generic.List[string]]::new([string[]](Get-Content -LiteralPath $MetaPath))
    $row = '| ' + ($Columns -join ' | ') + ' |'
    $head = $lines.FindIndex([Predicate[string]]{ param($l) $l -match '^## Tasks\s*$' })
    if ($head -lt 0) { return $false }
    $end = $head + 1
    while ($end -lt $lines.Count -and $lines[$end] -notmatch '^## ') { $end++ }
    $existing = -1
    for ($i = $head + 1; $i -lt $end; $i++) {
        if ($lines[$i].StartsWith("| $($Columns[0]) |")) { $existing = $i; break }
    }
    if ($existing -ge 0) {
        $old = $lines[$existing].Trim('|').Split('|') | ForEach-Object { $_.Trim() }
        $merged = for ($i = 0; $i -lt $Columns.Count; $i++) { if ($Columns[$i]) { $Columns[$i] } else { $old[$i] } }
        $lines[$existing] = '| ' + ($merged -join ' | ') + ' |'
    } else {
        $i = $head + 1
        while ($i -lt $lines.Count -and -not $lines[$i].StartsWith('|')) { $i++ }
        while ($i -lt $lines.Count -and $lines[$i].StartsWith('|')) { $i++ }
        $lines.Insert($i, $row)
    }
    Set-Content -LiteralPath $MetaPath -Value (($lines -join "`n") + "`n") -Encoding utf8NoBOM -NoNewline
    return $true
}

function Assert-CleanTree {
    param([string]$Hint = '먼저 "저장해줘"로 저장하세요.')
    $dirty = & git --no-optional-locks -C $script:RepoRoot status --porcelain --untracked-files=all --ignore-submodules=none
    if ($LASTEXITCODE -ne 0) { throw '작업 폴더 상태를 확인하지 못했습니다.' }
    if ($dirty) { throw "저장하지 않은 변경이 있습니다. $Hint`n$($dirty -join "`n")" }
}

function Get-AssetMetaPaths {
    # Assets/ 경로에 필요한 .meta 목록: 자기 자신 + Assets와 자신 사이의 모든 상위 폴더.
    # 예: Assets/Scenes/Tests/Curtain/Test.unity → Assets/Scenes.meta, Assets/Scenes/Tests.meta, Assets/Scenes/Tests/Curtain.meta, …/Test.unity.meta
    param([Parameter(Mandatory)][string]$Path)
    $clean = $Path.TrimEnd('/')
    if ($clean -notlike 'Assets/*' -or $clean -like '*.meta') { return @() }
    $parts = $clean.Split('/')
    # Unity가 무시하는 점 파일(.gitkeep 등)은 자기 .meta 없이 상위 폴더만 검사한다.
    $count = if ($parts[-1].StartsWith('.')) { $parts.Count - 1 } else { $parts.Count }
    $result = for ($i = 2; $i -le $count; $i++) { ($parts[0..($i - 1)] -join '/') + '.meta' }
    return @($result)
}

function Get-WorktreeFingerprint {
    # 추적 파일과 미추적 일반 파일의 내용 및 인덱스 상태를 묶는다.
    $paths = @(& git -C $script:RepoRoot ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw '작업 파일 목록을 읽지 못했습니다.' }
    $lines = foreach ($relative in ($paths | Sort-Object -Unique)) {
        $path = Join-Path $script:RepoRoot $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { "$relative MISSING"; continue }
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        "$relative $hash"
    }
    $staged = & git -C $script:RepoRoot diff --cached --binary
    if ($LASTEXITCODE -ne 0) { throw '스테이징 변경을 읽지 못했습니다.' }
    $lines += "STAGED $($staged -join "`n")"
    # 병합 대상이나 HEAD가 바뀌면 이전 검증 결과를 재사용하지 않는다.
    $head = & git -C $script:RepoRoot rev-parse --verify HEAD
    if ($LASTEXITCODE -ne 0) { throw '현재 커밋을 읽지 못했습니다.' }
    $mergeHead = & git -C $script:RepoRoot rev-parse --verify --quiet MERGE_HEAD 2>$null
    $lines += "HEAD $head", "MERGE_HEAD $mergeHead"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes))
}

function Get-VerificationEvidencePath {
    return Join-Path $script:RepoRoot '.harness/local/check-work-evidence.json'
}

function Save-VerificationEvidence {
    param([Parameter(Mandatory)][string]$Branch)
    $path = Get-VerificationEvidencePath
    New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force | Out-Null
    @{ Branch = $Branch; Fingerprint = (Get-WorktreeFingerprint) } |
        ConvertTo-Json -Compress | Set-Content -LiteralPath $path -Encoding utf8NoBOM
}

function Test-VerificationEvidence {
    param([Parameter(Mandatory)][string]$Branch)
    $path = Get-VerificationEvidencePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $false }
    try {
        $evidence = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($evidence.Branch -ne $Branch -or $evidence.Fingerprint -notmatch '^[A-F0-9]{64}$') { return $false }
        return $evidence.Fingerprint -eq (Get-WorktreeFingerprint)
    } catch { return $false }
}

# ---------- 커밋 작성자 (Harness/Core/Policies/git-workflow.md "작성자") ----------

function Get-GitHubIdentity {
    # gh 로그인 계정 → 커밋 작성자. 이메일은 계정과 항상 연결되는 noreply 주소.
    $gh = Get-GhCli
    if (-not $gh) { return $null }
    $r = Invoke-NativeWithTimeout -FilePath $gh -Arguments @('api', 'user', '--jq', '"\(.login) \(.id)"') -TimeoutSeconds 15
    if ($r.TimedOut -or $r.ExitCode -ne 0 -or -not $r.StdOut.Trim()) { return $null }
    $login, $id = $r.StdOut.Trim().Split(' ')
    return [pscustomobject]@{ Login = $login; Name = $login; Email = "$id+$login@users.noreply.github.com" }
}

function Set-GitIdentity {
    # 이 저장소(local)의 커밋 작성자를 gh 계정으로 맞추고 커밋 차단 훅을 켠다.
    param([Parameter(Mandatory)]$Identity)
    & git -C $script:RepoRoot config --local user.name $Identity.Name
    & git -C $script:RepoRoot config --local user.email $Identity.Email
    & git -C $script:RepoRoot config --local core.hooksPath .githooks
}

function Assert-CommitIdentity {
    # 커밋하는 스크립트가 커밋 전에 호출한다. 작성자가 gh 계정이 아니거나 훅이 꺼져 있으면 멈춘다.
    $identity = Get-GitHubIdentity
    if (-not $identity) { throw 'GitHub 연결이 필요합니다. 커밋은 내 GitHub 계정으로만 할 수 있어요. AI에게 "GitHub 연결해줘"라고 말하세요.' }
    $name = (& git -C $script:RepoRoot config user.name)
    $email = (& git -C $script:RepoRoot config user.email)
    $hooks = (& git -C $script:RepoRoot config core.hooksPath)
    if ($name -ne $identity.Name -or $email -ne $identity.Email -or $hooks -ne '.githooks') {
        throw "커밋 작성자($name <$email>)가 GitHub 계정(@$($identity.Login))과 다르거나 커밋 검사가 꺼져 있습니다. pwsh -File Scripts/setup-gh.ps1 을 실행하세요."
    }
}

# Git 환경은 자식 프로세스에만 적용한다. 수신 중인 Git/LFS는 timeout으로 종료하지 않는다.
function Invoke-TransitionGit {
    param([string]$Path, [string[]]$Arguments, [switch]$AllowFailure)
    $exe = (Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $psi = [System.Diagnostics.ProcessStartInfo]::new($exe)
    $psi.WorkingDirectory = $Path
    foreach ($key in 'GIT_DIR', 'GIT_INDEX_FILE', 'GIT_WORK_TREE', 'GIT_PREFIX', 'GIT_COMMON_DIR', 'GIT_OBJECT_DIRECTORY', 'GIT_ALTERNATE_OBJECT_DIRECTORIES') {
        [void]$psi.Environment.Remove($key)
    }
    $psi.Environment['GIT_LFS_SKIP_SMUDGE'] = '1'
    $psi.Environment['GIT_OPTIONAL_LOCKS'] = '0'
    foreach ($arg in (@('-C', $Path, '-c', 'submodule.recurse=false', '-c', 'core.quotePath=false') + $Arguments)) { $psi.ArgumentList.Add($arg) }
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $psi.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)
    $process = [System.Diagnostics.Process]::Start($psi)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        while (-not $process.WaitForExit(30000)) { Write-Host '[대기] Git/LFS가 실행 중입니다. 강제 종료·잠금 삭제 없이 기다립니다.' }
        $text = $stdout.GetAwaiter().GetResult()
        [void]$stderr.GetAwaiter().GetResult()
        if ($AllowFailure) { return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $text } }
        if ($process.ExitCode -ne 0) { throw "Git 명령 실패 ($($Arguments[0]), exit $($process.ExitCode)). 권한·네트워크·Git 상태를 확인하세요. 자동 복구하지 않았습니다." }
        return $text
    } finally { $process.Dispose() }
}

function Test-ThirdPartyRepository {
    param([string]$Path = $script:RepoRoot)
    $sub = Join-Path $Path 'Assets/_ThirdParty'
    if (-not (Test-Path -LiteralPath (Join-Path $sub '.git'))) { return $false }
    $result = Invoke-TransitionGit $sub @('rev-parse', '--show-toplevel') -AllowFailure
    return $result.ExitCode -eq 0 -and [IO.Path]::GetFullPath($result.Output.Trim()) -eq [IO.Path]::GetFullPath($sub)
}

function Get-TransitionGitlink {
    param([string]$Path, [string]$Commit)
    $rows = (Invoke-TransitionGit $Path @('ls-tree', '-r', '-z', $Commit)).Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)
    $links = @($rows | Where-Object { $_ -match '^160000 ' })
    if (-not $links.Count) { return $null }
    if ($links.Count -ne 1 -or $links[0] -notmatch '^160000 commit ([0-9a-f]{40,64})\tAssets/_ThirdParty$') { throw '지원하지 않는 submodule 구조입니다. 전환 전에 PM 확인이 필요합니다.' }
    $sha = $Matches[1]
    $config = Invoke-TransitionGit $Path @('config', '--blob', "${Commit}:.gitmodules", '--get-regexp', '^submodule\..*\.path$')
    $entries = @($config.Trim() -split '\r?\n')
    if ($entries.Count -ne 1 -or $entries[0] -notmatch '^submodule\..*\.path Assets/_ThirdParty$') { throw '대상 .gitmodules와 gitlink가 일치하지 않습니다.' }
    return $sha
}

function Get-TransitionLfsFiles {
    param([string]$Path, [string]$Commit)
    $data = (Invoke-TransitionGit $Path @('lfs', 'ls-files', '--json', $Commit)) | ConvertFrom-Json
    foreach ($file in $data.files) {
        if ($file.oid_type -ne 'sha256' -or $file.oid -notmatch '^[0-9a-f]{64}$' -or $file.size -lt 0) { throw '지원하지 않는 LFS 포인터입니다.' }
        $file
    }
}

function Assert-TransitionLfsFiles {
    param([string]$Path, [object[]]$Files, [switch]$Cache, [switch]$VerifyHash)
    if (-not $Files.Count) { return }
    if ($Cache) {
        $line = ((Invoke-TransitionGit $Path @('lfs', 'env')) -split '\r?\n' | Where-Object { $_.StartsWith('LocalMediaDir=') } | Select-Object -First 1)
        if (-not $line) { throw 'LFS 캐시 위치를 확인하지 못했습니다.' }
        $media = $line.Substring('LocalMediaDir='.Length)
    }
    foreach ($file in $Files) {
        $full = if ($Cache) { Join-Path $media "$($file.oid.Substring(0, 2))/$($file.oid.Substring(2, 2))/$($file.oid)" } else { Join-Path $Path $file.name }
        if (-not (Test-Path -LiteralPath $full -PathType Leaf) -or (Get-Item -LiteralPath $full).Length -ne $file.size) { throw "LFS 실파일/캐시 누락 또는 크기 불일치: $($file.name)" }
        if ($VerifyHash -and (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash -ine $file.oid) { throw "LFS hash 불일치: $($file.name)" }
    }
}

function Assert-TransitionTrackedFiles {
    param([string]$Path, [string]$Commit)
    foreach ($row in (Invoke-TransitionGit $Path @('ls-tree', '-r', '-z', $Commit)).Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)) {
        if ($row -match '^160000 ') { continue }
        $name = $row.Substring($row.IndexOf("`t") + 1)
        if (-not (Test-Path -LiteralPath (Join-Path $Path $name))) { throw "추적 파일 누락: $name" }
    }
}

function Get-ThirdPartyState {
    param([string]$Path = $script:RepoRoot, [switch]$VerifyHash)
    $state = [pscustomobject]@{ State = 'Error'; Detail = ''; Repo = $null }
    try {
        $sha = Get-TransitionGitlink $Path HEAD
        if (-not $sha) {
            if (Test-Path -LiteralPath (Join-Path $Path 'Assets/_ThirdParty')) { $state.Detail = 'gitlink 없는 외부 에셋 폴더가 남아 있습니다.'; return $state }
            $state.State = 'Absent'; $state.Detail = '대상 submodule 없음'; return $state
        }
        if (-not (Test-ThirdPartyRepository $Path)) { $state.State = 'Uninitialized'; $state.Detail = '독립 submodule 초기화가 필요합니다.'; return $state }
        $state.Repo = Join-Path $Path 'Assets/_ThirdParty'
        if ((Invoke-TransitionGit $state.Repo @('rev-parse', 'HEAD')).Trim() -ne $sha) { $state.State = 'HeadMismatch'; $state.Detail = 'gitlink와 private HEAD가 다릅니다.'; return $state }
        try { Assert-TransitionTrackedFiles $state.Repo HEAD } catch { $state.State = 'MissingFiles'; $state.Detail = $_.Exception.Message; return $state }
        try { Assert-TransitionLfsFiles $state.Repo @(Get-TransitionLfsFiles $state.Repo HEAD) -VerifyHash:$VerifyHash } catch { $state.State = 'LfsIncomplete'; $state.Detail = $_.Exception.Message; return $state }
        if ((Invoke-TransitionGit $state.Repo @('status', '--porcelain', '--untracked-files=all', '--ignore-submodules=none')).Trim()) { $state.State = 'Dirty'; $state.Detail = 'private 저장소에 보존할 변경이 있습니다.'; return $state }
        $state.State = 'Ready'; $state.Detail = 'gitlink·추적 파일·LFS 실파일 확인'; return $state
    } catch { $state.Detail = $_.Exception.Message; return $state }
}

function Test-ThirdPartyReady {
    param([string]$Path = $script:RepoRoot)
    return (Get-ThirdPartyState $Path).State -in 'Ready', 'Absent'
}

function Assert-TransitionIdle {
    param([string]$Path)
    foreach ($dir in @((Invoke-TransitionGit $Path @('rev-parse', '--absolute-git-dir')).Trim(), (Invoke-TransitionGit $Path @('rev-parse', '--path-format=absolute', '--git-common-dir')).Trim()) | Select-Object -Unique) {
        if (@(Get-ChildItem -LiteralPath $dir -Filter '*.lock' -Force -ErrorAction Stop).Count) { throw 'Git 잠금이 있습니다. 잠금을 삭제하지 말고 진행 중 작업을 확인하세요.' }
        $refs = Join-Path $dir 'refs'
        if ((Test-Path -LiteralPath $refs) -and @(Get-ChildItem -LiteralPath $refs -Filter '*.lock' -Recurse -Force).Count) { throw 'Git ref 잠금이 있습니다.' }
        foreach ($name in 'MERGE_HEAD', 'CHERRY_PICK_HEAD', 'REVERT_HEAD', 'rebase-merge', 'rebase-apply', 'sequencer', 'BISECT_START') {
            if (Test-Path -LiteralPath (Join-Path $dir $name)) { throw "진행 중 Git 작업: $name" }
        }
    }
    foreach ($running in @(Get-Process -Name git, git-lfs -ErrorAction SilentlyContinue)) {
        try { if (-not $running.WaitForExit(2000)) { throw 'Git/LFS 프로세스가 실행 중입니다. 완료 후 다시 시도하세요(자동 종료하지 않음).' } }
        finally { $running.Dispose() }
    }
}

function Get-TransitionSnapshot {
    param([string]$Path)
    Assert-TransitionIdle $Path
    if ((Invoke-TransitionGit $Path @('status', '--porcelain', '--untracked-files=all', '--ignore-submodules=none')).Trim()) { throw '저장하지 않은 변경이 있습니다. 자동 stash·삭제 없이 중단합니다.' }
    if ((Invoke-TransitionGit $Path @('ls-files', '-v')) -cmatch '(?m)^[a-zS] ') { throw '숨긴 index 변경(assume-unchanged/skip-worktree)은 안전 전환에서 지원하지 않습니다.' }
    $head = (Invoke-TransitionGit $Path @('rev-parse', 'HEAD')).Trim()
    $index = (Invoke-TransitionGit $Path @('rev-parse', '--path-format=absolute', '--git-path', 'index')).Trim()
    return [pscustomobject]@{
        Head = $head
        Branch = (Invoke-TransitionGit $Path @('symbolic-ref', '--quiet', '--short', 'HEAD') -AllowFailure).Output.Trim()
        IndexHash = if (Test-Path -LiteralPath $index) { (Get-FileHash -LiteralPath $index).Hash } else { '' }
    }
}

function Assert-TransitionSnapshot {
    param([string]$Path, $Expected)
    $actual = Get-TransitionSnapshot $Path
    if ($actual.Head -ne $Expected.Head -or $actual.Branch -cne $Expected.Branch -or $actual.IndexHash -ne $Expected.IndexHash) { throw '준비 중 다른 작업이 HEAD·브랜치·index를 변경했습니다. 전환하지 않습니다.' }
}

function Assert-TransitionUntrackedCollision {
    param([string]$Path, [string]$Commit)
    $others = (Invoke-TransitionGit $Path @('ls-files', '--others', '-z')).Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)
    if (-not $others.Count) { return }
    $targets = (Invoke-TransitionGit $Path @('ls-tree', '-r', '--name-only', '-z', $Commit)).Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)
    $set = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $others) { [void]$set.Add($name.TrimEnd('/')) }
    foreach ($target in $targets) {
        $part = $target
        while ($part) {
            if ($set.Contains($part)) { throw "무시된 파일과 대상 경로가 충돌합니다: $part" }
            $slash = $part.LastIndexOf('/'); $part = if ($slash -gt 0) { $part.Substring(0, $slash) } else { '' }
        }
    }
    $targetSet = [Collections.Generic.HashSet[string]]::new([string[]]$targets, [StringComparer]::OrdinalIgnoreCase)
    foreach ($other in $others) {
        $part = $other.TrimEnd('/')
        while ($part.Contains('/')) {
            $part = $part.Substring(0, $part.LastIndexOf('/'))
            if ($targetSet.Contains($part)) { throw "대상 파일 아래 무시된 파일이 있습니다: $other" }
        }
    }
}

function Prepare-TransitionRepository {
    param([string]$Path, [string]$Commit)
    $object = Invoke-TransitionGit $Path @('cat-file', '-e', "$Commit^{commit}") -AllowFailure
    if ($object.ExitCode -ne 0) {
        Write-Host '[준비] 대상 Git 객체를 받습니다(작업 폴더 전환 없음).'
        [void](Invoke-TransitionGit $Path @('fetch', '--no-recurse-submodules', '--no-tags', 'origin', $Commit))
        [void](Invoke-TransitionGit $Path @('cat-file', '-e', "$Commit^{commit}"))
    }
    Assert-TransitionUntrackedCollision $Path $Commit
    $files = @(Get-TransitionLfsFiles $Path $Commit)
    if ($files.Count) {
        Write-Host "[준비] 대상 LFS $($files.Count)개를 확인·수신합니다."
        [void](Invoke-TransitionGit $Path @('lfs', 'fetch', '--include=', '--exclude=', 'origin', $Commit))
        Assert-TransitionLfsFiles $Path $files -Cache -VerifyHash
    }
    return [pscustomobject]@{ Path = $Path; Commit = $Commit; Files = $files }
}

function Complete-TransitionRepository {
    param($Preparation)
    if ((Invoke-TransitionGit $Preparation.Path @('rev-parse', 'HEAD')).Trim() -ne $Preparation.Commit) { throw '반영된 HEAD가 준비한 SHA와 다릅니다.' }
    if ($Preparation.Files.Count) { [void](Invoke-TransitionGit $Preparation.Path @('-c', 'lfs.fetchinclude=', '-c', 'lfs.fetchexclude=', 'lfs', 'checkout')) }
    Assert-TransitionTrackedFiles $Preparation.Path $Preparation.Commit
    Assert-TransitionLfsFiles $Preparation.Path $Preparation.Files -VerifyHash
}

function Update-ThirdParty {
    param([string]$Path = $script:RepoRoot, $Preparation)
    if (-not $Preparation) {
        $state = Get-ThirdPartyState $Path -VerifyHash
        if ($state.State -notin 'Ready', 'Absent') { throw "외부 에셋: $($state.State) — $($state.Detail). 안전 전환 또는 PM 초기화가 필요합니다." }
        return
    }
    if ((Get-TransitionGitlink $Path HEAD) -ne $Preparation.Commit) { throw '반영할 gitlink가 준비한 대상과 다릅니다.' }
    Assert-TransitionIdle $Preparation.Path
    if ((Invoke-TransitionGit $Preparation.Path @('status', '--porcelain', '--untracked-files=all')).Trim()) { throw '전환 중 private 변경이 발생했습니다. 보존 후 중단합니다.' }
    [void](Invoke-TransitionGit $Path @('submodule', 'update', '--checkout', '--no-fetch', '--', 'Assets/_ThirdParty'))
    Complete-TransitionRepository $Preparation
}

function Invoke-SafeBranchTransition {
    param([string]$Path = $script:RepoRoot, [Parameter(Mandatory)][string]$TargetCommit, [string]$Branch, [switch]$CreateBranch, [switch]$Track)
    $lock = $null
    $phase = '사전 보호'
    try {
        $common = (Invoke-TransitionGit $Path @('rev-parse', '--path-format=absolute', '--git-common-dir')).Trim()
        # 파일은 남겨 두고 열린 핸들만 잠근다. 다른 실행의 잠금 파일을 삭제할 경쟁을 만들지 않는다.
        try { $lock = [IO.File]::Open((Join-Path $common 'harness-transition.guard'), 'OpenOrCreate', 'ReadWrite', 'None') } catch { throw '다른 안전 전환이 실행 중입니다.' }
        $before = Get-TransitionSnapshot $Path
        $target = (Invoke-TransitionGit $Path @('rev-parse', '--verify', "$TargetCommit^{commit}")).Trim()
        $intermediate = $target
        if ($Branch) {
            [void](Invoke-TransitionGit $Path @('check-ref-format', '--branch', $Branch))
            if ($CreateBranch -and $Track) { throw '새 브랜치와 원격 추적 옵션을 동시에 쓸 수 없습니다.' }
            if (-not $CreateBranch) {
                $ref = if ($Track) { "refs/remotes/origin/$Branch" } else { "refs/heads/$Branch" }
                $intermediate = (Invoke-TransitionGit $Path @('rev-parse', '--verify', "$ref^{commit}")).Trim()
                [void](Invoke-TransitionGit $Path @('merge-base', '--is-ancestor', $intermediate, $target))
            }
        } else {
            if ($CreateBranch -or $Track) { throw '브랜치 이름이 필요합니다.' }
            [void](Invoke-TransitionGit $Path @('merge-base', '--is-ancestor', $before.Head, $target))
        }
        $currentLink = Get-TransitionGitlink $Path $before.Head
        $targetLink = Get-TransitionGitlink $Path $target
        $middleLink = Get-TransitionGitlink $Path $intermediate
        if ([bool]$currentLink -ne [bool]$targetLink -or [bool]$middleLink -ne [bool]$targetLink) { throw 'submodule 추가·제거 전환은 PM의 별도 초기화가 필요합니다.' }
        $sub = $null; $subBefore = $null
        if ($currentLink) {
            $state = Get-ThirdPartyState $Path -VerifyHash
            if ($state.State -ne 'Ready') { throw "외부 에셋 $($state.State): $($state.Detail)" }
            $sub = $state.Repo; $subBefore = Get-TransitionSnapshot $sub
            if ((Invoke-TransitionGit $sub @('ls-tree', '-r', 'HEAD')) -match '(?m)^160000 ') { throw '중첩 submodule 전환은 지원하지 않습니다.' }
        } elseif (Test-Path -LiteralPath (Join-Path $Path 'Assets/_ThirdParty')) { throw 'gitlink 없는 외부 에셋 폴더가 남아 있습니다.' }
        if (@(Get-ThirdPartyLeftovers $Path).Count) { throw '예전 외부 에셋 잔여 폴더가 있습니다. 자동 삭제하지 않습니다. PM에게 확인하세요.' }
        Assert-TransitionTrackedFiles $Path $before.Head
        Assert-TransitionLfsFiles $Path @(Get-TransitionLfsFiles $Path $before.Head) -VerifyHash
        $phase = 'public 사전 수신'
        $parentPreparation = Prepare-TransitionRepository $Path $target
        if ($intermediate -ne $target) { [void](Prepare-TransitionRepository $Path $intermediate) }
        $subPreparation = $null
        if ($sub) {
            $phase = 'private 사전 수신'
            $subPreparation = Prepare-TransitionRepository $sub $targetLink
            if ((Invoke-TransitionGit $sub @('ls-tree', '-r', $targetLink)) -match '(?m)^160000 ') { throw '대상 중첩 submodule은 지원하지 않습니다.' }
            if ($middleLink -ne $targetLink) { [void](Prepare-TransitionRepository $sub $middleLink) }
            Assert-TransitionSnapshot $sub $subBefore
        }
        $phase = '경쟁 재검사'
        Assert-TransitionSnapshot $Path $before
        if ($Branch -and -not $CreateBranch -and (Invoke-TransitionGit $Path @('rev-parse', "$ref^{commit}")).Trim() -ne $intermediate) { throw '준비 중 대상 브랜치가 변경되었습니다.' }
        $phase = 'public 전환'
        Write-Host '[전환] 사전 수신·검증 완료. 로컬 캐시만 사용합니다.'
        if ($Branch) {
            if ($CreateBranch) { [void](Invoke-TransitionGit $Path @('switch', '--no-overwrite-ignore', '-c', $Branch, $target)) }
            elseif ($Track) { [void](Invoke-TransitionGit $Path @('switch', '--no-overwrite-ignore', '--track', "origin/$Branch")) }
            else { [void](Invoke-TransitionGit $Path @('switch', '--no-overwrite-ignore', $Branch)) }
        }
        if ((Invoke-TransitionGit $Path @('rev-parse', 'HEAD')).Trim() -ne $target) { [void](Invoke-TransitionGit $Path @('merge', '--ff-only', '--no-overwrite-ignore', $target)) }
        $phase = '캐시 반영·사후 검증'
        if ($subPreparation) { Update-ThirdParty -Path $Path -Preparation $subPreparation }
        Complete-TransitionRepository $parentPreparation
        [void](Get-TransitionSnapshot $Path)
        if ($sub) { [void](Get-TransitionSnapshot $sub) }
        if ($Branch -and (Invoke-TransitionGit $Path @('branch', '--show-current')).Trim() -cne $Branch) { throw '사후 브랜치 불일치' }
        Write-Host '[완료] HEAD·gitlink·추적 파일·LFS hash·변경 상태를 검증했습니다.'
    } catch {
        $reason = $_.Exception.Message
        $heads = 'HEAD 조회 불가'
        try {
            $publicHead = (Invoke-TransitionGit $Path @('rev-parse', '--verify', 'HEAD')).Trim()
            $privateHead = if (Test-ThirdPartyRepository $Path) { (Invoke-TransitionGit (Join-Path $Path 'Assets/_ThirdParty') @('rev-parse', '--verify', 'HEAD')).Trim() } else { '미초기화/없음' }
            $heads = "public=$publicHead, private=$privateHead"
        } catch { }
        throw "안전 전환 중단 [$phase]: $reason 현재 HEAD: $heads. 자동 rollback·삭제 없이 현재 상태를 보존했습니다. PM에게 현재 상태 확인을 요청하세요."
    } finally { if ($lock) { $lock.Dispose() } }
}

# 이전 경로는 진단만 한다. 비추적 파일과 Unity가 수정한 .meta도 임의로 지우거나 복구하지 않는다.
$script:ThirdPartyLegacyPaths = @('Assets/Plugins/Demigiant', 'Assets/Plugins')
function Get-ThirdPartyLeftovers {
    param([string]$Path = $script:RepoRoot)
    foreach ($rel in $script:ThirdPartyLegacyPaths) {
        if ((Test-Path -LiteralPath (Join-Path $Path $rel)) -and -not (Invoke-TransitionGit $Path @('ls-files', '--', $rel, "$rel.meta")).Trim()) { $rel }
    }
}

function Remove-ThirdPartyLeftovers {
    param([string]$Path = $script:RepoRoot)
    if (@(Get-ThirdPartyLeftovers $Path).Count) { throw '자동 잔여물 정리는 지원하지 않습니다. 파일을 보존하고 PM에게 확인하세요.' }
}
