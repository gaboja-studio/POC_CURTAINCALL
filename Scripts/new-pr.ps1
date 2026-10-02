# .github/PULL_REQUEST_TEMPLATE.md를 채워 PR을 만든다.
#   gh가 준비되어 있으면 바로 등록, 아니면(-Browser 포함) 내용이 채워진 GitHub 작성 페이지를 연다.
#   브랜치 흐름: feat|refactor/* → integration/* → dev → builds/*
#               fix|resource/* → dev 또는 integration/*  (builds/* 직행 불가, tests/*는 제출 불가)
# 채우는 값: 요약(-Summary), JIRA(Task setup.md), Issue 번호(-Issues), 검사 결과(Task handoff.md)
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Summary,
    [int[]]$Issues = @(),
    # PR 제목의 한 줄 요약. 생략하면 Task meta.md의 Title.
    [string]$Title,
    # 대상 브랜치. 생략하면 Task meta.md의 Integration (Task 없는 fix/*·resource/*는 dev).
    [string]$Base,
    [string]$PlayCheck = '미실행',
    # 검사 결과 칸 직접 지정(Task가 없는 통합 PR 등). 생략하면 Task handoff.md의 Verification을 쓴다.
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
    $meta = Join-Path $task.Path 'meta.md'
    if (-not $Base) { $Base = Get-MetaField -MetaPath $meta -Field 'Integration' }
    if (-not $Title) { $Title = Get-MetaField -MetaPath $meta -Field 'Title' }
    $prTitle = "[$($task.Type)] $($task.Id) $Title"
} else {
    if (-not $Base -and $head -match '^(fix|resource)/') { $Base = Get-BaseBranch }
    if (-not $Base -or -not $Title) { throw "현재 브랜치($head)에 대응하는 Task가 없음. -Base와 -Title을 직접 지정하세요." }
    $kind = switch -Regex ($head) { '^integration/' { 'integration' } '^dev$' { 'build' } default { ($head -split '/')[0] } }
    $prTitle = "[$kind] $Title"
}

$allowed = switch -Regex ($head) {
    '^(feat|refactor)/' { '^integration/.+' }
    '^(fix|resource)/' { '^(dev|integration/.+)$' }
    '^integration/' { '^dev$' }
    '^dev$' { '^builds/.+' }
    default { $null }
}
if (-not $allowed) { throw "이 브랜치($head)는 PR을 만들 수 없음. tests/* 등은 합치지 않는 브랜치입니다." }
if (-not $Base -or $Base -notmatch $allowed) {
    throw "대상 브랜치 규칙 위반: $head → '$Base' (허용: $allowed). Harness/Core/Policies/git-workflow.md 참고."
}
if (-not $Title) { $problems.Add('PR 제목 요약이 비어 있음 (meta.md Title 또는 -Title)') }

# 2) 본문: 템플릿을 제목(## ...)별로 채운다
$verification = @{}
if ($task) {
    $section = Get-MarkdownSection -Path (Join-Path $task.Path 'handoff.md') -Heading 'Verification'
    foreach ($line in ($section -split "`n")) {
        if ($line -match '^\s*-\s*(.+?):\s*(.*)$') { $verification[$Matches[1].Trim()] = $Matches[2].Trim() }
    }
}
function Get-Check { param([string]$Label)
    $key = $verification.Keys | Where-Object { $_ -eq $Label } | Select-Object -First 1
    if ($key -and $verification[$key]) { return $verification[$key] }
    return '미실행'
}

$jira = '없음'
if ($task) {
    $raw = Get-MarkdownSection -Path (Join-Path $task.Path 'setup.md') -Heading 'JIRA'
    $keys = @([regex]::Matches("$raw", '\b[A-Z][A-Z0-9]+-\d+\b') | ForEach-Object Value | Sort-Object -Unique)
    if ($keys.Count) { $jira = $keys -join ', ' }
}
$issueText = if ($Issues.Count) { ($Issues | ForEach-Object { "closed #$_" }) -join "`n" } else { '없음' }

# Task 없는 fix/*·resource/*는 handoff.md가 없으므로 구역 검사를 여기서 돌려 결과를 적는다.
$noTaskScope = $null
if (-not $task -and -not $ScopeCheck -and $head -match '^(fix|resource)/') {
    $scopeOut = @(& ([System.Environment]::ProcessPath) -NoProfile -File (Join-Path $PSScriptRoot 'verify-scope.ps1') -Branch $head -Base "origin/$Base" 2>&1)
    $noTaskScope = if ($LASTEXITCODE -eq 0) { "PASS ($(Get-Date -Format 'MM-dd'), Task 없음)" } else { 'FAIL — 구역 검사 실패' }
    if ($LASTEXITCODE -ne 0) { $scopeOut | ForEach-Object { Write-Output $_ }; $problems.Add('구역 검사 실패 (위 [위반] 항목 확인)') }
}

