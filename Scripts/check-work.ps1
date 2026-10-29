# 묶음 완료/사용자 요청 시 필요한 검사만 실행한다. 자동 테스트는 선택한다.
[CmdletBinding()]
param([switch]$SkipUnity,[switch]$RunTests,[ValidateSet('EditMode','PlayMode')][string]$Mode='EditMode',[string]$Base,[switch]$NoRecord)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')
if (-not $Base) { $Base=Get-CheckBase }
$paths=@(Get-ChangedPaths -Base $Base)
$needsUnity=@($paths | Where-Object { $_ -match '^(Assets|Packages|ProjectSettings)/' }).Count -gt 0
$before=Get-WorktreeFingerprint
$results=[ordered]@{}
$exitCode=0
function Step([string]$name,[string[]]$options=@()) {
    $out=@(& ([Environment]::ProcessPath) -NoProfile -File (Join-Path $PSScriptRoot $name) @options 2>&1)
    $code=$LASTEXITCODE
    $out | ForEach-Object { Write-Host "$_" }
    return [pscustomobject]@{ Code=$code; Text=($out -join "`n") }
}
$s=Step 'verify-scope.ps1' @('-Base',$Base)
$results['안전성']=if ($s.Code) { 'FAIL' } else { 'PASS' }
if ($s.Code) { $exitCode=1 }
if (-not $exitCode) {
    $s=Step 'verify-fast.ps1'
    $results['문서/규칙']=if ($s.Code) { 'FAIL' } else { 'PASS' }
    if ($s.Code) { $exitCode=1 }
} else { $results['문서/규칙']='SKIPPED(선행 실패)' }
if ($exitCode) { $results['컴파일']='SKIPPED(선행 실패)' }
elseif (-not $needsUnity -and -not $RunTests) { $results['컴파일']='SKIPPED(Unity 변경 없음)' }
elseif ($SkipUnity) { $results['컴파일']='SKIPPED(-SkipUnity)'; $exitCode=2 }
else {
    $s=Step 'verify-unity.ps1' @('-ProjectPath',$RepoRoot,'-Compile')
    $results['컴파일']=if ($s.Code -eq 2) { 'ENV_BLOCKED(Unity 환경)' } elseif ($s.Code -ne 0 -or $s.Text -notmatch 'Compile: PASS') { 'FAIL' } else { 'PASS' }
    if ($results['컴파일'] -ne 'PASS') { $exitCode=if ($s.Code -eq 2) { 2 } else { 1 } }
}
$results['테스트']='SKIPPED(자동 테스트 선택 안 함)'
if ($RunTests) {
    if ($exitCode) { $results['테스트']='SKIPPED(선행 실패·생략)' }
    else {
        $s=Step 'verify-unity.ps1' @('-ProjectPath',$RepoRoot,'-RunTests','-Mode',$Mode)
        $results['테스트']=if ($s.Code -eq 2) { 'ENV_BLOCKED(Unity 환경)' } elseif ($s.Code) { 'FAIL' } elseif ($s.Text -match "Tests\($Mode\): NO_PROJECT_TESTS") { 'NO_PROJECT_TESTS' } elseif ($s.Text -match "Tests\($Mode\): total (\d+), passed (\d+), failed (\d+)" -and [int]$Matches[1] -gt 0 -and $Matches[1] -eq $Matches[2] -and [int]$Matches[3] -eq 0) { 'PASS' } else { 'FAIL(결과 없음)' }
        if ($results['테스트'] -like 'FAIL*') { $exitCode=1 }
        elseif ($s.Code -eq 2) { $exitCode=2 }
    }
}
$report='- '+(Get-Date -Format 'yyyy-MM-dd')+' 검사: '+(($results.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '; ')
if ((Get-WorktreeFingerprint) -ne $before) { throw '검사 중 파일이 바뀌었습니다. 현재 변경을 다시 확인하세요.' }
Write-Output $report
$task=Find-CurrentTask
if ($task -and -not $NoRecord) { Add-TaskLog -TaskPath $task.Path -Text $report }
if (-not $exitCode) { Save-VerificationEvidence -Branch ((& git -C $RepoRoot branch --show-current).Trim()) }
exit $exitCode
