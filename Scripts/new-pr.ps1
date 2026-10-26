# .github/PULL_REQUEST_TEMPLATE.md를 채워 PR을 만든다.
#   gh가 준비되어 있으면 바로 등록, 아니면(-Browser 포함) 내용이 채워진 GitHub 작성 페이지를 연다.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Summary,
    # 이슈 번호. -Issues 9,10 / -Issues "9,10" / -Issues 9 10 모두 받는다(pwsh -File은 "9,10"을 문자열 하나로 넘긴다).
    [string[]]$Issues = @(),
    # PR 제목의 한 줄 요약. 생략하면 Task plan.md의 Title.
    [string]$Title,
    # 대상 브랜치. 생략하면 Task plan.md의 Base (Task 없는 fix/*·resource/*는 dev).
    [string]$Base,
    [string]$PlayCheck = '미실행',
    # 검사 결과 직접 지정. 생략하면 Task log.md의 최신 검사 결과를 쓴다.
    [string]$ScopeCheck,
    [string]$CompileCheck,
    [string]$TestCheck,
    [switch]$Push,
    [switch]$Browser,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$problems = [System.Collections.Generic.List[string]]::new()
$head = (& git -C $RepoRoot branch --show-current).Trim()
$task = Find-CurrentTask
$repo = Get-GitHubRepo

# 1) 대상 브랜치와 제목

if ($task) {
    $meta = Get-TaskPlanPath $task.Path
    if (-not $Base) { $Base = Get-MetaField -MetaPath $meta -Field 'Base' }
    if (-not $Title) { $Title = Get-MetaField -MetaPath $meta -Field 'Title' }
    $prTitle = "[$($task.Type)] $($task.Id) $Title"
} else {
    if (-not $Base) { $Base = Get-BaseBranch }
    if (-not $Base -or -not $Title) { throw "현재 브랜치($head)에 대응하는 Task가 없음. -Base와 -Title을 직접 지정하세요." }
    $kind = switch -Regex ($head) { '^integration/' { 'integration' } '^dev$' { if ($Base -like 'integration/*') { 'sync' } else { 'build' } } default { ($head -split '/')[0] } }
    $prTitle = "[$kind] $Title"
}

if (-not $Base) { $Base=Get-BaseBranch }
$mergeMethod = Get-PrFlow -Head $head -Base $Base
if ($mergeMethod -eq 'merge') { $prTitle = $prTitle -replace '^\[integration\]', '[integration/sync]' }
if (-not $Title) { $problems.Add('PR 제목 요약이 비어 있음 (plan.md Title 또는 -Title)') }

# 2) 본문: 템플릿을 제목(## ...)별로 채운다
$verification = @{}
if ($task) {
    $log = Get-Content -LiteralPath (Join-Path $task.Path 'log.md') -Raw
    $latest = @($log -split '\r?\n' | Where-Object { $_ -match ' 검사: ' } | Select-Object -Last 1)
    foreach ($m in [regex]::Matches(($latest -join ''), '(안전성|문서/규칙|컴파일|테스트)=([^;\r\n]+)')) {
        $verification[$m.Groups[1].Value] = $m.Groups[2].Value.Trim()
    }
}
function Get-Check { param([string]$Label)
    $key = $verification.Keys | Where-Object { $_ -eq $Label } | Select-Object -First 1
    if ($key -and $verification[$key]) { return $verification[$key] }
    return '미실행'
}

$jira = '없음'
if ($task) {
    $raw = Get-MarkdownSection -Path (Join-Path $task.Path 'plan.md') -Heading 'JIRA'
    $keys = @([regex]::Matches("$raw", '\b[A-Z][A-Z0-9]+-\d+\b') | ForEach-Object Value | Sort-Object -Unique)
    if ($keys.Count) { $jira = $keys -join ', ' }
}
$Issues = @($Issues | ForEach-Object { $_ -split '[,\s]+' } | Where-Object { $_ } | ForEach-Object {
    $n = 0
    if (-not [int]::TryParse(($_ -replace '^#', ''), [ref]$n) -or $n -le 0) { throw "이슈 번호가 아님: $_" }
    $n
})
$issueText = if ($Issues.Count) { ($Issues | ForEach-Object { "closed #$_" }) -join "`n" } else { '없음' }

$noTaskScope = $null
$checks = @{
    ScopeCheck = $(if ($ScopeCheck) { $ScopeCheck } elseif ($noTaskScope) { $noTaskScope } elseif (-not $task) { '기록 참조' } else { Get-Check '안전성' })
    CompileCheck = $(if ($CompileCheck) { $CompileCheck } else { Get-Check '컴파일' })
    TestCheck = $(if ($TestCheck) { $TestCheck } else { Get-Check '테스트' })
    PlayCheck = $PlayCheck
}
$mergeNote = if ($mergeMethod -eq 'merge') { '병합 방식: Merge commit. 선택한 방향으로 공유합니다.' } else { '병합 방식: Squash. dev 반영 전 지정 팀원이 검토합니다.' }
$bodyText = New-PrTemplateBody -Summary $Summary -Jira $jira -Issues $issueText @checks -MergeNote $mergeNote

