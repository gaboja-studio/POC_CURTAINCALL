# 문서 크기 검사. 기준 숫자는 Harness/Core/Policies/context-budget.md의 표에서만 읽는다.
# 대상: AGENTS.md, CLAUDE.md, Harness/, Docs/(References 제외), Tasks/Active/, Integrations/Active/ 의 모든 .md (History/ 제외)
# hard 초과가 있으면 exit 1 (차단). -WarnOnly면 결과만 출력하고 exit 0.
[CmdletBinding()]
param(
    # 특정 Task 폴더만 검사한다(Task 문서만 대상).
    [string]$TaskPath,
    [switch]$WarnOnly
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$budgetFile = Get-RepoPath 'Harness/Core/Policies/context-budget.md'
$limits = @{}
foreach ($line in Get-Content -LiteralPath $budgetFile) {
    if ($line -match '^\|\s*`([^`]+)`\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|') {
        $limits[$Matches[1]] = @{ Warn = [int]$Matches[2]; Hard = [int]$Matches[3] }
    }
}
foreach ($required in 'default', 'knowledge') {
    if (-not $limits.ContainsKey($required)) { throw "context-budget.md에 '$required' 기준이 없음" }
}

function Get-Rule {
    # 파일 경로 → @{ Key; Warn; Hard; Kind(기록형/안내형/템플릿) }
    param([string]$Rel)
    $name = Split-Path $Rel -Leaf
    $rule = { param($k, $kind) $l = $limits[$k] ?? $limits['default']; @{ Key = $k; Warn = $l.Warn; Hard = $l.Hard; Kind = $kind } }

    if ($Rel -match '^Harness/Core/Templates/(Task|Integration|Knowledge)/') {
        $target = switch ($Matches[1]) { 'Task' { "task/$name" } 'Integration' { "integration/$name" } default { 'knowledge' } }
        $t = $limits[$target] ?? $limits['default']
        # 템플릿은 채워질 문서의 warn보다 짧아야 한다(넘으면 차단).
        return @{ Key = "template→$target"; Warn = $t.Warn; Hard = $t.Warn; Kind = '템플릿' }
    }
    if ($Rel -match '^Tasks/Active/[^/]+/[^/]+$') { return & $rule "task/$name" '기록형' }
    if ($Rel -match '^Integrations/Active/[^/]+/[^/]+$') { return & $rule "integration/$name" '기록형' }
    if ($name -eq 'index.md') { return & $rule 'index' '안내형' }
    if ($Rel -eq 'AGENTS.md') { return & $rule 'AGENTS.md' '안내형' }
    if ($Rel -match '^Docs/Guides/') { return & $rule 'guide' '안내형' }
    if ($Rel -match '^Docs/Domains/') { return & $rule 'domain' '안내형' }
    if ($Rel -match '/Skills/') { return & $rule 'skill' '안내형' }
    if ($Rel -match '/Policies/') { return & $rule 'policy' '안내형' }
    if ($Rel -match '/(Pitfalls|Decisions|Facts)/') { return & $rule 'knowledge' '안내형' }
    return & $rule 'default' '안내형'
}

$files = if ($TaskPath) {
    @(Get-ChildItem -LiteralPath (Resolve-Path -LiteralPath $TaskPath).Path -Filter '*.md' -File)
} else {
    $roots = 'Harness', 'Docs', 'Tasks/Active', 'Integrations/Active' | ForEach-Object { Get-RepoPath $_ } | Where-Object { Test-Path -LiteralPath $_ }
    @(Get-Item -LiteralPath (Get-RepoPath 'AGENTS.md'), (Get-RepoPath 'CLAUDE.md') -ErrorAction SilentlyContinue) +
    @($roots | ForEach-Object { Get-ChildItem -LiteralPath $_ -Filter '*.md' -File -Recurse })
}

$warnCount = 0
$hardCount = 0
$checked = 0
foreach ($file in $files) {
    $rel = [System.IO.Path]::GetRelativePath($RepoRoot, $file.FullName).Replace('\', '/')
    if ($rel -match '(^|/)History/' -or $rel -match '^Docs/References/') { continue }
    $checked++
    $r = Get-Rule $rel
    $lines = @(Get-Content -LiteralPath $file.FullName).Count
    $action = switch ($r.Kind) {
        '기록형' { '오래된 내용을 같은 폴더 History/로 이동' }
        '템플릿' { '템플릿 문장 줄이기' }
        default { '압축, 그래도 길면 하위 문서로 분리' }
    }
    if ($lines -gt $r.Hard) {
        $hardCount++
        Write-Output ("[HARD] {0} {1}줄 (hard {2}, {3}) → {4}" -f $rel, $lines, $r.Hard, $r.Key, $action)
    } elseif ($lines -gt $r.Warn) {
        $warnCount++
        Write-Output ("[WARN] {0} {1}줄 (warn {2}, {3}) → {4}" -f $rel, $lines, $r.Warn, $r.Key, $action)
    }
}

$result = if ($hardCount) { 'COMPACT_REQUIRED' } elseif ($warnCount) { 'WARN' } else { 'OK' }
Write-Output "Context: $result ($checked files, warn $warnCount, hard $hardCount)"
if ($hardCount -and -not $WarnOnly) {
    Write-Output '→ Harness/Core/Skills/compact-docs.md 절차로 줄인 뒤 다시 실행하세요.'
    exit 1
}
exit 0
