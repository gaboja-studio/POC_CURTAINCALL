# 외부 에셋 검사: 외부(서드파티) 에셋이 private submodule(Assets/_ThirdParty) 밖, 즉 공개 레포에 올라가지 않았는지 본다.
#   -CheckSubmodule: submodule 포인터가 private 레포 main에 push된 커밋인지도 본다(pre-push 훅, PR 검사).
# 규칙: Harness/Project/Decisions/third-party-assets.md
[CmdletBinding()]
param([switch]$CheckSubmodule)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$subPath = 'Assets/_ThirdParty'
$subRepo = 'gaboja-studio/CURTAINCALL_PaidAssets'

# Assets/ 최상위에 둘 수 있는 것. Asset Store 패키지는 보통 최상위에 자기 폴더를 만들므로 여기서 걸린다.
# 직접 만든 콘텐츠용 최상위 폴더가 새로 필요하면 PM이 이 목록과 Docs/Guides/assets-folder-rules.md를 함께 고친다.
$allowedTop = @(
    '_ThirdParty', 'InputSystem_Actions.inputactions', 'Readme.asset',
    'Resources', 'Scenes', 'Scripts', 'Settings', 'TextMesh Pro', 'TutorialInfo'
)
# 외부 에셋 표지: 이름으로 알 수 있는 Asset Store 기본 설치 경로. 새 외부 에셋을 들이면 여기에 경로를 추가한다.
$blocked = @(
    '^Assets/Plugins/Demigiant(/|\.meta$)',
    '/DOTweenPro[^/]*(/|\.meta$)',
    '/DemiLib(/|\.meta$)'
)
# 직접 만든 콘텐츠에는 없는 파일 종류(빌드된 라이브러리·패키지 묶음)
$binary = '(?i)\.(dll|so|dylib|a|aar|jar|unitypackage)$|\.(bundle|framework)/'

# submodule 연결이 빠지면 외부 에셋을 받을 곳이 없다.
$gitmodules = Get-RepoPath '.gitmodules'
if (-not (Test-Path -LiteralPath $gitmodules) -or -not (Select-String -LiteralPath $gitmodules -Pattern "path\s*=\s*$subPath\s*$" -Quiet)) {
    $failures.Add(".gitmodules에 $subPath submodule이 없음")
}

# 커밋·스테이징된 파일 + 아직 추가하지 않은 새 파일(.gitignore 제외)
$files = @(& git -C $RepoRoot ls-files) + @(& git -C $RepoRoot ls-files --others --exclude-standard)
foreach ($file in $files | Where-Object { $_ -like 'Assets/*' -and $_ -ne $subPath -and $_ -notlike "$subPath/*" } | Sort-Object -Unique) {
    $top = ($file -split '/')[1] -replace '\.meta$', ''
    $hint = "$subPath/<퍼블리셔>/<에셋>/ 로 옮기고 private 레포에 저장하세요"
    if ($allowedTop -notcontains $top) { $failures.Add("허용되지 않은 Assets 최상위 경로: $file ($hint)") }
    elseif (@($blocked | Where-Object { $file -match $_ }).Count) { $failures.Add("외부 에셋이 공개 레포에 있음: $file ($hint)") }
    elseif ($file -match $binary) { $failures.Add("외부 라이브러리·패키지 파일: $file ($hint)") }
}

# submodule 안의 수정은 공개 레포 커밋에 실리지 않는다. 저장은 private 레포에서 한다.
if (Test-ThirdPartyReady) {
    $dirty = @(& git -C (Get-RepoPath $subPath) status --porcelain 2>$null)
    if ($dirty.Count) { $warnings.Add("$subPath 안에 저장하지 않은 변경 $($dirty.Count)건 — 외부 에셋 수정은 PM에게 요청(private 레포에서 커밋·push)") }
}

if ($CheckSubmodule) {
    $sha = ((& git -C $RepoRoot ls-tree HEAD $subPath) -split '\s+')[2]
    $published = $null
    $sub = Get-RepoPath $subPath
    if ($sha -and (Test-ThirdPartyReady)) {
        & git -C $sub fetch --quiet origin main 2>$null
        if ($LASTEXITCODE -eq 0) {
            & git -C $sub merge-base --is-ancestor $sha origin/main 2>$null
            $published = $LASTEXITCODE -eq 0
        }
    } elseif ($sha -and $env:GH_TOKEN) {
        # CI: submodule을 받지 않고 GitHub API로 main이 그 커밋을 포함하는지 본다.
        $status = & gh api "repos/$subRepo/compare/$sha...main" --jq '.status' 2>$null
        if ($LASTEXITCODE -eq 0) { $published = $status -in 'ahead', 'identical' }
    }
    if ($null -eq $published) { $warnings.Add("$subPath 포인터($sha)가 private 레포에 있는지 확인하지 못함 (권한·네트워크)") }
    elseif (-not $published) { $failures.Add("$subPath 포인터($sha)가 private 레포 main에 없음 — private 레포에서 먼저 push하세요") }
}

$warnings | ForEach-Object { Write-Output "[주의] $_" }
if ($failures.Count) {
    $failures | Select-Object -First 20 | ForEach-Object { Write-Output "[위반] $_" }
    if ($failures.Count -gt 20) { Write-Output "... 외 $($failures.Count - 20)건" }
    Write-Output "ThirdParty: FAIL ($($failures.Count))"
    exit 1
}
Write-Output 'ThirdParty: OK'
exit 0
