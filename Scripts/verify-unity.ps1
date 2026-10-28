# 검증 2~3단계: Unity 에디터 상태 확인(읽기 전용) → 선택적으로 컴파일 → 선택적으로 테스트.
# Pipeline 명령 이름은 버전마다 바뀔 수 있다. 실패하면 아래 $Commands를 현재 버전에 맞게 고친다
# (Harness/Engine/Unity/Pitfalls/pipeline-command-drift.md).
# 종료 코드: 0 성공, 1 실패, 2 에디터 없음(ENV_BLOCKED)
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [switch]$Compile,
    [switch]$RunTests,
    [ValidateSet('EditMode', 'PlayMode')][string]$Mode = 'EditMode',
    [string]$Filter
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$Commands = @{
    EditorStatus    = 'editor_status'
    OpenScenes      = 'list_open_scenes'
    Recompile       = 'recompile'
    RecompileStatus = 'recompile_status'
    RunTests        = 'run_tests'
    TestStatus      = 'test_status'
}

$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$unity = Get-UnityCli
if (-not $unity) { throw 'unity CLI를 찾을 수 없음' }
$reportDir = Join-Path $project '.harness' 'local'
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null

function Invoke-EditorCommand {
    param([Parameter(Mandatory)][string]$Name, [int]$TimeoutSeconds = 30, [string[]]$Arguments = @())
    $r = Invoke-NativeWithTimeout -FilePath $unity -TimeoutSeconds ($TimeoutSeconds + 5) -Arguments (@(
        'command', $Name) + $Arguments + @('--project-path', $project, '--timeout', "$TimeoutSeconds", '--json', '--no-banner'))
    if ($r.TimedOut) { throw "TIMEOUT: unity command $Name" }
    $json = ConvertFrom-UnityJson $r.StdOut
    if (-not $json) { throw "PARSE_FAILED: unity command $Name 출력: $($r.StdOut)$($r.StdErr)" }
    if (-not $json.success) {
        $msg = @($json.errors | ForEach-Object message) -join '; '
        if ($msg -match 'not found|unknown|No command') { throw "COMMAND_MISSING: '$Name' — Pitfalls/pipeline-command-drift.md 참고. $msg" }
        throw "COMMAND_FAILED: $Name — $msg"
    }
    $result = $json.data.result ?? $json.data
    if ($result -is [string] -and $result.Trim() -match '^[\[{]') { $result = $result | ConvertFrom-Json -Depth 20 }
    return $result
}

Write-Output "Project: $project"

# 1) 에디터 연결 (읽기 전용)
$status = Invoke-NativeWithTimeout -FilePath $unity -TimeoutSeconds 15 -Arguments @(
    'status', '--json', '--no-banner', '--project-path', $project)
$statusJson = ConvertFrom-UnityJson $status.StdOut
# 같은 경로로 시작하는 MPPM 가상 플레이어(<project>/Library/VP/...)도 함께 나오므로 경로가 정확히 같은 에디터만 센다
# (Harness/Engine/Unity/Pitfalls/mppm-virtual-player-status.md)
# CLI beta.14부터 행의 project 키는 폐기 예정이므로 projectPath를 먼저 읽는다.
$instances = @($statusJson.data.instances | Where-Object {
    $instancePath = $_.projectPath ?? $_.project
    $instancePath -and ([System.IO.Path]::GetFullPath($instancePath).TrimEnd('/') -eq $project.TrimEnd('/'))
})
if ($status.TimedOut -or $instances.Count -eq 0) {
    Write-Output 'ENV_BLOCKED: 연결된 에디터 없음. 수정은 시도하지 않았음 (Pitfalls/editor-not-visible.md).'
    exit 2
}
if ($instances.Count -gt 1) { throw "이 경로에 연결된 에디터가 $($instances.Count)개. 대상이 모호하여 중단." }

# 2) 에디터 상태 / 열린 씬 (읽기 전용)
$editor = Invoke-EditorCommand $Commands.EditorStatus
if ($editor.projectPath -and ([System.IO.Path]::GetFullPath($editor.projectPath).TrimEnd('/') -ne $project.TrimEnd('/'))) {
    throw "연결된 에디터 경로 불일치: $($editor.projectPath)"
}
if ($editor.compiling -or $editor.domainReloadInProgress) { throw '에디터가 컴파일/리로드 중. 끝난 뒤 다시 실행.' }
if ($editor.playMode -and $editor.playMode -ne 'stopped') { throw "Play Mode 중: $($editor.playMode)" }
Write-Output "Editor: ready (status=$($editor.status))"

