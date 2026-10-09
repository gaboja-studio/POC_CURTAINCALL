# 외부 에셋 검사: 외부(서드파티) 에셋이 private submodule(Assets/_ThirdParty) 밖, 즉 공개 레포에 올라가지 않았는지 본다.
# 규칙: Harness/Project/Decisions/third-party-assets.md
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$failures = [System.Collections.Generic.List[string]]::new()

# submodule 연결이 빠지면 외부 에셋을 받을 곳이 없다.
$gitmodules = Get-RepoPath '.gitmodules'
if (-not (Test-Path -LiteralPath $gitmodules) -or -not (Select-String -LiteralPath $gitmodules -Pattern 'path\s*=\s*Assets/_ThirdParty\s*$' -Quiet)) {
    $failures.Add('.gitmodules에 Assets/_ThirdParty submodule이 없음')
}

# Asset Store가 기본 경로에 설치하는 외부 에셋 표지. 새 외부 에셋을 들이면 여기에 경로를 추가한다.
$blocked = @(
    '^Assets/Plugins/Demigiant(/|\.meta$)',
    '/DOTweenPro[^/]*(/|\.meta$)',
    '/DemiLib(/|\.meta$)'
)

# 커밋·스테이징된 파일 + 아직 추가하지 않은 새 파일(.gitignore 제외)
$files = @(& git -C $RepoRoot ls-files) + @(& git -C $RepoRoot ls-files --others --exclude-standard)
foreach ($file in $files | Where-Object { $_ -like 'Assets/*' -and $_ -notlike 'Assets/_ThirdParty/*' }) {
    if (@($blocked | Where-Object { $file -match $_ }).Count) { $failures.Add("외부 에셋이 공개 레포에 있음: $file (Assets/_ThirdParty submodule로 옮기세요)") }
}

if ($failures.Count) {
    $failures | Select-Object -First 20 | ForEach-Object { Write-Output "[위반] $_" }
    if ($failures.Count -gt 20) { Write-Output "... 외 $($failures.Count - 20)건" }
    Write-Output "ThirdParty: FAIL ($($failures.Count))"
    exit 1
}
Write-Output 'ThirdParty: OK'
exit 0
