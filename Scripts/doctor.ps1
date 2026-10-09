# 작업 전 환경 점검. 1분 안에 끝나며 결과를 한 줄씩, 마지막에 요약 한 줄을 출력한다.
# FAIL이 있으면 exit 1. WARN은 작업 가능하지만 확인이 필요한 상태.
[CmdletBinding()]
param(
    # Unity 에디터 연결 확인을 생략한다(문서/스크립트 작업 시).
    [switch]$SkipEditor
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$results = [System.Collections.Generic.List[object]]::new()
function Add-Check {
    param([ValidateSet('OK', 'WARN', 'FAIL')][string]$Level, [string]$Name, [string]$Detail)
    $results.Add([pscustomobject]@{ Level = $Level; Name = $Name; Detail = $Detail })
    Write-Output ("[{0,-4}] {1,-18} {2}" -f $Level, $Name, $Detail)
}

# 1. PowerShell
if ($PSVersionTable.PSVersion.Major -ge 7) {
    Add-Check OK 'pwsh' $PSVersionTable.PSVersion.ToString()
} else {
    Add-Check FAIL 'pwsh' "PowerShell 7+ 필요 (현재 $($PSVersionTable.PSVersion))"
}

# 2. Git / 브랜치
$base = Get-BaseBranch
$branch = (& git -C $RepoRoot branch --show-current).Trim()
& git -C $RepoRoot rev-parse --verify --quiet $base | Out-Null
if ($LASTEXITCODE -ne 0) {
    Add-Check FAIL 'git' "기본 브랜치 '$base'를 찾을 수 없음"
} else {
    Add-Check OK 'git' "현재 '$branch', 기본 '$base'"
}
if (Test-ThirdPartyReady) {
    Add-Check OK 'third-party' 'Assets/_ThirdParty 받음'
} else {
    Add-Check FAIL 'third-party' "외부 에셋 submodule을 받지 않음 — git submodule update --init (권한 없으면 PM에게 CURTAINCALL_PaidAssets 읽기 권한 요청)"
}

# 3. 작업 공간 (브랜치 종류별로 필요한 문서가 있는지)
$allTasks = @(Get-AllTaskFolders)
switch -Regex ($branch) {
    '^(feat|fix|refactor)/' {
        $task = Find-CurrentTask
        if ($task) {
            $st = Get-MetaField -MetaPath (Join-Path $task.Path 'meta.md') -Field 'Status'
            Add-Check OK 'workspace' "$($task.Id) 작업 브랜치 (Status $st)"
        } elseif ($branch -like 'fix/*') {
            Add-Check WARN 'workspace' "Task 없는 fix 브랜치 — 급한 수정일 때만 허용. PR 본문에 이유와 범위를 적기"
        } else {
            Add-Check FAIL 'workspace' "브랜치 $branch 에 대응하는 Tasks/Active 폴더가 없음 (PM에게 확인)"
        }
    }
    '^integration/(.+)$' {
        if (Test-Path -LiteralPath (Get-RepoPath "Integrations/Active/Integration-$($Matches[1])")) {
            Add-Check OK 'workspace' "통합 브랜치 (PM 작업 공간)"
        } else {
            Add-Check WARN 'workspace' "통합 기록 폴더가 없음: Integrations/Active/Integration-$($Matches[1])"
        }
    }
    '^resource/' { Add-Check OK 'workspace' '리소스 브랜치(resource/*) — Task 없음, Assets/Resources/ 리소스만(외부 에셋은 Assets/_ThirdParty submodule)' }
    '^tests/' { Add-Check OK 'workspace' '연습 브랜치(tests/*) — 합쳐지지 않음' }
    '^builds/' { Add-Check OK 'workspace' '빌드 브랜치 (PM)' }
    default {
        if ($branch -eq $base) { Add-Check OK 'workspace' "공용 원본($base). 작업은 작업 브랜치에서 ('Task-○○○ 시작해줘')" }
        else { Add-Check WARN 'workspace' "브랜치 규칙에 없는 이름: $branch (Harness/Core/Policies/git-workflow.md)" }
    }
}

# 4. Task ID 중복
$dupes = @($allTasks | Group-Object Id | Where-Object {
    $taskGroup = $_
    $stateConflicts = @($taskGroup.Group | Group-Object Worktree | Where-Object {
        $states = @($_.Group.State | Sort-Object -Unique)
        $states -contains 'Active' -and $states -contains 'Done'
    })
    $stateConflicts.Count -gt 0 -or
    (@($taskGroup.Group | ForEach-Object { Split-Path $_.Path -Leaf } | Sort-Object -Unique).Count -gt 1)
})
if ($dupes.Count) {
    Add-Check FAIL 'task-ids' "중복/상태 충돌: $($dupes.Name -join ', ')"
} else {
    Add-Check OK 'task-ids' "Task $(@($allTasks.Id | Sort-Object -Unique).Count)개, 중복 없음"
}

# 5. 정리 대상 worktree (원격에서 이미 삭제된 브랜치의 worktree)
$extra = @(Get-Worktrees | Select-Object -Skip 1)
$stale = @($extra | Where-Object {
    $_.Branch -and -not (& git -C $RepoRoot rev-parse --verify --quiet "refs/remotes/origin/$($_.Branch)")
})
if ($stale.Count) {
    Add-Check WARN 'worktrees' "원격에 없는 브랜치의 worktree: $($stale.Branch -join ', ') (worktree.ps1 -Remove)"
} else {
    Add-Check OK 'worktrees' "추가 worktree $($extra.Count)개"
}

# 6. Unity 버전 (단일 출처: Harness/Engine/Unity/Decisions/version.md)
$versionDoc = Get-RepoPath 'Harness/Engine/Unity/Decisions/version.md'
$projectVersionFile = Get-RepoPath 'ProjectSettings/ProjectVersion.txt'
$decided = (Select-String -LiteralPath $versionDoc -Pattern 'Unity Editor: `([^`]+)`' | Select-Object -First 1).Matches[0].Groups[1].Value
$actual = ((Get-Content -LiteralPath $projectVersionFile | Where-Object { $_ -like 'm_EditorVersion:*' }) -replace 'm_EditorVersion:\s*', '').Trim()
if ($decided -eq $actual) {
    Add-Check OK 'unity-version' $actual
} else {
    Add-Check FAIL 'unity-version' "ProjectVersion.txt=$actual, Decisions/version.md=$decided"
}

$manifest = Get-Content -LiteralPath (Get-RepoPath 'Packages/manifest.json') -Raw | ConvertFrom-Json -AsHashtable
$pipelineActual = $manifest.dependencies['com.unity.pipeline']
$pipelineDecided = (Select-String -LiteralPath $versionDoc -Pattern '`com.unity.pipeline` `([^`]+)`' | Select-Object -First 1).Matches[0].Groups[1].Value
if ($pipelineActual -eq $pipelineDecided) {
    Add-Check OK 'pipeline-pkg' $pipelineActual
} else {
    Add-Check WARN 'pipeline-pkg' "manifest=$pipelineActual, Decisions/version.md=$pipelineDecided (Pitfalls/pipeline-command-drift.md)"
}

# 7. Unity CLI / 에디터 설치 / 연결
$unity = Get-UnityCli
if (-not $unity) {
    Add-Check FAIL 'unity-cli' 'unity CLI를 찾을 수 없음'
} else {
    $ver = Invoke-NativeWithTimeout -FilePath $unity -Arguments @('--version') -TimeoutSeconds 10
    $cliVersion = $ver.StdOut.Trim()
    $factFile = Get-RepoPath 'Harness/Engine/Unity/Facts/unity-cli.md'
    if ($cliVersion -and (Select-String -LiteralPath $factFile -SimpleMatch "``$cliVersion``" -Quiet)) {
        Add-Check OK 'unity-cli' $cliVersion
    } else {
        Add-Check WARN 'unity-cli' "$cliVersion (Facts/unity-cli.md와 다름 — 명령 변경 여부 확인)"
    }

    $editors = Invoke-NativeWithTimeout -FilePath $unity -Arguments @('editors', '--json', '--no-banner') -TimeoutSeconds 20
    $editorsJson = ConvertFrom-UnityJson $editors.StdOut
    if ($editors.TimedOut) {
        Add-Check WARN 'unity-editor' '설치 목록 조회 시간 초과'
    } elseif ($editorsJson -and @($editorsJson.data | Where-Object version -eq $actual).Count) {
        Add-Check OK 'unity-editor' "$actual 설치됨"
    } else {
        Add-Check FAIL 'unity-editor' "$actual 미설치 (unity install $actual)"
    }

    if ($SkipEditor) {
        Add-Check OK 'editor-link' '생략(-SkipEditor)'
    } else {
        $status = Invoke-NativeWithTimeout -FilePath $unity -Arguments @('status', '--json', '--no-banner', '--project-path', $RepoRoot) -TimeoutSeconds 15
        $statusJson = ConvertFrom-UnityJson $status.StdOut
        $instances = @($statusJson.data.instances)
        if ($status.TimedOut) {
            Add-Check WARN 'editor-link' '연결 확인 시간 초과'
        } elseif ($instances.Count -gt 0) {
            Add-Check OK 'editor-link' "연결됨 ($($instances.Count)개)"
        } else {
            Add-Check WARN 'editor-link' '연결된 에디터 없음. Unity 조작이 필요하면 Pitfalls/editor-not-visible.md'
        }
    }
}

# 8. .env / Skill 어댑터
& git -C $RepoRoot check-ignore -q .env
$envIgnored = $LASTEXITCODE -eq 0
& git -C $RepoRoot check-ignore -q .env.example
$exampleIgnored = $LASTEXITCODE -eq 0
if ($envIgnored -and -not $exampleIgnored) {
    Add-Check OK 'env' '.env 무시됨, .env.example 추적됨'
} else {
    Add-Check FAIL 'env' '.gitignore의 .env 규칙 확인 필요'
}

$skillsDir = Get-RepoPath '.claude/skills'
if (Test-Path -LiteralPath $skillsDir) {
    Add-Check OK 'skill-links' '.claude/skills 존재'
} else {
    Add-Check WARN 'skill-links' '없음. pwsh -File Scripts/setup-links.ps1'
}

$ghState = Get-GhState
if ($ghState.State -eq 'Ready') {
    Add-Check OK 'github-cli' $ghState.Detail
    try {
        Assert-CommitIdentity
        Add-Check OK 'commit-author' "$(& git -C $RepoRoot config user.name) (gh 계정, 커밋 검사 훅 켜짐)"
    } catch {
        Add-Check FAIL 'commit-author' '커밋 작성자가 gh 계정이 아니거나 커밋 검사 훅이 꺼짐 → pwsh -File Scripts/setup-gh.ps1'
    }
} else {
    Add-Check WARN 'github-cli' "$($ghState.Detail). 커밋은 GitHub 연결 후에만 가능 (Skill: setup-gh)"
}

$fail = @($results | Where-Object Level -eq 'FAIL').Count
$warn = @($results | Where-Object Level -eq 'WARN').Count
$summary = if ($fail) { 'FAIL' } elseif ($warn) { 'WARN' } else { 'OK' }
Write-Output ''
Write-Output "Doctor: $summary (fail $fail, warn $warn, total $($results.Count))"
if ($fail) { exit 1 }
exit 0
