# 격리 저장소에서 3문서·착수·공유 방향·검사 선택·기록 보존을 검증한다. 실제 GitHub 쓰기 없음.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ScratchRoot)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $source 'Scripts/Lib/common.ps1')
if (Test-PathInside -Child $ScratchRoot -Parent $source) { throw '테스트 폴더는 저장소 밖에 두세요.' }
$fixture=Join-Path $ScratchRoot ('harness-flow-'+[guid]::NewGuid().ToString('N'))
$repo=Join-Path $fixture 'checkout'
$origin=Join-Path $fixture 'origin.git'
New-Item -ItemType Directory -Path $repo -Force | Out-Null
$git=(Get-Command git -CommandType Application | Select-Object -First 1).Source
$pwsh=[Environment]::ProcessPath
$script:passed=0
function FixtureGit {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Options)
    $out=@(& $git -C $repo @Options 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "fixture git $Options : $out" }
    return ($out -join "`n")
}
function Put([string]$path,[string]$text) {
    $full=Join-Path $repo $path
    New-Item -ItemType Directory -Path (Split-Path $full) -Force | Out-Null
    [IO.File]::WriteAllText($full,$text+"`n",[Text.UTF8Encoding]::new($false))
}
function Check([string]$name,[scriptblock]$action) { & $action; $script:passed++; Write-Output "PASS $name" }
function Assert([bool]$ok,[string]$why) { if (-not $ok) { throw $why } }
function Run([string]$name,[string[]]$options=@(),[int]$code=0) {
    $out=@(& $pwsh -NoProfile -File (Join-Path $repo "Scripts/$name") @options 2>&1)
    if ($LASTEXITCODE -ne $code) { throw "$name expected $code, got $LASTEXITCODE : $out" }
    return ($out -join "`n")
}
FixtureGit init -b dev | Out-Null
FixtureGit config user.name 'Harness Fixture' | Out-Null
FixtureGit config user.email 'fixture@example.invalid' | Out-Null
FixtureGit config core.autocrlf false | Out-Null
& $git init --bare $origin | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'fixture origin init failed' }
FixtureGit remote add origin $origin | Out-Null
foreach ($name in 'Lib/common.ps1','new-task.ps1','start-work.ps1','verify-scope.ps1','check-work.ps1','new-pr.ps1','close-integration.ps1','assign-task.ps1','sync-integration.ps1') {
    Put "Scripts/$name" (Get-Content -LiteralPath (Join-Path $source "Scripts/$name") -Raw)
}
foreach ($name in 'plan.md','todo.md','log.md') { Put "Harness/Core/Templates/Task/$name" (Get-Content -LiteralPath (Join-Path $source "Harness/Core/Templates/Task/$name") -Raw) }
Put '.github/PULL_REQUEST_TEMPLATE.md' (Get-Content -LiteralPath (Join-Path $source '.github/PULL_REQUEST_TEMPLATE.md') -Raw)
Put 'Scripts/verify-fast.ps1' "Write-Output 'Fast: OK (fixture)'`nexit 0"
Put 'Scripts/verify-unity.ps1' "throw 'unexpected Unity call'"
Put 'Docs/base.md' 'base'
Put '.gitignore' '.harness/'
FixtureGit add -A | Out-Null
FixtureGit commit -m baseline | Out-Null
FixtureGit push -u origin dev | Out-Null
$script:RepoRoot=$repo
Check '공유 순서 제한 없이 직접 dev·작업·다른 통합으로 공유' {
    foreach ($pair in @(@('feat/feature','dev'),@('refactor/tools','dev'),@('fix/bug','dev'),@('integration/a','integration/b'),@('dev','feat/feature'),@('feat/a','fix/b'))) {
        Assert ((Get-PrFlow -Head $pair[0] -Base $pair[1]) -in 'squash','merge') ($pair -join ' → ')
    }
    Assert ((Get-PrFlow -Head dev -Base builds/v1) -eq 'merge') 'dev build flow'
}
Check '자기 자신·tests 제출·build 직행은 거부' {
    foreach ($pair in @(@('feat/a','feat/a'),@('tests/a','dev'),@('feat/a','builds/v1'))) {
        $rejected=$false
        try { Get-PrFlow -Head $pair[0] -Base $pair[1] | Out-Null } catch { $rejected=$true }
        Assert $rejected 'invalid flow accepted'
    }
}
Check '미리보기는 Task를 만들지 않음' {
    Run new-task.ps1 @('-Type','feat','-Slug','sample','-Title','fixture','-Assignee','worker','-WhatIf') | Out-Null
    Assert (-not (Test-Path (Join-Path $repo 'Tasks/Active'))) 'preview mutated tasks'
}
Run new-task.ps1 @('-Type','feat','-Slug','sample','-Title','fixture','-Assignee','worker') | Out-Null
$task=Get-ChildItem (Join-Path $repo 'Tasks/Active') -Directory | Select-Object -First 1
$id=$task.Name -replace '^(Task-\d{8}-\d{3}).*','$1'
Check 'PM 인계 없이 필수 3문서만 생성' {
    Assert (((Get-ChildItem $task.FullName -File).Name | Sort-Object) -join ',' -eq 'log.md,plan.md,todo.md') 'not 3 documents'
    Assert ((Get-Content (Join-Path $task.FullName 'plan.md') -Raw) -match '\*\*Assignee:\*\* @worker') 'assignee lost'
}
Check '미추적 Task 3문서로 기준 dev에서 작업 브랜치 바로 시작' {
    Run start-work.ps1 @('-Id',$id) | Out-Null
    Assert ((FixtureGit branch --show-current) -ceq 'feat/sample') 'branch not created'
    Assert (Test-Path (Join-Path $task.FullName 'plan.md')) 'task lost'
}
Check '같은 브랜치는 미저장 변경을 보존하고 재개' {
    Put 'Docs/work.md' 'keep me'
    Run start-work.ps1 @('-Id',$id) | Out-Null
    Assert ((Get-Content (Join-Path $repo 'Docs/work.md') -Raw).Trim() -eq 'keep me') 'local work changed'
}
Check '예전 setup 금지 구역과 다른 기능 코드도 수정 가능' {
    Put 'Tasks/Active/Task-20000101-002-other/plan.md' '- **Branch:** feat/other'
    Put 'Tasks/Active/Task-20000101-002-other/setup.md' "## 작업 구역`n- ``Assets/Scripts/Other/``"
    Put 'Assets/Scripts/Other/fix.cs' 'class Fix {}'
    foreach ($meta in 'Assets/Scripts.meta','Assets/Scripts/Other.meta','Assets/Scripts/Other/fix.cs.meta') { Put $meta 'fileFormatVersion: 2' }
    $text=Run verify-scope.ps1 @('-Base','dev','-Actor','worker')
    Assert ($text -match 'Safety: OK') 'scope still blocked'
}
Check 'Unity meta 누락은 계속 검출' {
    $meta=Join-Path $repo 'Assets/Scripts/Other/fix.cs.meta'
    Remove-Item -LiteralPath $meta
    Assert ((Run verify-scope.ps1 @('-Base','dev') 1) -match '\.meta 없음') 'meta loss undetected'
    Put 'Assets/Scripts/Other/fix.cs.meta' 'fileFormatVersion: 2'
}
Check 'Unity 변경 생략은 컴파일 성공으로 처리하지 않음' {
    Assert ((Run check-work.ps1 @('-Base','dev','-SkipUnity','-NoRecord') 2) -match 'SKIPPED\(-SkipUnity\)') 'unity skip reported success'
}
# 변경을 기준 dev에 넣어 이후 문서 변경만 남긴다 (테스트 저장소만).
FixtureGit add -A | Out-Null
FixtureGit commit -m fixture-implementation | Out-Null
FixtureGit branch -f dev HEAD | Out-Null
Put 'Docs/work.md' 'document change only'
Check '문서 변경 검사는 Unity·자동 테스트를 실행하지 않고 log 기록' {
    $text=Run check-work.ps1 @('-Base','dev')
    Assert ($text -match 'SKIPPED\(Unity 변경 없음\)') 'unexpected Unity requirement'
    Assert ((Get-Content (Join-Path $task.FullName 'log.md') -Raw) -match '안전성=PASS') 'check not logged'
}
Check 'Task 조회와 ref 번호는 plan만으로 동작' {
    Assert ((Find-CurrentTask).Id -ceq $id) 'current task lookup failed'
    $refs=@(Get-TaskRefMetadata -Ref HEAD)
    Assert (@($refs | Where-Object Branch -CEQ 'feat/sample').Count -eq 1) 'plan ref lookup failed'
}
Check 'PR 미리보기·실제 제출은 plan과 최신 log를 읽고 Status를 만들지 않음' {
    # 외부 호출은 fixture의 PowerShell mock으로만 답한다.
    Put 'Scripts/mock-gh.ps1' @'
param([Parameter(ValueFromRemainingArguments)][string[]]$Options)
if (($Options -join ' ') -like 'pr list *') {
    if ($Options -notcontains '--jq') { Write-Output '[]' }
    exit 0
}
if (($Options -join ' ') -like 'pr create *') {
    $idx=[array]::IndexOf($Options,'--body-file')
    Copy-Item -LiteralPath $Options[$idx+1] -Destination (Join-Path $PSScriptRoot 'sent-body.txt')
    Write-Output 'https://github.com/fixture/repo/pull/1'
    exit 0
}
throw 'unexpected fixture gh command'
'@
    Add-Content -LiteralPath (Join-Path $repo 'Scripts/Lib/common.ps1') -Value @'

function Get-GitHubRepo { [pscustomobject]@{ Owner='fixture'; Name='repo'; Slug='fixture/repo' } }
function Get-GhState { [pscustomobject]@{ State='Ready'; Gh=(Join-Path $script:RepoRoot 'Scripts/mock-gh.ps1'); Detail='fixture' } }
'@
    FixtureGit add -A | Out-Null
    FixtureGit commit -m fixture-pr-ready | Out-Null
    FixtureGit push -u origin feat/sample | Out-Null
    $text=Run new-pr.ps1 @('-Summary','fixture change','-DryRun')
    Assert ($text -match '안전성: PASS' -and $text -match '컴파일: SKIPPED\(Unity 변경 없음\)') 'latest check missing in PR'
    Assert (-not (Test-Path (Join-Path $repo 'Scripts/sent-body.txt'))) 'preview submitted PR'
    Run new-pr.ps1 @('-Summary','fixture change') | Out-Null
    Assert ((Get-Content (Join-Path $task.FullName 'log.md') -Raw) -match '/pull/1') 'PR not logged'
    Assert (-not (Test-Path (Join-Path $task.FullName 'meta.md'))) 'Status document recreated'
}
Check '다른 통합끼리 부모·PM gate 없이 공유 PR 미리보기 가능' {
    foreach ($name in 'integration/one','integration/two') {
        FixtureGit branch $name HEAD | Out-Null
        FixtureGit push origin $name | Out-Null
    }
    $text=Run sync-integration.ps1 @('-Parent','integration/one','-Child','integration/two','-DryRun')
    Assert ($text -match 'WouldCreate') 'cross-integration sharing blocked'
}
Check '필수 3문서의 완료 이관은 명시 Task만 이동하고 미리보기는 보존' {
    Run close-integration.ps1 @('-Slug','optional','-CompletedTasks',$id,'-WhatIf') | Out-Null
    Assert (Test-Path $task.FullName) 'preview moved task'
    Run close-integration.ps1 @('-Slug','optional','-CompletedTasks',$id) | Out-Null
    Assert (Test-Path (Join-Path $repo "Tasks/Done/$($task.Name)/log.md")) 'completed task not preserved'
    Assert (Test-Path (Join-Path $repo 'Tasks/Active/Task-20000101-002-other/setup.md')) 'unselected task moved'
}
Check '자동 브랜치 삭제는 하지 않음' {
    Assert ((Run close-integration.ps1 @('-Slug','optional','-Cleanup') 1) -match '자동 브랜치 삭제') 'cleanup still enabled'
}
Check '소스의 진행 작업 원문 보관은 기존 HEAD와 바이트가 일치' {
    foreach ($dir in Get-ChildItem -LiteralPath (Join-Path $source 'Tasks/Active') -Directory) {
        $archive=Join-Path $dir.FullName 'History/Harness-v1'
        if (-not (Test-Path -LiteralPath $archive)) { continue }
        $currentPlan=Get-Content -LiteralPath (Join-Path $dir.FullName 'plan.md') -Raw
        Assert ($currentPlan -notmatch 'System\.Object\[\]') "migration summary malformed: $($dir.Name)"
        foreach ($old in 'meta.md','setup.md','handoff.md') {
            Assert (-not (Test-Path -LiteralPath (Join-Path $dir.FullName $old))) "old required document remains: $($dir.Name)/$old"
        }
        foreach ($name in 'meta.md','setup.md','handoff.md','plan.md','todo.md','log.md') {
            $archived=Join-Path $archive $name
            if (-not (Test-Path -LiteralPath $archived)) { continue }
            $rel="Tasks/Active/$($dir.Name)/$name"
            # 이관 커밋 전에는 원래 경로, 이관 후 CI에서는 보관 경로와 비교한다.
            $expected=@(& $git -C $source rev-parse --verify "HEAD:$rel" 2>$null)
            if ($LASTEXITCODE -ne 0 -or $name -in 'plan.md','todo.md','log.md') {
                $archiveRel="Tasks/Active/$($dir.Name)/History/Harness-v1/$name"
                $archivedHead=@(& $git -C $source rev-parse --verify "HEAD:$archiveRel" 2>$null)
                if ($LASTEXITCODE -eq 0) { $expected=$archivedHead }
            }
            if ($expected.Count -ne 1 -or $expected[0] -notmatch '^[a-f0-9]{40,64}$') { throw "archive baseline missing: $rel" }
            $actual=(& $git -C $source hash-object --path=$rel $archived).Trim()
            Assert ($expected[0].Trim() -ceq $actual) "archive content changed: $rel"
        }
    }
}
Write-Output "Harness flow: PASS ($script:passed cases). Fixture: $fixture"