$template = Get-Content -LiteralPath (Get-RepoPath '.github/PULL_REQUEST_TEMPLATE.md') -Raw
$parts = [regex]::Split($template, '(?m)^(?=## )')
$body = [System.Text.StringBuilder]::new()
foreach ($part in $parts) {
    if ($part -notmatch '^## (.+)') { [void]$body.Append($part); continue }
    $heading = $Matches[1].Trim()
    $value = switch -Regex ($heading) {
        '요약' { $Summary }
        'JIRA' { $jira }
        'Issue' { $issueText }
        '검사' {
            $lines = foreach ($l in ($part -split "`n")) {
                if ($l -notmatch '^\s*-\s*([^(:]+)') { continue }
                $label = $Matches[1].Trim()
                $v = switch -Regex ($label) {
                    '구역' { if ($ScopeCheck) { $ScopeCheck } elseif ($noTaskScope) { $noTaskScope } elseif (-not $task) { '해당 없음 (Task 없는 PM 브랜치)' } else { Get-Check '구역 검사' } }
                    '컴파일' { if ($CompileCheck) { $CompileCheck } else { Get-Check '2 컴파일' } }
                    '테스트' { if ($TestCheck) { $TestCheck } else { Get-Check '3 테스트' } }
                    '플레이' { $PlayCheck }
                    default { '미실행' }
                }
                "- ${label}: $v"
            }
            $lines -join "`n"
        }
        default { $null }
    }
    if ($null -eq $value) { [void]$body.Append($part) }
    else { [void]$body.Append("## $heading`n`n$value`n`n") }
}
$bodyText = $body.ToString().Trim() + "`n"

# 3) 원격 상태 확인
$dirty = & git -C $RepoRoot status --porcelain
if ($dirty) { $problems.Add('저장(커밋)하지 않은 변경이 있음. 먼저 "저장해줘"') }
& git -C $RepoRoot ls-remote --exit-code --heads origin $Base *> $null
if ($LASTEXITCODE -ne 0) { $problems.Add("대상 브랜치가 원격에 없음: $Base (PM 확인 필요)") }
$remoteHead = (& git -C $RepoRoot ls-remote --heads origin $head) -split '\s+' | Select-Object -First 1
$localHead = (& git -C $RepoRoot rev-parse HEAD).Trim()
$needPush = $remoteHead -ne $localHead
if ($needPush -and -not $Push) { $problems.Add('내 브랜치의 최신 저장본이 원격에 없음 (-Push로 올리기)') }

$gh = Get-GhState
if ($gh.State -eq 'Ready') {
    $existing = (& $gh.Gh pr list --repo $repo.Slug --head $head --state open --json url --jq '.[0].url' 2>$null)
    if ($existing) { $problems.Add("이미 열린 PR이 있음: $existing") }
}

$compareUrl = "https://github.com/$($repo.Slug)/compare/${Base}...${head}" +
    "?expand=1&title=$([uri]::EscapeDataString($prTitle))&body=$([uri]::EscapeDataString($bodyText))"

Write-Output "제목: $prTitle"
Write-Output "방향: $head → $Base"
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

# 4) Task 문서 갱신 (gh로 등록해 URL을 아는 경우만. 브라우저 방식은 등록 후 AI가 갱신)
if ($task -and $prUrl) {
    $handoff = Join-Path $task.Path 'handoff.md'
    $text = [regex]::Replace((Get-Content -LiteralPath $handoff -Raw), '(?m)^- PR:.*$', "- PR: $prUrl")
    Set-Content -LiteralPath $handoff -Value $text -Encoding utf8NoBOM -NoNewline
    $metaText = [regex]::Replace((Get-Content -LiteralPath $meta -Raw), '(?m)^(- \*\*Status:\*\*).*$', '$1 submitted')
    Set-Content -LiteralPath $meta -Value $metaText -Encoding utf8NoBOM -NoNewline
    Write-Output 'handoff.md에 PR 링크, meta.md Status를 submitted로 기록했습니다. (이 변경은 다음 저장 때 함께 올라갑니다)'
}
