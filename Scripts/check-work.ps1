# 작업자의 "검사해줘". 0 구역 → 1 문서 → 2 컴파일 → 3 테스트를 실행하고 결과를 Task handoff.md Verification에 적는다.
[CmdletBinding()]
param([switch]$SkipUnity)

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

$scope = Invoke-Step 'verify-scope.ps1'
$results['구역 검사'] = if ($scope.Code -eq 0) { "PASS ($today)" } else { "FAIL — $(@($scope.Output | Where-Object { $_ -like '[위반]*' }).Count)건 ($today)" }
$fast = Invoke-Step 'verify-fast.ps1'
$results['1 문서/규칙'] = if ($fast.Code -eq 0) { "PASS ($today)" } else { "FAIL ($today)" }

if ($SkipUnity) {
    $results['2 컴파일'] = 'SKIPPED(-SkipUnity)'
    $results['3 테스트'] = 'SKIPPED(-SkipUnity)'
    $unity = $null
} else {
    $unity = Invoke-Step 'verify-unity.ps1' @('-ProjectPath', $RepoRoot, '-Compile', '-RunTests')
    $text = $unity.Output -join "`n"
    if ($unity.Code -eq 2) {
        $results['2 컴파일'] = 'ENV_BLOCKED(Unity 에디터 미연결)'
        $results['3 테스트'] = 'ENV_BLOCKED(Unity 에디터 미연결)'
    } else {
        $results['2 컴파일'] = if ($text -match 'Compile: PASS') { "PASS ($today)" } elseif ($text -match 'COMPILE_FAILED') { "FAIL — 컴파일 오류 ($today)" } else { "FAIL — $(($unity.Output | Select-Object -Last 1)) ($today)" }
        $results['3 테스트'] = if ($text -match 'NO_PROJECT_TESTS') { 'NO_PROJECT_TESTS' }
            elseif ($text -match 'total (\d+), passed (\d+), failed (\d+)') { if ([int]$Matches[3] -eq 0) { "PASS ($($Matches[2])/$($Matches[1]), $today)" } else { "FAIL — $($Matches[3])개 실패 ($today)" } }
            else { '미실행(컴파일 단계에서 중단)' }
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
foreach ($step in @($scope, $fast, $unity) | Where-Object { $_ -and $_.Code -eq 1 }) {
    Write-Output '  --- 세부 ---'
    $step.Output | Where-Object { $_ -match '\[(위반|FAIL|HARD)\]|FAILED|오류' } | Select-Object -First 10 | ForEach-Object { Write-Output "  $_" }
}
if (@($results.Values | Where-Object { $_ -like 'FAIL*' }).Count) { exit 1 }
exit 0
