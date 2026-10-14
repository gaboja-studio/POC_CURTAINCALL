# 하네스 구조 검사: 필수 파일, 이름 규칙(폴더 첫 글자 대문자 / 파일 소문자), Skill frontmatter, .env 규칙.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$failures = [System.Collections.Generic.List[string]]::new()

$requiredFiles = @(
    'AGENTS.md', 'CLAUDE.md', '.env.example', '.mcp.json',
    'Harness/Core/Policies/context-budget.md', 'Harness/Core/Policies/knowledge-routing.md',
    'Harness/Core/Templates/Task/meta.md', 'Harness/Core/Templates/Task/plan.md',
    'Harness/Core/Templates/Task/todo.md', 'Harness/Core/Templates/Task/handoff.md',
    'Harness/Core/Templates/Task/log.md',
    'Harness/Engine/Unity/Decisions/version.md', 'Harness/Project/Facts/repository.md',
    'Docs/Domains/index.md', '.github/CODEOWNERS', '.github/PULL_REQUEST_TEMPLATE.md',
    'Harness/Core/Templates/Task/setup.md', 'Harness/Project/Facts/team.md', '.githooks/commit-msg'
)
foreach ($rel in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Get-RepoPath $rel) -PathType Leaf)) { $failures.Add("필수 파일 없음: $rel") }
}

# 이름 규칙 (AGENTS.md "이름 규칙")
$folderPattern = '^[A-Z][a-z0-9]*(?:-[a-z0-9]+)*$'
$filePattern = '^(?:[a-z0-9]+(?:[-.][a-z0-9]+)*|\.gitkeep)$'
foreach ($top in 'Harness', 'Docs', 'Tasks', 'Integrations', 'Scripts') {
    $root = Get-RepoPath $top
    if (-not (Test-Path -LiteralPath $root)) { continue }
    foreach ($item in Get-ChildItem -LiteralPath $root -Recurse -Force) {
        $rel = [System.IO.Path]::GetRelativePath($RepoRoot, $item.FullName)
        if ($rel -match '^Docs[\\/]References[\\/]') { continue }  # 외부 원본 자료는 이름 유지
        if ($rel -match '^Harness[\\/]Mods[\\/][^\\/]+[\\/]') { continue }  # 모드 내부는 Claude Code가 정한 이름(.claude-plugin, hooks)
        if ($item.PSIsContainer) {
            if ($item.Name -cnotmatch $folderPattern) { $failures.Add("폴더명 규칙 위반(첫 글자만 대문자): $rel") }
        } elseif ($item.Name -cnotmatch $filePattern -and $item.Name -ne '.DS_Store') {
            $failures.Add("파일명 규칙 위반(모두 소문자): $rel")
        }
    }
}

# Skill frontmatter: name은 파일명과 같아야 한다(.claude/skills 어댑터 이름).
foreach ($skill in Get-ChildItem -LiteralPath (Get-RepoPath 'Harness') -Filter '*.md' -File -Recurse |
         Where-Object { $_.FullName -match '[\\/]Skills[\\/]' }) {
    $text = Get-Content -LiteralPath $skill.FullName -Raw
    $rel = [System.IO.Path]::GetRelativePath($RepoRoot, $skill.FullName)
    if ($text -notmatch '(?s)^---\r?\nname: ([a-z0-9-]+)\r?\ndescription: [^"''].+?\r?\n---') {
        $failures.Add("Skill frontmatter 형식 오류: $rel")
    } elseif ($Matches[1] -ne $skill.BaseName) {
        $failures.Add("Skill name($($Matches[1]))이 파일명과 다름: $rel")
    }
}

# .env 규칙
& git -C $RepoRoot check-ignore -q .env
if ($LASTEXITCODE -ne 0) { $failures.Add('.env가 gitignore되지 않음') }
& git -C $RepoRoot check-ignore -q .env.example
if ($LASTEXITCODE -eq 0) { $failures.Add('.env.example이 gitignore됨') }
$envPath = Get-RepoPath '.env'
if (Test-Path -LiteralPath $envPath) {
    $known = @('WORKTREE_ROOT', 'UNITY_CLI')
    foreach ($line in Get-Content -LiteralPath $envPath) {
        if ($line -match '^\s*(?:#|$)') { continue }
        if ($line -notmatch '^([A-Z][A-Z0-9_]*)=') { $failures.Add('.env에 형식이 잘못된 줄이 있음(값은 출력하지 않음)'); continue }
        if ($known -notcontains $Matches[1]) { $failures.Add("알 수 없는 .env 키: $($Matches[1])") }
    }
}

if ($failures.Count) {
    $failures | ForEach-Object { Write-Output "[FAIL] $_" }
    Write-Output "Structure: FAIL ($($failures.Count))"
    exit 1
}
Write-Output 'Structure: OK'
exit 0
