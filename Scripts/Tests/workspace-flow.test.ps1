# 격리 Git fixture와 gh mock만 사용한다. 추가 의존성 없이 검사하며 실제 저장소에서 작업 흐름 쓰기를 실행하지 않는다.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ScratchRoot)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $source 'Scripts/Lib/common.ps1')
if (Test-PathInside -Child $ScratchRoot -Parent $source) { throw 'ScratchRoot must be outside the repository.' }
$fixture = Join-Path $ScratchRoot ('workspace-flow-' + [guid]::NewGuid().ToString('N'))
$repoPath = Join-Path $fixture 'checkout'
$origin = Join-Path $fixture 'origin.git'
New-Item -ItemType Directory -Path $repoPath -Force | Out-Null
$script:RepoRoot = $repoPath
$script:Passed = 0
$script:Git = (Get-Command git -CommandType Application | Select-Object -First 1).Source
$script:Pwsh = [System.Environment]::ProcessPath
function FixtureGit {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    $out = @(& $script:Git -C $repoPath @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "fixture git failed: $($Arguments -join ' ')`n$($out -join "`n")" }
    return ,$out
}
function Put {
    param([string]$Path, [string]$Text)
    $full = Join-Path $repoPath $Path
    New-Item -ItemType Directory -Path (Split-Path $full) -Force | Out-Null
    Set-Content -LiteralPath $full -Value $Text -Encoding utf8NoBOM
}
function Commit {
    param([string]$Message = 'fixture')
    FixtureGit add --all | Out-Null
    FixtureGit commit -m $Message | Out-Null
    return (FixtureGit rev-parse HEAD)[0]
}
function Check {
    param([string]$Name, [scriptblock]$Action)
    & $Action
    $script:Passed++
    Write-Output "PASS $Name"
}
function Reject {
    param([scriptblock]$Action, [string]$Pattern)
    try { & $Action; throw 'EXPECTED_REJECTION_MISSING' } catch {
        if ($_.Exception.Message -eq 'EXPECTED_REJECTION_MISSING' -or $_.Exception.Message -notmatch $Pattern) { throw }
    }
}
function Assert {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}
function Run {
    param([string]$Script, [string[]]$Arguments, [string]$RejectPattern)
    $out = @(& $script:Pwsh -NoProfile -File (Join-Path $repoPath "Scripts/$Script") @Arguments 2>&1)
    $code = $LASTEXITCODE
    $text = $out -join "`n"
    if ($RejectPattern) {
        Assert ($code -ne 0 -and $text -match "(?s)$RejectPattern") "script should reject: $Script / $RejectPattern / $text"
    } else { Assert ($code -eq 0) "script failed: $Script / $text" }
    return $text
}
function Integration {
    param([string]$Slug, [string]$Base = 'dev', [string]$Active = '')
    Put "Integrations/Active/Integration-$Slug/meta.md" "# Integration-$Slug`n`n- **Branch:** integration/$Slug`n- **Base:** $Base`n- **Active Task:** $Active`n`n## Tasks`n`n| Task | 종류 | 담당 | 브랜치 | PR | 상태 |`n|---|---|---|---|---|---|`n`n## 준비 브랜치`n`n| Task | 브랜치 | 준비 기준 SHA | 승인 SHA |`n|---|---|---|---|"
}
$id = 'Task-20261015-001'
$previous = 'Task-20261015-002'
$taskRel = "Tasks/Active/$id-stage-one"
function TaskMeta {
    param([string]$Status = 'scaffolded', [string]$Previous = '', [string]$TaskId = $id, [string]$Branch = 'feat/stage-one')
    Put "Tasks/Active/$TaskId-$(if ($TaskId -eq $id) { 'stage-one' } else { 'stage-two' })/meta.md" "# $TaskId`n`n- **Title:** fixture`n- **Type:** feat`n- **Status:** $Status`n- **Branch:** $Branch`n- **Integration:** integration/child`n- **Assignee:** @fixture`n- **Previous Task:** $Previous`n- **Current Skill:** scaffold-task`n- **Updated:** 2026-10-15"
}
function Setup {
    param([string]$Rel = $taskRel, [string]$Allowed = 'Scripts/Stage/', [string]$Jira = 'https://fixture.atlassian.net/browse/CC-124')
    $area = if ($Allowed) { '- code: `' + $Allowed + '`' } else { '- none' }
    Put "$Rel/setup.md" "# Setup`n`n## JIRA`n`n- 관련 JIRA: $Jira`n`n## 작업 구역`n`n$area`n`n## 예약 구역`n`n- future: ``Scripts/Reserved/```n`n## 배정된 공용 파일`n`n- 없음`n`n## 수정 금지`n`n- ``Assets/```n`n## 인계 전 체크 (PM)`n`n- [x] fixture ready"
}
try {
    Put 'Scripts/error-output.ps1' @'
param([ValidateSet('wrapped', 'other', 'success')][string]$Scenario)
if ($Scenario -eq 'other') {
    [Console]::Error.WriteLine('unrelated fixture error')
    exit 1
}
[Console]::Error.WriteLine("Update`n$([char]27)[31m금지$([char]27)[0m")
if ($Scenario -eq 'success') { exit 0 }
exit 1
'@
    Check '예상 오류는 줄바꿈과 ANSI를 포함해도 인정' {
        $out = Run error-output.ps1 @('-Scenario', 'wrapped') 'Update.*금지'
        Assert ($out -match 'Update\r?\n\x1B\[31m금지') 'fixture lost forced newline or ANSI'
    }
    Check '다른 오류는 예상 거부로 인정하지 않음' {
        Reject { Run error-output.ps1 @('-Scenario', 'other') 'Update.*금지' | Out-Null } 'script should reject'
    }
    Check '성공 종료는 예상 문구가 있어도 거부' {
        Reject { Run error-output.ps1 @('-Scenario', 'success') 'Update.*금지' | Out-Null } 'script should reject'
    }
    FixtureGit init -b dev | Out-Null
    # 작성자는 새로 만든 fixture 저장소에만 설정한다.
    FixtureGit config --local user.name 'Workspace Fixture' | Out-Null
    FixtureGit config --local user.email 'fixture@example.invalid' | Out-Null
    FixtureGit config --local core.longpaths true | Out-Null
    & $script:Git init --bare $origin | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'bare fixture init failed' }
    FixtureGit remote add origin $origin | Out-Null
    foreach ($name in 'Lib/common.ps1', 'new-integration.ps1', 'new-pr.ps1', 'assign-task.ps1', 'start-work.ps1', 'merge-integration.ps1') {
        Put "Scripts/$name" (Get-Content -LiteralPath (Join-Path $source "Scripts/$name") -Raw)
    }
    Put 'Harness/Core/Templates/Integration/meta.md' (Get-Content -LiteralPath (Join-Path $source 'Harness/Core/Templates/Integration/meta.md') -Raw)
    Put '.github/PULL_REQUEST_TEMPLATE.md' "## 요약`n`n## 관련 JIRA`n`n## 검사`n`n- 구역:`n- 컴파일:`n- 테스트:`n- 플레이:"
    $mock = @'
function Assert-CommitIdentity { } # fixture 전용
function Get-CurrentGitHubUser { return 'fixture' }
function Get-PmAccount { return 'fixture' }
function Get-GitHubRepo { return [pscustomobject]@{ Slug = 'fixture/workspace'; Owner = 'fixture'; Name = 'workspace' } }
function Get-GhState { return [pscustomobject]@{ State = 'Ready'; Gh = (Join-Path $script:RepoRoot 'Scripts/fake-gh.ps1'); Detail = 'fixture mock' } }
function Invoke-SafeBranchTransition {
    param([string]$Path = $script:RepoRoot, [Parameter(Mandatory)][string]$TargetCommit, [string]$Branch, [switch]$CreateBranch, [switch]$Track)
    # private/LFS는 별도 회귀가 담당한다. 여기서는 호출 계약과 Task 쓰기 순서만 검증한다.
    if ($Branch) {
        if ($CreateBranch) { & git -C $Path -c submodule.recurse=false switch -c $Branch $TargetCommit }
        elseif ($Track) { & git -C $Path -c submodule.recurse=false switch --track "origin/$Branch" }
        else { & git -C $Path -c submodule.recurse=false switch $Branch }
        if ($LASTEXITCODE -ne 0) { throw 'fixture transition switch failed' }
    }
    & git -C $Path -c submodule.recurse=false merge --ff-only --quiet $TargetCommit
    if ($LASTEXITCODE -ne 0) { throw 'fixture transition ff failed' }
    Add-Content -LiteralPath (Join-Path $Path 'thirdparty-called') -Value 'called'
    if ($env:WORKSPACE_TRANSITION_FAIL -eq '1') { throw 'fixture transition verification failed' }
}
'@
    Add-Content -LiteralPath (Join-Path $repoPath 'Scripts/Lib/common.ps1') -Value $mock
    Put 'Scripts/fake-gh.ps1' @'
param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
Add-Content -LiteralPath $env:WORKSPACE_GH_LOG -Value ($Arguments -join ' ')
$global:LASTEXITCODE = 0
switch ($Arguments[1]) {
    'view' {
        if ($env:WORKSPACE_PR_VIEW_FAIL -eq '1') { $global:LASTEXITCODE = 1; return '{}' }
        $base = $env:WORKSPACE_PR_BASE ? $env:WORKSPACE_PR_BASE : 'integration/child'
        $state = $env:WORKSPACE_PR_STATE ? $env:WORKSPACE_PR_STATE : 'OPEN'
        return "{`"baseRefName`":`"$base`",`"headRefName`":`"$env:WORKSPACE_PR_HEAD`",`"headRefOid`":`"$env:WORKSPACE_PR_SHA`",`"state`":`"$state`",`"mergeStateStatus`":`"CLEAN`",`"title`":`"fixture`"}"
    }
    'list' {
        if ($Arguments -contains '--jq') { return '' }
        if ($env:WORKSPACE_PR_LIST -and $Arguments -notcontains 'merged') { return $env:WORKSPACE_PR_LIST }
        return '[]'
    }
    'create' { return 'https://github.com/fixture/workspace/pull/1' }
    'merge' { return '' }
    'update-branch' { return '' } # 호출 기록은 공통 로그에 남긴다.
    default { throw 'unexpected gh mock call' }
}
'@
    $env:WORKSPACE_GH_LOG = Join-Path $fixture 'gh.log'
    $dev = Commit 'fixture harness'
    FixtureGit push -u origin dev | Out-Null
    FixtureGit switch -c integration/parent | Out-Null
    Integration parent
    Commit 'parent' | Out-Null
    FixtureGit push -u origin integration/parent | Out-Null
    FixtureGit switch -c integration/child | Out-Null
    Integration child integration/parent $id
    TaskMeta
    Setup
    Put "$taskRel/plan.md" "# Plan`n`n## 공동 테스트 항목`n`n1. fixture collaboration"
    Put "$taskRel/todo.md" (Get-Content -LiteralPath (Join-Path $source 'Harness/Core/Templates/Task/todo.md') -Raw)
    Put "$taskRel/handoff.md" "# Handoff`n`n## Verification`n`n- 5 공동 테스트: 미실행"
    Put 'Scripts/Stage/.gitkeep' ''
    $basis = Commit 'approved preparation'
    FixtureGit push -u origin integration/child | Out-Null
    FixtureGit branch feat/stage-one $basis | Out-Null
    FixtureGit push origin feat/stage-one | Out-Null
    $childMeta = Join-Path $repoPath 'Integrations/Active/Integration-child/meta.md'
    Add-Content -LiteralPath $childMeta -Value "| $id | feat/stage-one | $basis | $basis |"
    Commit 'preparation registry' | Out-Null
    FixtureGit push origin integration/child | Out-Null
    FixtureGit fetch origin --quiet | Out-Null
    Check 'remote parent chain resolves' { Assert-IntegrationLineage integration/child }
    Check 'new child starts at fresh remote parent tip and records Base' {
        FixtureGit switch integration/parent | Out-Null
        Put 'parent-tip.txt' 'latest'
        $tip = Commit 'latest parent'
        FixtureGit push origin integration/parent | Out-Null
        Run new-integration.ps1 @('-Slug','new-child','-Feature','fixture','-Base','integration/parent','-NoPush') | Out-Null
        Assert ((FixtureGit rev-parse HEAD^)[0] -eq $tip) 'child did not start at latest parent tip'
        Assert ((Get-MetaField -MetaPath (Join-Path $repoPath 'Integrations/Active/Integration-new-child/meta.md') -Field Base) -eq 'integration/parent') 'Base was not recorded'
        Assert (Test-Path (Join-Path $repoPath 'thirdparty-called')) 'new integration did not call transition mock'
        Remove-Item -LiteralPath (Join-Path $repoPath 'thirdparty-called')
        FixtureGit switch integration/child | Out-Null
    }
    Check 'missing parent metadata blocks' {
        FixtureGit branch integration/missing $dev | Out-Null
        FixtureGit push origin integration/missing | Out-Null
        Reject { Assert-IntegrationLineage integration/missing } '문서를 읽지'
    }
    Check 'mismatched parent Branch blocks' {
        FixtureGit switch -c integration/mismatch | Out-Null
        Integration mismatch
        Set-MetaField -MetaPath (Join-Path $repoPath 'Integrations/Active/Integration-mismatch/meta.md') -Field Branch -Value integration/wrong
        Commit | Out-Null
        FixtureGit push origin integration/mismatch | Out-Null
        Reject { Assert-IntegrationLineage integration/mismatch } '불일치'
        FixtureGit switch integration/child | Out-Null
    }
    Check 'self reference and cycles block' {
        FixtureGit switch -c integration/cycle-a | Out-Null
        Integration cycle-a integration/cycle-b
        Integration cycle-b integration/cycle-a
        Commit | Out-Null
        FixtureGit branch integration/cycle-b | Out-Null
        FixtureGit push origin integration/cycle-a integration/cycle-b | Out-Null
        Reject { Assert-IntegrationLineage integration/cycle-a } '순환'
        Reject { Assert-IntegrationLineage integration/child -Child integration/parent } '순환'
        Run new-integration.ps1 @('-Slug','self','-Feature','fixture','-Base','integration/self','-WhatIf') '자기참조' | Out-Null
        FixtureGit switch integration/child | Out-Null
    }
    Check 'PR child to parent and parent to child use merge' {
        Assert ((Get-PrFlow integration/child integration/parent) -eq 'merge') 'child flow'
        FixtureGit switch integration/parent | Out-Null
        Assert ((Get-PrFlow integration/parent integration/child) -eq 'merge') 'parent sync'
        Run new-pr.ps1 @('-Summary','fixture','-Title','sync','-Base','integration/child','-DryRun') | Out-Null
        FixtureGit switch integration/child | Out-Null
    }
    Check 'PR child dev shortcut and unrelated integrations block' {
        Reject { Get-PrFlow integration/child dev } '선언된'
        Reject { Get-PrFlow integration/child integration/mismatch } '불일치'
        Reject { Get-PrFlow integration/child integration/new-child } '문서를 읽지|선언된'
        Run new-pr.ps1 @('-Summary','fixture','-Title','invalid','-Base','dev','-DryRun') '선언된' | Out-Null
        FixtureGit switch dev | Out-Null
        Reject { Get-PrFlow dev integration/child } 'Base가 dev'
        Assert ((Get-PrFlow dev integration/parent) -eq 'merge') 'legacy dev sync'
        FixtureGit switch integration/child | Out-Null
    }
    Check 'closed integration PR reads Done meta and only targets Base' {
        FixtureGit switch dev | Out-Null
        FixtureGit switch -c integration/closed | Out-Null
        Integration closed
        Commit | Out-Null
        # close-integration처럼 통합 기록을 Done으로 옮긴 뒤 dev로 올린다.
        New-Item -ItemType Directory -Path (Join-Path $repoPath 'Integrations/Done') -Force | Out-Null
        FixtureGit mv Integrations/Active/Integration-closed Integrations/Done/Integration-closed | Out-Null
        Commit 'close integration' | Out-Null
        FixtureGit push -u origin integration/closed | Out-Null
        Assert ((Get-PrFlow integration/closed dev) -eq 'merge') 'closed integration to base'
        Reject { Get-PrFlow integration/closed integration/parent } '마감된 통합'
        Reject { Get-IntegrationMetadata integration/closed -Ref HEAD } '문서를 읽지'
        Run new-pr.ps1 @('-Summary','fixture','-Title','close','-Base','dev','-DryRun') | Out-Null
        Run new-pr.ps1 @('-Summary','fixture','-Title','close','-DryRun') | Out-Null
        FixtureGit switch integration/child | Out-Null
    }
    Check 'prepared branch identity, SHAs and sync direction pass' {
        Assert-PreparedBranch integration/child feat/stage-one $id feat
        Assert ((Get-PrFlow integration/child feat/stage-one) -eq 'merge') 'prepared sync'
        Run new-pr.ps1 @('-Summary','fixture','-Title','sync','-Base','feat/stage-one','-DryRun') | Out-Null
        Reject { Assert-PreparedBranch integration/child feat/stage-one $id fix } '불일치'
        $bad = (Get-Content -LiteralPath $childMeta -Raw).Replace($basis, ('0' * 40))
        Reject { Assert-PreparedBranch integration/child feat/stage-one $id -MetadataText $bad } '승인된'
    }
    Check 'pure parent sync merge stays an approved prepared branch' {
        FixtureGit switch feat/stage-one | Out-Null
        FixtureGit merge --no-ff integration/child -m 'parent preparation sync' | Out-Null
        FixtureGit push origin feat/stage-one | Out-Null
        FixtureGit switch integration/child | Out-Null
        Assert-PreparedBranch integration/child feat/stage-one $id feat
    }
    Check 'independent prepared implementation commit blocks' {
        FixtureGit switch feat/stage-one | Out-Null
        Put 'independent.txt' 'implementation'
        Commit | Out-Null
        # 원격은 그대로지만 로컬에 독립 커밋이 있다. 배정은 양쪽 이력을 모두 검사해야 한다.
        Reject { Assert-PreparedBranch integration/child feat/stage-one $id -BranchRef refs/heads/feat/stage-one } '독립 구현'
        FixtureGit switch integration/child | Out-Null
        Run assign-task.ps1 @('-Id',$id,'-UsePreparedBranch','-WhatIf') '독립 구현' | Out-Null
        # 거부된 브랜치는 보존하고 승인된 원격 브랜치에서 새 로컬 fixture 브랜치를 만든다.
        FixtureGit branch -m feat/stage-one feat/rejected-implementation | Out-Null
    }
    Check 'scaffolded start blocks before switch or thirdparty' {
        $head = (FixtureGit rev-parse HEAD)[0]
        Run start-work.ps1 @('-Id',$id) 'assigned/working' | Out-Null
        Assert ((FixtureGit rev-parse HEAD)[0] -eq $head) 'start changed branch before guard'
        Assert (-not (Test-Path (Join-Path $repoPath 'thirdparty-called'))) 'thirdparty ran before guard'
    }
    Check 'reserved paths do not grant Allowed' {
        $areas = Get-SetupAreas -SetupPath (Join-Path $repoPath "$taskRel/setup.md")
        Assert ($areas.Allowed -contains 'Scripts/Stage/' -and $areas.Allowed -notcontains 'Scripts/Reserved/') 'reserved paths granted'
    }
    Check 'single active and real Jira gates block' {
        $text = Get-Content -LiteralPath $childMeta -Raw
        Reject { Assert-TaskAssignmentGate $previous (Join-Path $repoPath $taskRel) $text } 'Active Task'
        foreach ($jira in '없음', '미정', 'CC-124', 'https://.../browse/CC-124') {
            Setup -Jira $jira
            Reject { Assert-TaskAssignmentGate $id (Join-Path $repoPath $taskRel) $text } '실제'
        }
        Setup
        TaskMeta -Status assigned -TaskId $previous -Branch feat/stage-two
        Setup -Rel "Tasks/Active/$previous-stage-two" -Allowed 'Scripts/Other/'
        Reject { Assert-TaskAssignmentGate $id (Join-Path $repoPath $taskRel) $text } '다른 활성'
        TaskMeta -TaskId $previous -Branch feat/stage-two
        Setup -Rel "Tasks/Active/$previous-stage-two" -Allowed 'Scripts/Stage/Nested/'
        Reject { Assert-TaskAssignmentGate $id (Join-Path $repoPath $taskRel) $text } '소유권 충돌'
    }
    Check 'previous integrated with real joint evidence and ownership transfer required' {
        $text = Get-Content -LiteralPath $childMeta -Raw
        TaskMeta -Previous $previous
        Reject { Assert-TaskAssignmentGate $id (Join-Path $repoPath $taskRel) $text } 'integrated'
        TaskMeta -Status integrated -TaskId $previous -Branch feat/stage-two
        Put "Tasks/Active/$previous-stage-two/handoff.md" "# Handoff`n`n## Verification`n`n- 5 공동 테스트: PASS"
        Reject { Assert-TaskAssignmentGate $id (Join-Path $repoPath $taskRel) $text } '근거'
        Put "Tasks/Active/$previous-stage-two/handoff.md" "# Handoff`n`n## Verification`n`n- 5 공동 테스트: PASS — 2026-10-15 공동 재현`n`n## 공동 테스트 결과`n`n- 일시: 2026-10-15 15:00`n- 참여자: PM + worker`n- 재현: fixture scene, host + client, launch`n- 예상: shared launch`n- 실제: shared launch confirmed"
        Reject { Assert-TaskAssignmentGate $id (Join-Path $repoPath $taskRel) $text } '소유권 충돌'
        $handoffPath = Join-Path $repoPath "Tasks/Active/$previous-stage-two/handoff.md"
        $evidenceText = Get-Content -LiteralPath $handoffPath -Raw
        Set-Content -LiteralPath $handoffPath -Value ($evidenceText.Replace('- 실제: shared launch confirmed', '- 실제:')) -Encoding utf8NoBOM
        Reject { Assert-TaskAssignmentGate $id (Join-Path $repoPath $taskRel) $text } '실제 근거'
        Set-Content -LiteralPath $handoffPath -Value $evidenceText -Encoding utf8NoBOM
        Setup -Rel "Tasks/Active/$previous-stage-two" -Allowed ''
        Assert-TaskAssignmentGate $id (Join-Path $repoPath $taskRel) $text
        Commit 'ownership transfer' | Out-Null
        FixtureGit push origin integration/child | Out-Null
    }
    Check 'assignment prepares sync PR without moving branch or confirming handoff' {
        $before = (FixtureGit rev-parse refs/remotes/origin/feat/stage-one)[0]
        $out = Run assign-task.ps1 @('-Id',$id,'-UsePreparedBranch')
        Assert ($out -match '기술 배정 준비 완료 · 실제 전달 미확인') 'assignment claims delivery'
        Assert ((FixtureGit rev-parse refs/remotes/origin/feat/stage-one)[0] -eq $before) 'prepared branch moved'
        $todo = Get-Content -LiteralPath (Join-Path $repoPath "$taskRel/todo.md") -Raw
        Assert ($todo -match '\[x\] 인계 전 기술 점검 완료' -and $todo -match '\[ \] 담당자에게 실제 전달 — PM 확인') 'wrong todo checked'
        Assert ((Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw) -match 'pr create.*--base feat/stage-one') 'sync PR not created'
        Assert ((Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw) -notmatch 'pr merge') 'assignment auto merged'
    }
    Check 'assigned but unreceived branch start rejects' {
        Run start-work.ps1 @('-Id',$id) 'assigned/working' | Out-Null
        # assigned 상태만 받고 setup/Previous Task는 받지 않은 경우에도 거부해야 한다.
        FixtureGit switch -c feat/stage-one --track origin/feat/stage-one | Out-Null
        TaskMeta assigned
        Commit 'partial assignment documents' | Out-Null
        FixtureGit push origin feat/stage-one | Out-Null
        FixtureGit switch integration/child | Out-Null
        Run start-work.ps1 @('-Id',$id) '미수신' | Out-Null
    }
    Check 'full sync merge is allowed, unchecked actual handoff start blocks' {
        FixtureGit switch feat/stage-one | Out-Null
        FixtureGit merge --no-ff integration/child -m 'receive fixture assignment' | Out-Null
        FixtureGit push origin feat/stage-one | Out-Null
        FixtureGit switch integration/child | Out-Null
        # 앞서 만든 부분 문서의 독립 커밋은 승인된 준비 이력이 아니다.
        Reject { Assert-PreparedBranch integration/child feat/stage-one $id } '독립 구현'
        Run start-work.ps1 @('-Id',$id) '실제 전달' | Out-Null
        Assert (-not (Test-Path (Join-Path $repoPath 'thirdparty-called'))) 'blocked start updated thirdparty'
    }
    Check 'remote Active Task mismatch blocks even with received docs' {
        Set-MetaField -MetaPath $childMeta -Field 'Active Task' -Value $previous
        Commit 'next active' | Out-Null
        FixtureGit push origin integration/child | Out-Null
        Run start-work.ps1 @('-Id',$id) 'Active Task 불일치' | Out-Null
        Set-MetaField -MetaPath $childMeta -Field 'Active Task' -Value $id
        Commit 'restore active' | Out-Null
        FixtureGit push origin integration/child | Out-Null
    }
    Check 'actual PM handoff + received docs permit start' {
        [void](Set-TodoChecked -TodoPath (Join-Path $repoPath "$taskRel/todo.md") -Text '담당자에게 실제 전달 — PM 확인')
        Commit 'PM confirms actual delivery' | Out-Null
        FixtureGit push origin integration/child | Out-Null
        FixtureGit switch feat/stage-one | Out-Null
        FixtureGit merge --no-ff integration/child -m 'receive actual delivery confirmation' | Out-Null
        FixtureGit push origin feat/stage-one | Out-Null
        FixtureGit switch integration/child | Out-Null
        Check 'transition verification failure leaves Task documents untouched' {
            $metaBefore = (FixtureGit show "refs/heads/feat/stage-one:$taskRel/meta.md") -join "`n"
            $todoBefore = (FixtureGit show "refs/heads/feat/stage-one:$taskRel/todo.md") -join "`n"
            $env:WORKSPACE_TRANSITION_FAIL = '1'
            try { Run start-work.ps1 @('-Id',$id) 'fixture transition verification failed' | Out-Null }
            finally { Remove-Item Env:WORKSPACE_TRANSITION_FAIL -ErrorAction SilentlyContinue }
            Assert ((FixtureGit branch --show-current)[0] -eq 'feat/stage-one') 'failure mock did not exercise post-switch verification'
            Assert (((Get-Content -LiteralPath (Join-Path $repoPath "$taskRel/meta.md")) -join "`n") -ceq $metaBefore) 'failed transition changed Task meta'
            Assert (((Get-Content -LiteralPath (Join-Path $repoPath "$taskRel/todo.md")) -join "`n") -ceq $todoBefore) 'failed transition changed Task todo'
            Assert (Test-Path (Join-Path $repoPath 'thirdparty-called')) 'failure did not reach transition mock'
            Remove-Item -LiteralPath (Join-Path $repoPath 'thirdparty-called')
            $remainingChanges = FixtureGit status --porcelain
            Assert ($remainingChanges.Count -eq 0) 'failed transition changed tracked fixture state'
            FixtureGit switch integration/child | Out-Null
        }
        Run start-work.ps1 @('-Id',$id) | Out-Null
        Assert ((FixtureGit branch --show-current)[0] -eq 'feat/stage-one') 'start did not switch'
        Assert ((Get-MetaField -MetaPath (Join-Path $repoPath "$taskRel/meta.md") -Field Status) -eq 'working') 'start did not update working'
        Assert (Test-Path (Join-Path $repoPath 'thirdparty-called')) 'allowed start did not call thirdparty mock'
        Remove-Item -LiteralPath (Join-Path $repoPath 'thirdparty-called')
        Commit 'working state' | Out-Null
    }
    Check 'legacy integration with empty/absent Active Task retains start behavior' {
        FixtureGit switch integration/child | Out-Null
        Set-MetaField -MetaPath $childMeta -Field 'Active Task' -Value ''
        Commit 'disable gate' | Out-Null
        FixtureGit push origin integration/child | Out-Null
        Assert-TaskStartGate (Get-RefTask -Ref refs/remotes/origin/feat/stage-one -Id $id)
        $noActive = [regex]::Replace((Get-Content -LiteralPath $childMeta -Raw), '(?m)^- \*\*Active Task:\*\*[^\r\n]*\r?\n', '')
        Set-Content -LiteralPath $childMeta -Value $noActive -Encoding utf8NoBOM
        Commit 'absent optional gate' | Out-Null
        FixtureGit push origin integration/child | Out-Null
        Assert-TaskStartGate (Get-RefTask -Ref refs/remotes/origin/feat/stage-one -Id $id)
        $task = Get-RefTask -Ref refs/remotes/origin/feat/stage-one -Id $id
        $task.Integration = 'dev'
        Assert-TaskStartGate $task
        $task.Integration = 'integration/child'
        $task.Status = 'submitted'
        Reject { Assert-TaskStartGate $task } 'assigned/working'
        $task.Status = 'integrated'
        Reject { Assert-TaskStartGate $task } 'assigned/working'
    }
    Check 'legacy combined delivery checkbox is never automatically checked' {
        Put 'legacy-todo.md' '- [ ] 인계 전 체크 완료, 담당자에게 전달'
        Assert (-not (Set-TodoChecked -TodoPath (Join-Path $repoPath 'legacy-todo.md') -Text '인계 전 기술 점검 완료')) 'legacy combined checkbox checked'
        Remove-Item -LiteralPath (Join-Path $repoPath 'legacy-todo.md')
    }
    Check 'actual PR dev/integration heads cannot be Squashed' {
        $env:WORKSPACE_PR_SHA = (FixtureGit rev-parse HEAD)[0]
        foreach ($head in 'dev', 'integration/parent') {
            $env:WORKSPACE_PR_HEAD = $head
            Run merge-integration.ps1 @('-Slug','child','-Merge','1') 'Squash 금지' | Out-Null
        }
        Assert ((Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw) -notmatch 'pr merge') 'blocked squash invoked gh merge'
        $env:WORKSPACE_PR_HEAD = 'feat/stage-one'
        Run merge-integration.ps1 @('-Slug','child','-Merge','1') | Out-Null
        Assert ((Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw) -match 'pr merge 1.*--squash.*--match-head-commit') 'work PR lost squash/head guard'
    }
    Check 'Update accepts actual open work heads on the requested base' {
        foreach ($head in 'feat/stage-one', 'fix/stage-one', 'refactor/stage-one', 'resource/stage-one') {
            $env:WORKSPACE_PR_HEAD = $head
            $before = Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw
            Run merge-integration.ps1 @('-Slug','child','-Update','11') | Out-Null
            $calls = (Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw).Substring($before.Length)
            Assert ($calls -match '(?s)pr view 11.*--json baseRefName[, ]headRefName[, ]state.*pr update-branch 11') 'Update did not validate actual PR before writing'
            Assert ($calls -notmatch 'pr merge') 'Update unexpectedly merged'
        }
    }
    Check 'Update rejects parent and non-work heads without writing' {
        foreach ($head in 'dev', 'integration/parent', 'builds/test', 'tests/test', 'Feat/stage-one') {
            $env:WORKSPACE_PR_HEAD = $head
            $before = Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw
            Run merge-integration.ps1 @('-Slug','child','-Update','12') 'Update.*금지' | Out-Null
            $calls = (Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw).Substring($before.Length)
            Assert ($calls -notmatch 'pr update-branch|pr merge') 'blocked head triggered a write'
        }
        $env:WORKSPACE_PR_HEAD = 'feat/stage-one'
    }
    Check 'Update rejects another actual base without writing' {
        $env:WORKSPACE_PR_BASE = 'integration/other'
        $before = Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw
        try { Run merge-integration.ps1 @('-Slug','child','-Update','13') '대상이.*가 아님' | Out-Null }
        finally { Remove-Item Env:WORKSPACE_PR_BASE }
        $calls = (Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw).Substring($before.Length)
        Assert ($calls -notmatch 'pr update-branch|pr merge') 'wrong base triggered a write'
    }
    Check 'Update rejects closed or merged PRs without writing' {
        foreach ($state in 'CLOSED', 'MERGED') {
            $env:WORKSPACE_PR_STATE = $state
            $before = Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw
            try { Run merge-integration.ps1 @('-Slug','child','-Update','14') '열려 있지 않음' | Out-Null }
            finally { Remove-Item Env:WORKSPACE_PR_STATE }
            $calls = (Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw).Substring($before.Length)
            Assert ($calls -notmatch 'pr update-branch|pr merge') 'closed PR triggered a write'
        }
    }
    Check 'Update rejects failed PR lookup without writing' {
        $env:WORKSPACE_PR_VIEW_FAIL = '1'
        $before = Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw
        try { Run merge-integration.ps1 @('-Slug','child','-Update','15') '조회 실패' | Out-Null }
        finally { Remove-Item Env:WORKSPACE_PR_VIEW_FAIL }
        $calls = (Get-Content -LiteralPath $env:WORKSPACE_GH_LOG -Raw).Substring($before.Length)
        Assert ($calls -notmatch 'pr update-branch|pr merge') 'failed lookup triggered a write'
    }
    # 일반 작업만 최신화를 안내하고 부모·통합 PR에는 역유입 위험 동작을 권하지 않는다.
    $behindPrs = '[{"number":21,"title":"work","headRefName":"feat/stage-one","state":"OPEN","mergeStateStatus":"BEHIND","isDraft":false},{"number":22,"title":"parent sync","headRefName":"integration/parent","state":"OPEN","mergeStateStatus":"BEHIND","isDraft":false},{"number":23,"title":"dev sync","headRefName":"dev","state":"OPEN","mergeStateStatus":"DIRTY","isDraft":false}]'
    Check 'PR listing never recommends Update for sync heads' {
        $env:WORKSPACE_PR_LIST = $behindPrs
        try { $out = Run merge-integration.ps1 @('-Slug','child') }
        finally { Remove-Item Env:WORKSPACE_PR_LIST }
        Assert ($out -match '#21[^\r\n]*Update 필요') 'work PR lost Update guidance'
        foreach ($number in 22, 23) {
            Assert ($out -match "#$number[^\r\n]*Update 금지" -and $out -notmatch "#$number[^\r\n]*Update 필요") 'listing recommends Update for sync PR'
        }
    }
    Check 'post-merge guidance only recommends Update for work heads' {
        $env:WORKSPACE_PR_LIST = $behindPrs
        try { $out = Run merge-integration.ps1 @('-Slug','child','-Merge','1') }
        finally { Remove-Item Env:WORKSPACE_PR_LIST }
        Assert ($out -match '#21[^\r\n]*-Update 21') 'work PR lost post-merge Update guidance'
        foreach ($number in 22, 23) {
            Assert ($out -match "#$number[^\r\n]*Update 금지" -and $out -notmatch "-Update $number") 'post-merge recommends Update for sync PR'
        }
    }
    Write-Output "PASS workspace-flow: $script:Passed checks; fixture: $fixture"
} finally {
    # 원인 확인을 위해 fixture는 전달받은 세션 scratchpad에 보존한다.
    Remove-Item Env:WORKSPACE_GH_LOG, Env:WORKSPACE_PR_HEAD, Env:WORKSPACE_PR_SHA, Env:WORKSPACE_PR_BASE, Env:WORKSPACE_PR_STATE, Env:WORKSPACE_PR_VIEW_FAIL, Env:WORKSPACE_PR_LIST -ErrorAction SilentlyContinue
}
