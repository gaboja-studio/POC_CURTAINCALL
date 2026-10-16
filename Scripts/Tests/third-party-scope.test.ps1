# 실제 private 에셋 없이 격리 gitlink·역할 명부로 저장/제출 공용 구역 검사를 검증한다.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ScratchRoot)
$ErrorActionPreference = 'Stop'
$gitExe = (Get-Command git -CommandType Application | Select-Object -First 1).Source
$source = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$fixture = Join-Path $ScratchRoot ('third-party-scope-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
function Put([string]$Path, [string]$Text) {
    $full = Join-Path $fixture $Path
    New-Item -ItemType Directory -Path (Split-Path $full) -Force | Out-Null
    Set-Content -LiteralPath $full -Value $Text -Encoding utf8NoBOM
}
function Git {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    $out = @(& $gitExe -C $fixture @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "fixture git: $Arguments / $out" }
    return ,$out
}
function Check([string]$Name, [string]$Actor, [string]$Branch, [bool]$Pass, [string]$Pattern = '') {
    $out = @(& ([System.Environment]::ProcessPath) -NoProfile -File (Join-Path $fixture 'Scripts/verify-scope.ps1') -Branch $Branch -Base dev -Actor $Actor 2>&1)
    $code = $LASTEXITCODE
    if (($code -eq 0) -ne $Pass -or ($Pattern -and ($out -join "`n") -notmatch $Pattern)) { throw "$Name (exit $code): $out" }
    Write-Output "PASS $Name"
}
Put 'Scripts/Lib/common.ps1' (Get-Content (Join-Path $source 'Scripts/Lib/common.ps1') -Raw)
Put 'Scripts/verify-scope.ps1' (Get-Content (Join-Path $source 'Scripts/verify-scope.ps1') -Raw)
Put 'Harness/Project/Facts/team.md' @'
| 역할 | GitHub 계정 | 비고 |
|---|---|---|
| 개발 PM | `@pm` | fixture |
| 에셋 관리자 | `@manager` | fixture |
| 작업자 | `@worker` | fixture |
'@
Put '.github/CODEOWNERS' '/Harness/ @pm'
Put 'Tasks/Active/Task-20261016-001-fixture/meta.md' "# Fixture`n- **Type:** feat`n- **Branch:** feat/fixture`n- **Integration:** dev`n- **Status:** working"
Put 'Tasks/Active/Task-20261016-001-fixture/setup.md' @'
## 작업 구역
- `Assets/Scenes/fixture.unity`
## 수정 금지
- `Assets/_ThirdParty/`
- `Packages/`
- `ProjectSettings/`
- `Assets/Resources/`
'@
Put 'Assets/_ThirdParty.meta' 'fixture'
Put 'Assets/Resources.meta' 'fixture'
Put 'Assets/Resources/font.asset' 'fixture'
Put 'Assets/Resources/font.asset.meta' 'fixture'
Put 'Packages/manifest.json' '{}'
Put 'ProjectSettings/ProjectSettings.asset' 'fixture'
Put 'Assets/TutorialInfo.meta' 'fixture'
Put 'Assets/TutorialInfo/readme.txt' 'fixture'
Put 'Assets/TutorialInfo/readme.txt.meta' 'fixture'
Git init -b dev | Out-Null
Git config user.name 'Scope Fixture' | Out-Null
Git config user.email fixture@example.invalid | Out-Null
Git add --all | Out-Null
Git commit -m fixture | Out-Null
$first = (Git rev-parse HEAD)[0]
Git update-index --add --cacheinfo "160000,$first,Assets/_ThirdParty" | Out-Null
Git commit -m gitlink | Out-Null
$second = (Git rev-parse HEAD)[0]
Git switch -c feat/fixture | Out-Null
# 실제 에셋 없이 gitlink SHA만 갱신한다.
Git update-index --cacheinfo "160000,$second,Assets/_ThirdParty" | Out-Null
Check 'manager task pointer overrides legacy prohibition' manager feat/fixture $true
Check 'manager resource pointer' manager resource/third-party/fixture $true
Check 'role matching ignores account case' MANAGER resource/third-party/fixture $true
Check 'worker task pointer denied' worker feat/fixture $false '지정 에셋 관리자'
Check 'worker resource pointer denied' worker resource/third-party/fixture $false '지정 에셋 관리자'
Check 'unknown actor denied' unknown resource/third-party/fixture $false
Check 'worker urgent fix pointer denied' worker fix/urgent $false '지정 에셋 관리자'
Check 'manager urgent fix pointer' manager fix/urgent $true
Check 'PM pointer' pm feat/fixture $true
Check 'manager cannot push dev' manager dev $false '직접 작업'
foreach ($path in 'Packages/manifest.json', 'ProjectSettings/ProjectSettings.asset', 'Assets/Resources/font.asset', 'Assets/TutorialInfo/readme.txt', 'Assets/_ThirdParty.meta') {
    $original = Get-Content (Join-Path $fixture $path) -Raw
    Put $path 'changed'
    Check "manager task still denies $path" manager feat/fixture $false
    Put $path $original.TrimEnd("`r", "`n")
}
Put 'Harness/Project/Facts/team.md' ((Get-Content (Join-Path $fixture 'Harness/Project/Facts/team.md') -Raw).Replace('@manager','@revoked'))
Check 'role changes remain PM protected' manager resource/third-party/fixture $false 'PM 전용'
Git checkout -- Harness/Project/Facts/team.md | Out-Null
Write-Output 'Third-party scope: PASS (16 cases)'
