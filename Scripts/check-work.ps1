# 작업자의 "검사해줘". 0 구역 → 1 문서 → 2 컴파일 → 3 테스트를 실행하고 결과를 Task handoff.md Verification에 적는다.
[CmdletBinding()]
param(
    [switch]$SkipUnity,
    [ValidateSet('EditMode', 'PlayMode')][string]$Mode = 'EditMode'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$task = Find-CurrentTask
if (-not $task) { throw '현재 브랜치에 대응하는 Task가 없습니다. "Task-○○○ 시작해줘"를 먼저 하세요.' }
$today = Get-Date -Format 'MM-dd'
$results = [ordered]@{}

function Invoke-Step { param([string]$Script, [string[]]$Arguments = @())
    # PATH에 pwsh가 없을 수 있으므로 지금 실행 중인 PowerShell을 그대로 쓴다.
    $out = & ([System.Environment]::ProcessPath) -NoProfile -File (Join-Path $PSScriptRoot $Script) @Arguments 2>&1
    return [pscustomobject]@{ Code = $LASTEXITCODE; Output = @($out | ForEach-Object { "$_" }) }
}

$steps = [System.Collections.Generic.List[object]]::new()
$scope = Invoke-Step 'verify-scope.ps1'
$steps.Add($scope)
$results['구역 검사'] = if ($scope.Code -eq 0) { "PASS ($today)" } else { "FAIL — 구역 검사 실패 ($today)" }

if ($scope.Code -ne 0) {
    $results['1 문서/규칙'] = 'SKIPPED(구역 검사 실패)'
    $results['2 컴파일'] = 'SKIPPED(구역 검사 실패)'
    $results['3 테스트'] = 'SKIPPED(구역 검사 실패)'
} else {
    $fast = Invoke-Step 'verify-fast.ps1'
    $steps.Add($fast)
    $results['1 문서/규칙'] = if ($fast.Code -eq 0) { "PASS ($today)" } else { "FAIL — 문서/규칙 검사 실패 ($today)" }
    if ($fast.Code -ne 0) {
        $results['2 컴파일'] = 'SKIPPED(문서/규칙 검사 실패)'
        $results['3 테스트'] = 'SKIPPED(문서/규칙 검사 실패)'
    } elseif ($SkipUnity) {
        $results['2 컴파일'] = 'SKIPPED(-SkipUnity)'
        $results['3 테스트'] = 'SKIPPED(-SkipUnity)'
    } else {
        $compile = Invoke-Step 'verify-unity.ps1' @('-ProjectPath', $RepoRoot, '-Compile')
        $steps.Add($compile)
        if ($compile.Code -eq 2) {
            $results['2 컴파일'] = 'ENV_BLOCKED(Unity 에디터 미연결)'
            $results['3 테스트'] = 'SKIPPED(컴파일 단계 환경 차단)'
        } elseif ($compile.Code -ne 0 -or ($compile.Output -join "`n") -notmatch 'Compile: PASS') {
            $results['2 컴파일'] = "FAIL — 컴파일 단계 오류 ($today)"
            $results['3 테스트'] = 'SKIPPED(컴파일 단계 실패)'
        } else {
            $results['2 컴파일'] = "PASS ($today)"
            $unity = Invoke-Step 'verify-unity.ps1' @('-ProjectPath', $RepoRoot, '-RunTests', '-Mode', $Mode)
            $steps.Add($unity)
            $text = $unity.Output -join "`n"
            if ($unity.Code -eq 2) {
                $results['3 테스트'] = 'ENV_BLOCKED(Unity 에디터 미연결)'
            } elseif ($text -match "Tests\($Mode\): NO_PROJECT_TESTS") {
                $results['3 테스트'] = if ($unity.Code -eq 0) { "NO_PROJECT_TESTS ($Mode)" } else { "FAIL — 테스트 실행 오류 ($today)" }
            } elseif ($text -match "Tests\($Mode\): total (\d+), passed (\d+), failed (\d+)") {
                $results['3 테스트'] = if ($unity.Code -eq 0 -and [int]$Matches[1] -gt 0 -and [int]$Matches[2] -eq [int]$Matches[1] -and [int]$Matches[3] -eq 0) { "PASS ($Mode, $($Matches[2])/$($Matches[1]), $today)" } else { "FAIL — 테스트 오류 ($today)" }
            } else {
                $results['3 테스트'] = "FAIL — 테스트 결과 없음 ($today)"
            }
        }
    }
}

$handoff = Join-Path $task.Path 'handoff.md'
$content = Get-Content -LiteralPath $handoff -Raw
foreach ($key in $results.Keys) {
    $content = [regex]::Replace($content, "(?m)^- $([regex]::Escape($key)):.*$", "- ${key}: $($results[$key])")
}
Set-Content -LiteralPath $handoff -Value $content -Encoding utf8NoBOM -NoNewline

Write-Output "검사 결과 ($($task.Id)) — handoff.md에 기록함"
foreach ($key in $results.Keys) { Write-Output ("  {0,-10} {1}" -f $key, $results[$key]) }
foreach ($step in $steps | Where-Object { $_.Code -ne 0 }) {
    Write-Output '  --- 세부 ---'
    $step.Output | Select-Object -Last 10 | ForEach-Object { Write-Output "  $_" }
}
if (@($results.Values | Where-Object { $_ -like 'ENV_BLOCKED*' }).Count) {
    Write-Output "Unity 에디터에서 프로젝트 '$RepoRoot'를 열고 연결 상태를 확인한 뒤 pwsh -File Scripts/check-work.ps1 -Mode $Mode 로 다시 검사하세요."
    exit 2
}
if (@($results.Values | Where-Object { $_ -like 'FAIL*' }).Count) { exit 1 }
if ($SkipUnity) { Write-Output '필수 Unity 검증을 생략했습니다. 컴파일·테스트 통과로 처리하지 않습니다.'; exit 2 }
if ($results['2 컴파일'] -like 'PASS*') {
    Save-VerificationEvidence -Branch ((& git -C $RepoRoot branch --show-current).Trim())
}
exit 0