# 3) 원격 상태 확인
$dirty = & git -C $RepoRoot status --porcelain --ignore-submodules=all
if ($dirty) { $problems.Add('저장(커밋)하지 않은 변경이 있음. 먼저 "저장해줘"') }
& git -C $RepoRoot ls-remote --exit-code --heads origin $Base *> $null
if ($LASTEXITCODE -ne 0) { $problems.Add("대상 브랜치가 원격에 없음: $Base (PM 확인 필요)") }
$remoteHead = (& git -C $RepoRoot ls-remote --heads origin $head) -split '\s+' | Select-Object -First 1
$localHead = (& git -C $RepoRoot rev-parse HEAD).Trim()
$needPush = $remoteHead -ne $localHead
if ($needPush -and -not $Push) { $problems.Add('내 브랜치의 최신 저장본이 원격에 없음 (-Push로 올리기)') }

$gh = Get-GhState
if ($gh.State -eq 'Ready') {
    $existing = (& $gh.Gh pr list --repo $repo.Slug --head $head --base $Base --state open --json url --jq '.[0].url' 2>$null)
    if ($existing) { $problems.Add("이미 열린 PR이 있음: $existing") }
}

$compareUrl = "https://github.com/$($repo.Slug)/compare/${Base}...${head}" +
    "?expand=1&title=$([uri]::EscapeDataString($prTitle))&body=$([uri]::EscapeDataString($bodyText))"

Write-Output "제목: $prTitle"
Write-Output "방향: $head → $Base"
Write-Output "병합 방식: $(if ($mergeMethod -eq 'merge') { 'Merge commit (Squash 금지)' } else { 'Squash' })"
Write-Output "등록 방식: $(if ($Browser -or $gh.State -ne 'Ready') { "브라우저 ($($gh.Detail))" } else { 'gh 자동 등록' })"
Write-Output '----- 본문 -----'
Write-Output $bodyText
Write-Output '----------------'
$problems | ForEach-Object { Write-Output "[문제] $_" }

if ($DryRun) {
    Write-Output "웹페이지 방식 주소: $compareUrl"
    Write-Output '미리보기만 했습니다(-DryRun). 실제로 등록하지 않았습니다.'
    exit ($problems.Count ? 1 : 0)
}
$blocking = @($problems | Where-Object { $_ -notlike '*원격에 없음 (-Push*' })
if ($blocking.Count) { throw "등록 중단: $($blocking -join ' / ')" }

if ($needPush -and $Push) {
    & git -C $RepoRoot push -u origin $head
    if ($LASTEXITCODE -ne 0) { throw 'git push 실패' }
}

$prUrl = $null
if ($Browser -or $gh.State -ne 'Ready') {
    if ($compareUrl.Length -gt 7000) {
        $bodyFile = Join-Path (Get-RepoPath '.harness/local') 'pr-body.md'
        New-Item -ItemType Directory -Path (Split-Path $bodyFile) -Force | Out-Null
        Set-Content -LiteralPath $bodyFile -Value $bodyText -Encoding utf8NoBOM
        try { Set-Clipboard -Value $bodyText } catch { }
        $compareUrl = "https://github.com/$($repo.Slug)/compare/${Base}...${head}?expand=1&title=$([uri]::EscapeDataString($prTitle))"
        Write-Output "본문이 길어 클립보드에 복사했습니다(파일: $bodyFile). 페이지의 본문 칸에 붙여 넣으세요."
    }
    Open-Url $compareUrl
    Write-Output "브라우저에서 PR 작성 페이지를 열었습니다. 내용 확인 후 'Create pull request'를 눌러 주세요."
    Write-Output $compareUrl
} else {
    $tmp = New-TemporaryFile
    Set-Content -LiteralPath $tmp -Value $bodyText -Encoding utf8NoBOM
    $output = & $gh.Gh pr create --repo $repo.Slug --base $Base --head $head --title $prTitle --body-file $tmp
    $code = $LASTEXITCODE
    Remove-Item -LiteralPath $tmp
    if ($code -ne 0) { throw 'gh pr create 실패' }
    $prUrl = "$output".Trim()
    Write-Output "PR 등록 완료: $prUrl"
}

# 실제 생성된 PR 링크만 log에 기록한다. 중간 Status는 없다.
if ($task -and $prUrl) {
    Add-TaskLog -TaskPath $task.Path -Text "- $(Get-Date -Format yyyy-MM-dd) PR: $prUrl ($head → $Base)"
}