$scenes = Invoke-EditorCommand $Commands.OpenScenes
$dirty = @($scenes.scenes | Where-Object isDirty)
if ($dirty.Count) { throw "저장되지 않은 씬: $($dirty.path -join ', ') (Pitfalls/scene-not-saved.md)" }
Write-Output "Open scenes: $(@($scenes.scenes).Count), dirty 0"

# 3) 컴파일
if ($Compile) {
    try { $null = Invoke-EditorCommand $Commands.Recompile -TimeoutSeconds 60 } catch {
        # 도메인 리로드가 요청 응답을 끊을 수 있다. 상태 폴링으로 판단한다.
        if ($_.Exception.Message -like 'COMMAND_MISSING*') { throw }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(300)
    $compileResult = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $s = Invoke-EditorCommand $Commands.RecompileStatus
            if ($s.status -in 'completed', 'up_to_date') { $compileResult = $s; break }
        } catch {
            if ($_.Exception.Message -like 'COMMAND_MISSING*') { throw }
        }
        Start-Sleep -Seconds 2
    }
    if (-not $compileResult) { throw 'TIMEOUT: 300초 안에 컴파일이 끝나지 않음' }
    if ($compileResult.failed -or $compileResult.compilationFailed -or @($compileResult.errors).Count) {
        throw "COMPILE_FAILED: $(@($compileResult.errors) -join '; ')"
    }
    Write-Output "Compile: PASS ($($compileResult.status))"
} else {
    Write-Output 'Compile: SKIPPED'
}

# 4) 테스트 — 열린 에디터 안에서 실행한다. batch `unity test`는 같은 프로젝트를 연 에디터가 있으면 거부된다
#    (Harness/Engine/Unity/Pitfalls/unity-test-editor-open.md). PlayMode는 도메인 리로드 때문에 동기 실행이 안 되므로
#    두 모드 모두 비동기로 시작하고 상태 파일이 completed가 될 때까지 기다린다.
if ($RunTests) {
    $report = Join-Path $reportDir "test-results-$($Mode.ToLower()).json"
    Remove-Item -LiteralPath $report -ErrorAction SilentlyContinue
    # 이전 실행의 결과를 새 결과로 읽지 않게 상태 파일을 먼저 지운다
    $statusFile = Join-Path $project 'Temp' 'pipeline_test_status.json'
    Remove-Item -LiteralPath $statusFile -ErrorAction SilentlyContinue
    $testArgs = @('--mode', $Mode, '--async_tests', 'true')
    if ($Filter) { $testArgs += @('--filter', $Filter) }
    $started = Invoke-EditorCommand $Commands.RunTests -Arguments $testArgs -TimeoutSeconds 60
    if ($started.success -eq $false) { throw "TEST_RUN_FAILED: $($started.error ?? $started.message)" }
    $deadline = [DateTime]::UtcNow.AddSeconds(900)
    $done = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds 2
        try {
            $s = Invoke-EditorCommand $Commands.TestStatus
            if ($s.status -eq 'completed') { $done = $s; break }
            if ($s.status -in 'failed', 'error', 'aborted') { throw "TEST_RUN_FAILED: $($s.status) $($s.message)" }
        } catch {
            # PlayMode 진입·종료 때 도메인 리로드로 연결이 잠깐 끊긴다. 상태 확인을 계속한다.
            if ($_.Exception.Message -like 'COMMAND_MISSING*' -or $_.Exception.Message -like 'TEST_RUN_FAILED*') { throw }
        }
    }
    if (-not $done) { throw 'TIMEOUT: 테스트가 900초 안에 끝나지 않음' }
    $done | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $report -Encoding utf8NoBOM
    $sum = $done.summary
    $total = [int]$sum.total
    if ($total -eq 0) {
        Write-Output "Tests($Mode): NO_PROJECT_TESTS"
    } else {
        Write-Output "Tests($Mode): total $total, passed $($sum.passed), failed $($sum.failed), skipped $($sum.skipped) → $report"
        if ([int]$sum.failed -gt 0 -or ([int]$sum.passed + [int]$sum.skipped) -ne $total) { exit 1 }
    }
} else {
    Write-Output 'Tests: SKIPPED'
}
exit 0
