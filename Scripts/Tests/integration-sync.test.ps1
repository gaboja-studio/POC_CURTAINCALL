# 격리 Git 저장소 + gh mock 회귀. 운영 origin/인증/작업 폴더에는 쓰지 않는다.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ScratchRoot)
$ErrorActionPreference = 'Stop'
$source = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$scratch = [IO.Path]::GetFullPath($ScratchRoot)
if ($scratch.TrimEnd('/', '\') -eq $source -or $scratch.StartsWith($source + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'ScratchRoot는 저장소 밖이어야 합니다.' }
$root = Join-Path $scratch ('integration-sync-' + [guid]::NewGuid().ToString('N'))
$checkout = Join-Path $root 'checkout'
$origin = Join-Path $root 'origin.git'
New-Item -ItemType Directory -Path $checkout -Force | Out-Null
$git = (Get-Command git).Source
$pwsh = [Environment]::ProcessPath
$passed = 0
function Git { & $git -C $checkout @args 2>$null; if ($LASTEXITCODE -ne 0) { throw "fixture git 실패: $args" } }
function Put { param([string]$Path, [string]$Text)
    $file = Join-Path $checkout $Path
    New-Item -ItemType Directory -Path (Split-Path $file) -Force | Out-Null
    Set-Content -LiteralPath $file -Value $Text -Encoding utf8NoBOM
}
function Assert { param([bool]$Condition, [string]$Message) if (-not $Condition) { throw $Message } }
function Check { param([string]$Label, [scriptblock]$Body) & $Body; $script:passed++; Write-Output "[PASS] $Label" }
function Meta { param([string]$Name, [string]$Base, [string]$Status = 'working', [string]$Branch = "integration/$Name")
    Put "Integrations/Active/Integration-$Name/meta.md" "# Integration-$Name`n- **Branch:** $Branch`n- **Base:** $Base`n- **Status:** $Status"
}

Git init -b dev | Out-Null
Git config user.name 'Integration Fixture'
Git config user.email 'fixture@example.invalid'
Git config core.autocrlf false
Git config core.longpaths true
Git config commit.gpgsign false
& $git init --bare $origin 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'fixture origin 생성 실패' }
Git remote add origin $origin
foreach ($path in @('Scripts/Lib/common.ps1', 'Scripts/sync-integration.ps1', '.github/PULL_REQUEST_TEMPLATE.md')) {
    $destination = Join-Path $checkout $path
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $path) -Destination $destination
}
Put 'Harness/Project/Facts/repository.md' '- **Base branch:** dev'
Put 'kept.txt' '원본'
Meta 'parent' 'dev'
Meta 'child' 'integration/parent'
Meta 'direct' 'dev'
Meta 'cycle-a' 'integration/cycle-b'
Meta 'cycle-b' 'integration/cycle-a'
Meta 'self' 'integration/self'
Meta 'cancelled' 'dev' 'cancelled'
Meta 'done' 'dev' 'done'
Meta 'closed' 'dev' 'closed'
Meta 'unknown' 'dev' 'mystery'
Meta 'no-status' 'dev' ''
Meta 'no-base' ''
Meta 'wrong-branch' 'dev' 'working' 'integration/other'
Meta 'work-parent' 'fix/task'
Meta 'build-parent' 'builds/demo'
Meta 'missing-parent' 'integration/missing'
Meta 'child-cancelled-parent' 'integration/cancelled'
Meta 'child-bad-grandparent' 'integration/missing-parent'
# private도 실제 중첩 저장소로 만들어 index/dirty/비추적 상태 보존을 검사한다.
$private = Join-Path $checkout 'Assets/_ThirdParty'
New-Item -ItemType Directory -Path $private -Force | Out-Null
& $git -C $private init -b main 2>$null | Out-Null
& $git -C $private config user.name 'Private Fixture'
& $git -C $private config user.email 'fixture@example.invalid'
& $git -C $private config commit.gpgsign false
Put 'Assets/_ThirdParty/asset.txt' '외부 에셋 원본'
& $git -C $private add asset.txt
& $git -C $private commit -m fixture 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'private fixture commit 실패' }
$privateSha = (& $git -C $private rev-parse HEAD).Trim()
Put '.gitmodules' "[submodule `"Assets/_ThirdParty`"]`n`tpath = Assets/_ThirdParty`n`turl = ./private.git"
Git add -- Scripts .github Harness Integrations kept.txt .gitmodules
Git update-index --add --cacheinfo "160000,$privateSha,Assets/_ThirdParty"
Git commit -m fixture | Out-Null
$sha = (Git rev-parse HEAD).Trim()
$names = @('parent', 'child', 'direct', 'cycle-a', 'cycle-b', 'self', 'cancelled', 'done', 'closed', 'unknown', 'no-status', 'no-base', 'wrong-branch', 'work-parent', 'build-parent', 'missing-parent', 'child-cancelled-parent', 'child-bad-grandparent', 'missing')
foreach ($name in $names) { Git branch "integration/$name" }
Git push origin --all | Out-Null
Git switch -c fix/unrelated 2>$null

# 운영 기능의 외부 경계만 대체한다. 실제 계보·템플릿·PR 판단 코드는 그대로 실행한다.
Add-Content -LiteralPath (Join-Path $checkout 'Scripts/Lib/common.ps1') -Encoding utf8NoBOM -Value @'
function Get-GhState {
    $config = Get-Content $env:SYNC_FIXTURE_CONFIG -Raw | ConvertFrom-Json
    return [pscustomobject]@{ State = $(if ($config.AuthFail) { 'NotLoggedIn' } else { 'Ready' }); Gh = (Join-Path $script:RepoRoot 'fake-gh.ps1'); Detail = 'fixture' }
}
function Get-GitHubRepo { [pscustomobject]@{ Slug = 'fixture/workspace'; Owner = 'fixture'; Name = 'workspace' } }
function Get-PmAccount { 'fixture-pm' }
$script:FixtureGit = (Get-Command git -CommandType Application).Source
function git {
    $argv = @($args)
    Add-Content -LiteralPath $env:SYNC_FIXTURE_GIT_LOG -Value (ConvertTo-Json -InputObject $argv -Compress)
    $config = Get-Content $env:SYNC_FIXTURE_CONFIG -Raw | ConvertFrom-Json
    if ($config.FetchFail -and $argv -contains 'fetch') { $global:LASTEXITCODE = 17; return }
    if ($argv -contains 'ls-remote') {
        $count = @(Get-Content $env:SYNC_FIXTURE_GIT_LOG | Where-Object { $_ -match '"ls-remote"' }).Count
        if ($config.TipFail) { $global:LASTEXITCODE = 18; return }
        if ($config.TipChangeAt -and $count -ge $config.TipChangeAt) {
            $result = @(& $script:FixtureGit @argv)
            if ($LASTEXITCODE -ne 0) { return }
            $result | ForEach-Object { $_ -replace ('^' + $config.Sha), ('f' * 40) }
            $global:LASTEXITCODE = 0
            return
        }
    }
    & $script:FixtureGit @argv
    $global:LASTEXITCODE = $LASTEXITCODE
}
'@
Put 'fake-gh.ps1' @'
$ErrorActionPreference = 'Stop'
$a = @($args)
$c = Get-Content $env:SYNC_FIXTURE_CONFIG -Raw | ConvertFrom-Json
$call = @{ args = $a }
$post = $a -contains 'POST'
if ($post) { $call.body = ($input | Out-String) | ConvertFrom-Json }
Add-Content -LiteralPath $env:SYNC_FIXTURE_GH_LOG -Value ($call | ConvertTo-Json -Depth 12 -Compress)
function Json($value) { ConvertTo-Json -InputObject $value -Depth 12 -Compress }
function Pr($number = 7) {
    return @{
        number = $number; state = 'open'; html_url = "https://github.com/fixture/workspace/pull/$number"
        head = @{ ref = $c.Parent; sha = $c.Sha; repo = @{ id = 42; full_name = 'fixture/workspace' } }
        base = @{ ref = $c.Child; sha = $c.Sha; repo = @{ id = 42; full_name = 'fixture/workspace' } }
    }
}
$endpoint = $a[1]
if ($c.ApiFail -and $endpoint -like $c.ApiFail) { exit 19 }
if ($c.BadJson -and $endpoint -like $c.BadJson) { '{bad'; exit 0 }
switch -Wildcard ($endpoint) {
    'user' { Json @{ login = $(if ($c.NotPm) { 'other' } else { 'fixture-pm' }) }; exit 0 }
    'repos/fixture/workspace' { Json @{ id = 42; full_name = $(if ($c.WrongRepo) { 'other/repo' } else { 'fixture/workspace' }) }; exit 0 }
    'repos/fixture/workspace/compare/*' { Json @{ status = $c.Compare }; exit 0 }
    'repos/fixture/workspace/pulls/*' {
        $pr = Pr
        switch ($c.FreshChange) {
            'head' { $pr.head.ref = 'dev' }
            'base' { $pr.base.ref = 'dev' }
            'state' { $pr.state = 'closed' }
            'repo' { $pr.head.repo.id = 99 }
            'sha' { $pr.head.sha = 'f' * 40 }
            'base-sha' { $pr.base.sha = 'f' * 40 }
            'url' { $pr.html_url = 'https://example.invalid/7' }
            'number' { $pr.number = 9 }
        }
        Json $pr; exit 0
    }
    'repos/fixture/workspace/pulls' {
        if ($post) {
            if ($c.CreateFail -or $c.Existing -eq 'race') { exit 20 }
            if ($c.CreateMalformed) { Json @{}; exit 0 }
            Json (Pr); exit 0
        }
        $calls = @(Get-Content $env:SYNC_FIXTURE_GH_LOG | ForEach-Object { $_ | ConvertFrom-Json } | Where-Object { $_.args[1] -eq $endpoint -and $_.args -notcontains 'POST' }).Count
        if ($c.ListMalformed) { '{"not":"array"}'; exit 0 }
        if ($c.ListEntryMalformed) { '[[{}]]'; exit 0 }
        $rows = @()
        if ($c.Existing -eq 'same' -or ($c.Existing -eq 'race' -and $calls -gt 1)) { $rows += (Pr) }
        if ($c.Existing -eq 'closed') { $pr = Pr; $pr.state = 'closed'; $rows += $pr }
        if ($c.Existing -eq 'fork') { $pr = Pr; $pr.head.repo.id = 99; $pr.head.repo.full_name = 'fork/workspace'; $rows += $pr }
        if ($c.Existing -eq 'wrong-pair') { $pr = Pr; $pr.base.ref = 'integration/other'; $rows += $pr }
        if ($c.Existing -eq 'duplicate') { $rows += (Pr); $rows += (Pr 8) }
        # --paginate --slurp와 같은 배열의 배열. 빈 첫 페이지도 소실시키지 않는다.
        if ($c.SecondPage) { '[[],' + (Json $rows) + ']' } else { '[' + (Json $rows) + ']' }
        exit 0
    }
    default { throw "예상하지 않은 gh 호출: $a" }
}
'@
Put 'kept.txt' 'stage된 사용자 내용'
Git add kept.txt
Put 'kept.txt' 'stage 이후 사용자 수정'
Put 'untracked.txt' '사용자 비추적'
Put 'Assets/_ThirdParty/asset.txt' 'private staged'
& $git -C $private add asset.txt
Put 'Assets/_ThirdParty/asset.txt' 'private dirty'
Put 'Assets/_ThirdParty/untracked.txt' 'private 비추적'

function Snapshot {
    $publicIndex = (Git rev-parse --git-path index).Trim()
    if (-not [IO.Path]::IsPathRooted($publicIndex)) { $publicIndex = Join-Path $checkout $publicIndex }
    $privateIndex = (& $git -C $private rev-parse --git-path index).Trim()
    if (-not [IO.Path]::IsPathRooted($privateIndex)) { $privateIndex = Join-Path $private $privateIndex }
    $files = @(Get-ChildItem -LiteralPath $checkout -Recurse -File -Force | Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' } | Sort-Object FullName | ForEach-Object { $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash })
    return (@((Git rev-parse HEAD), (& $git -C $private rev-parse HEAD), (Get-FileHash $publicIndex).Hash, (Get-FileHash $privateIndex).Hash) + $files) -join "`n"
}
$before = Snapshot
$configPath = Join-Path $root 'config.json'
$ghLog = Join-Path $root 'gh.jsonl'
$gitLog = Join-Path $root 'git.jsonl'
$env:SYNC_FIXTURE_CONFIG = $configPath
$env:SYNC_FIXTURE_GH_LOG = $ghLog
$env:SYNC_FIXTURE_GIT_LOG = $gitLog
function Run {
    param([hashtable]$Options = @{}, [string[]]$Cli = @(), [string]$Expect = 'Created', [string]$Failure, [int]$Writes = 1)
    $config = @{ Sha = $sha; Child = 'integration/child'; Parent = 'integration/parent'; Compare = 'ahead' }
    foreach ($key in $Options.Keys) { $config[$key] = $Options[$key] }
    Set-Content $configPath ($config | ConvertTo-Json) -Encoding utf8NoBOM
    Set-Content $ghLog '' -NoNewline
    Set-Content $gitLog '' -NoNewline
    $output = @(& $pwsh -NoProfile -File (Join-Path $checkout 'Scripts/sync-integration.ps1') -Child $config.Child @Cli 2>&1)
    $exit = $LASTEXITCODE
    $text = $output -join "`n"
    if ($Failure) { Assert ($exit -ne 0 -and $text -match $Failure) "예상 차단 없음($Failure): $text" }
    else { Assert ($exit -eq 0 -and $text -match $Expect) "예상 결과 없음($Expect): $text" }
    $calls = @(Get-Content $ghLog | Where-Object { $_ } | ForEach-Object { $_ | ConvertFrom-Json })
    $posts = @($calls | Where-Object { $_.args -contains 'POST' })
    Assert ($posts.Count -eq $Writes) "외부 쓰기 횟수 불일치: $($posts.Count) / $Writes"
    foreach ($call in $calls) {
        Assert ($call.args[0] -eq 'api') '허용하지 않은 gh 명령'
        Assert (-not ($call.args -match '^(merge|update-branch|--admin|--force|--auto|PATCH|PUT|DELETE)$')) '보호/쓰기 경계 위반'
    }
    foreach ($line in Get-Content $gitLog) {
        $argsList = $line | ConvertFrom-Json
        Assert (-not ($argsList -match '^(switch|checkout|merge|push|stash|reset|clean|submodule|lfs|--force|-f)$')) '사용자 Git 상태 변경 명령'
        if ($argsList -contains 'fetch') { Assert ($argsList -contains '--no-recurse-submodules' -and $argsList -contains 'submodule.recurse=false') '재귀 fetch 금지 누락' }
    }
    Assert ((Snapshot) -ceq $before) 'HEAD/index/사용자 파일/private가 바뀌었습니다.'
    $script:lastText = $text
    $script:lastPosts = $posts
}
try {
    Check '부모 통합 → 자식 생성, 다른 checkout·dirty 보존' { Run }
    Check '공통 템플릿·실행하지 않은 검사·Merge 요구' {
        $body = $script:lastPosts[0].body.body
        Assert ($body -match '## 📄 작업 내용 요약' -and $body -match '## 📎 관련 JIRA 이슈' -and $body -match '## 📎 Issue 번호' -and $body -match '## 🔎 검사 결과') '템플릿 섹션 누락'
        Assert ($body -match '컴파일: 미실행' -and $body -match '테스트: 미실행' -and $body -match 'Squash 금지' -and $body -match '필수 리뷰·CI') '검사/병합 안내 오류'
        Assert ($body -notmatch 'Co-Authored-By|Generated with') 'AI attribution 금지'
    }
    Check 'dev → 직접 자식 생성' { Run @{ Child = 'integration/direct'; Parent = 'dev' } }
    Check 'diverged 생성' { Run @{ Compare = 'diverged' } }
    foreach ($status in 'behind', 'identical') { Check "$status 변경 불필요" { Run @{ Compare = $status } -Expect UpToDate -Writes 0 } }
    Check '기존 PR 재사용·본문 변경 없음' { Run @{ Existing = 'same' } -Expect Reused -Writes 0 }
    Check '두 번째 페이지 PR 재사용' { Run @{ Existing = 'same'; SecondPage = $true } -Expect Reused -Writes 0 }
    foreach ($kind in 'closed', 'fork', 'wrong-pair') { Check "$kind PR 비재사용" { Run @{ Existing = $kind } } }
    Check '동시 생성 실패 후 정확한 PR 재사용' { Run @{ Existing = 'race' } -Expect Reused }
    Check '생성 실패 후 PR 없음은 실패' { Run @{ CreateFail = $true } -Failure 'GitHub API 실패' }
    Check '생성 응답 불량은 성공 아님' { Run @{ CreateMalformed = $true } -Failure 'PR 응답 필수 필드' }
    Check '생성 DryRun 쓰기 0회' { Run -Cli @('-DryRun') -Expect WouldCreate -Writes 0 }
    Check '재사용 DryRun 쓰기 0회' { Run @{ Existing = 'same' } -Cli @('-DryRun') -Expect Reused -Writes 0 }
    Check 'UpToDate DryRun 쓰기 0회' { Run @{ Compare = 'identical' } -Cli @('-DryRun') -Expect UpToDate -Writes 0 }
    Check '명시 부모 일치' { Run -Cli @('-Parent', 'integration/parent') }
    Check '명시 부모 불일치' { Run -Cli @('-Parent', 'dev') -Failure '선언된 부모와 불일치' -Writes 0 }
    Check '인증 실패' { Run @{ AuthFail = $true } -Failure 'gh가 필요' -Writes 0 }
    Check '다른 gh 계정 차단' { Run @{ NotPm = $true } -Failure 'PM의 gh 인증' -Writes 0 }
    Check '저장소 불일치 차단' { Run @{ WrongRepo = $true } -Failure '저장소가 일치하지' -Writes 0 }
    Check 'fetch 실패' { Run @{ FetchFail = $true } -Failure 'fetch 실패' -Writes 0 }
    Check 'tip 조회 실패' { Run @{ TipFail = $true } -Failure 'tip 재확인 실패' -Writes 0 }
    foreach ($endpoint in 'user', 'repos/fixture/workspace/pulls', 'repos/fixture/workspace/compare/*') {
        Check "API 실패 $endpoint" { Run @{ ApiFail = $endpoint } -Failure 'GitHub API 실패' -Writes 0 }
        Check "불량 JSON $endpoint" { Run @{ BadJson = $endpoint } -Failure 'JSON 오류' -Writes 0 }
    }
    Check '잘못된 목록 구조' { Run @{ ListMalformed = $true } -Failure '목록 응답 형식' -Writes 0 }
    Check '잘못된 PR 항목' { Run @{ ListEntryMalformed = $true } -Failure 'PR 응답 필수 필드' -Writes 0 }
    Check '중복 열린 PR 차단' { Run @{ Existing = 'duplicate' } -Failure '여러 개' -Writes 0 }
    Check '알 수 없는 compare 차단' { Run @{ Compare = 'unknown' } -Failure 'compare 응답' -Writes 0 }
    foreach ($name in 'cancelled', 'closed', 'done', 'unknown', 'no-status', 'child-cancelled-parent') {
        Check "비활성 상태 $name" { Run @{ Child = "integration/$name" } -Failure '활성 통합 상태' -Writes 0 }
    }
    foreach ($name in 'cycle-a', 'self') { Check "순환 $name" { Run @{ Child = "integration/$name" } -Failure '자기참조/순환' -Writes 0 } }
    Check 'Base 누락' { Run @{ Child = 'integration/no-base' } -Failure 'Base 누락' -Writes 0 }
    Check '메타 Branch 불일치' { Run @{ Child = 'integration/wrong-branch' } -Failure 'Branch 불일치' -Writes 0 }
    foreach ($name in 'work-parent', 'build-parent') { Check "허용하지 않는 계보 $name" { Run @{ Child = "integration/$name" } -Failure '허용하지 않는 부모' -Writes 0 } }
    foreach ($name in 'missing', 'missing-parent', 'child-bad-grandparent') { Check "메타 누락 $name" { Run @{ Child = "integration/$name" } -Failure 'Git 문서를 읽지' -Writes 0 } }
    Check '미리보기 전 tip 변경' { Run @{ TipChangeAt = 1 } -Cli @('-DryRun') -Failure '원격 tip 변경' -Writes 0 }
    Check '생성 직전 tip 변경' { Run @{ TipChangeAt = 2 } -Failure '원격 tip 변경' -Writes 0 }
    Check '생성 후 tip 변경은 재검토' { Run @{ TipChangeAt = 3 } -Failure '원격 tip 변경' }
    Check '재사용 tip 변경은 재검토' { Run @{ Existing = 'same'; TipChangeAt = 1 } -Failure '원격 tip 변경' -Writes 0 }
    foreach ($change in 'head', 'base', 'state', 'repo', 'sha', 'base-sha', 'url', 'number') {
        Check "PR 재검증 $change" { Run @{ Existing = 'same'; FreshChange = $change } -Failure '재검토 필요' -Writes 0 }
    }
    Check '재사용 상세 조회 실패' { Run @{ Existing = 'same'; ApiFail = 'repos/fixture/workspace/pulls/7' } -Failure 'GitHub API 실패' -Writes 0 }
    Write-Output "Integration sync: PASS ($passed checks). Fixture: $root"
} finally {
    Remove-Item Env:SYNC_FIXTURE_CONFIG, Env:SYNC_FIXTURE_GH_LOG, Env:SYNC_FIXTURE_GIT_LOG -ErrorAction SilentlyContinue
}
