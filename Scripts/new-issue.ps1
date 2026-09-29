# .github/ISSUE_TEMPLATE/*.yml 입력 폼 정의대로 이슈를 만든다.
#   -List로 사용 가능한 종류와 칸(id)을 확인한다. 필수 칸이 비면 등록하지 않는다.
#   gh가 준비되어 있으면 바로 등록, 아니면(-Browser 포함) 칸이 채워진 GitHub 이슈 폼을 연다.
[CmdletBinding()]
param(
    # 템플릿 파일 이름(대소문자 무시, 앞부분만 써도 됨): bug, qa, qa_request
    [string]$Type,
    [string]$Title,
    # 칸 id → 내용. 예: @{ description = '...'; situation = "1. ...`n2. ..." }
    [hashtable]$Fields = @{},
    # 위와 같은 내용의 JSON 파일 경로 (여러 줄 내용을 넘길 때 편함)
    [string]$FieldsFile,
    [string[]]$Assignee = @(),
    [switch]$List,
    [switch]$Browser,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

function Read-IssueForm {
    param([System.IO.FileInfo]$File)
    $form = [ordered]@{ File = $File.Name; Key = $File.BaseName.ToLower(); Name = ''; TitlePrefix = ''; Labels = @(); Fields = [System.Collections.Generic.List[object]]::new() }
    $inBody = $false
    $field = $null
    foreach ($line in Get-Content -LiteralPath $File.FullName) {
        $unquoted = { param($s) $s.Trim().Trim('"').Trim("'") }
        if (-not $inBody) {
            if ($line -match '^name:\s*(.+)$') { $form.Name = & $unquoted $Matches[1] }
            elseif ($line -match '^title:\s*(.+)$') { $form.TitlePrefix = & $unquoted $Matches[1] }
            elseif ($line -match '^labels:\s*\[(.*)\]') { $form.Labels = @($Matches[1] -split ',' | ForEach-Object { & $unquoted $_ } | Where-Object { $_ }) }
            elseif ($line -match '^body:') { $inBody = $true }
            continue
        }
        if ($line -match '^\s*-\s*type:\s*(\S+)') {
            $field = [pscustomobject]@{ Type = $Matches[1]; Id = ''; Label = ''; Required = $false }
            if ($field.Type -ne 'markdown') { $form.Fields.Add($field) }
        } elseif ($field -and $line -match '^\s+id:\s*(\S+)') { $field.Id = $Matches[1] }
        elseif ($field -and -not $field.Label -and $line -match '^\s+label:\s*(.+)$') { $field.Label = & $unquoted $Matches[1] }
        elseif ($field -and $line -match '^\s+required:\s*true') { $field.Required = $true }
    }
    return [pscustomobject]$form
}

$forms = @(Get-ChildItem -LiteralPath (Get-RepoPath '.github/ISSUE_TEMPLATE') -File |
    Where-Object { $_.Extension -in '.yml', '.yaml' -and $_.BaseName -ne 'config' } |
    ForEach-Object { Read-IssueForm $_ })

if ($List -or -not $Type) {
    foreach ($f in $forms) {
        Write-Output "[$($f.Key)] $($f.Name)  제목 접두어 '$($f.TitlePrefix)'  라벨: $($f.Labels -join ', ')"
        foreach ($x in $f.Fields) { Write-Output ("    {0,-12} {1}{2}" -f $x.Id, $x.Label, ($x.Required ? '  (필수)' : '')) }
    }
    exit 0
}

$form = @($forms | Where-Object { $_.Key -eq $Type.ToLower() })
if (-not $form) { $form = @($forms | Where-Object { $_.Key.StartsWith($Type.ToLower()) }) }
if ($form.Count -ne 1) { throw "이슈 종류 '$Type'를 하나로 정할 수 없음. -List로 확인하세요." }
$form = $form[0]
if (-not $Title) { throw '-Title이 필요함' }

if ($FieldsFile) {
    $json = Get-Content -LiteralPath $FieldsFile -Raw | ConvertFrom-Json -AsHashtable
    foreach ($k in $json.Keys) { $Fields[$k] = $json[$k] }
}

$problems = [System.Collections.Generic.List[string]]::new()
$known = @($form.Fields.Id)
foreach ($k in $Fields.Keys) { if ($known -notcontains $k) { $problems.Add("알 수 없는 칸 id: $k (가능: $($known -join ', '))") } }
foreach ($x in $form.Fields | Where-Object Required) {
    if (-not "$($Fields[$x.Id])".Trim()) { $problems.Add("필수 칸이 비어 있음: $($x.Label) (id: $($x.Id))") }
}

$prefix = $form.TitlePrefix.Trim()
$fullTitle = if ($prefix -and -not $Title.StartsWith($prefix)) { "$prefix $Title" } else { $Title }
$sections = foreach ($x in $form.Fields) {
    $v = "$($Fields[$x.Id])".Trim()
    "### $($x.Label)`n`n$(if ($v) { $v } else { '_No response_' })"
}
$bodyText = ($sections -join "`n`n") + "`n"

$repo = Get-GitHubRepo
$gh = Get-GhState
$labels = $form.Labels
if ($gh.State -eq 'Ready' -and $labels.Count) {
    $existing = @(& $gh.Gh label list --repo $repo.Slug --limit 300 --json name --jq '.[].name')
    $missing = @($labels | Where-Object { $existing -notcontains $_ })
    if ($missing.Count) {
        Write-Output "[경고] 저장소에 없는 라벨은 빼고 등록합니다: $($missing -join ', ') (PM이 라벨 생성 필요)"
        $labels = @($labels | Where-Object { $existing -contains $_ })
    }
}

$query = [System.Collections.Generic.List[string]]::new()
$query.Add("template=$([uri]::EscapeDataString($form.File))")
$query.Add("title=$([uri]::EscapeDataString($fullTitle))")
foreach ($x in $form.Fields | Where-Object Id) {
    $v = "$($Fields[$x.Id])".Trim()
    if ($v) { $query.Add("$($x.Id)=$([uri]::EscapeDataString($v))") }
}
$formUrl = "https://github.com/$($repo.Slug)/issues/new?" + ($query -join '&')

Write-Output "종류: $($form.Name)"
Write-Output "제목: $fullTitle"
Write-Output "라벨: $($labels -join ', ')"
Write-Output "등록 방식: $(if ($Browser -or $gh.State -ne 'Ready') { "브라우저 ($($gh.Detail))" } else { 'gh 자동 등록' })"
Write-Output '----- 본문 -----'
Write-Output $bodyText
Write-Output '----------------'
$problems | ForEach-Object { Write-Output "[문제] $_" }

if ($DryRun) {
    Write-Output "웹페이지 방식 주소: $formUrl"
    Write-Output '미리보기만 했습니다(-DryRun). 실제로 등록하지 않았습니다.'
    exit ($problems.Count ? 1 : 0)
}
if ($problems.Count) { throw "등록 중단: $($problems -join ' / ')" }

if ($Browser -or $gh.State -ne 'Ready') {
    Open-Url $formUrl
    Write-Output "브라우저에서 이슈 작성 폼을 열었습니다. 내용 확인 후 'Create'를 눌러 주세요."
    Write-Output $formUrl
    exit 0
}

$tmp = New-TemporaryFile
Set-Content -LiteralPath $tmp -Value $bodyText -Encoding utf8NoBOM
$ghArgs = @('issue', 'create', '--repo', $repo.Slug, '--title', $fullTitle, '--body-file', $tmp.FullName)
foreach ($l in $labels) { $ghArgs += @('--label', $l) }
foreach ($a in $Assignee) { $ghArgs += @('--assignee', $a) }
$output = & $gh.Gh @ghArgs
$code = $LASTEXITCODE
Remove-Item -LiteralPath $tmp
if ($code -ne 0) { throw 'gh issue create 실패' }
Write-Output "이슈 등록 완료: $("$output".Trim())"
