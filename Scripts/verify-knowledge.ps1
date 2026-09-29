# 지식 폴더(Pitfalls/Decisions/Facts) 검사: index.md 존재, index ↔ 실제 파일 일치, frontmatter 필수 항목.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$failures = [System.Collections.Generic.List[string]]::new()
$required = 'summary', 'status', 'updated', 'source'

$roots = @(Get-ChildItem -LiteralPath (Get-RepoPath 'Harness') -Directory -Recurse |
    Where-Object { $_.Name -in 'Pitfalls', 'Decisions', 'Facts' -and $_.FullName -notmatch '[\\/]Templates[\\/]' })
$dirs = @($roots) + @($roots | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Directory -Recurse })

foreach ($dir in $dirs) {
    $rel = [System.IO.Path]::GetRelativePath($RepoRoot, $dir.FullName)
    $index = Join-Path $dir.FullName 'index.md'
    if (-not (Test-Path -LiteralPath $index)) {
        $failures.Add("index.md 없음: $rel")
        continue
    }
    $indexText = Get-Content -LiteralPath $index -Raw
    $links = @([regex]::Matches($indexText, '\]\(([^)]+\.md)\)') | ForEach-Object { $_.Groups[1].Value })

    foreach ($link in $links) {
        if (-not (Test-Path -LiteralPath (Join-Path $dir.FullName $link))) {
            $failures.Add("index가 없는 파일을 가리킴: $rel/index.md → $link")
        }
    }

    foreach ($file in Get-ChildItem -LiteralPath $dir.FullName -Filter '*.md' -File | Where-Object Name -ne 'index.md') {
        if ($links -notcontains $file.Name) { $failures.Add("index에 없음: $rel/$($file.Name)") }
        $text = Get-Content -LiteralPath $file.FullName -Raw
        if ($text -notmatch '(?s)^---\r?\n(.*?)\r?\n---') {
            $failures.Add("frontmatter 없음: $rel/$($file.Name)")
            continue
        }
        $front = $Matches[1]
        foreach ($key in $required) {
            if ($front -notmatch "(?m)^$key\s*:\s*\S") { $failures.Add("frontmatter '$key' 없음: $rel/$($file.Name)") }
        }
    }

    foreach ($sub in Get-ChildItem -LiteralPath $dir.FullName -Directory) {
        if ($links -notcontains "$($sub.Name)/index.md") { $failures.Add("index에 하위 폴더 없음: $rel/$($sub.Name)/index.md") }
    }
}

if ($failures.Count) {
    $failures | ForEach-Object { Write-Output "[FAIL] $_" }
    Write-Output "Knowledge: FAIL ($($failures.Count))"
    exit 1
}
Write-Output "Knowledge: OK ($($dirs.Count) folders)"
exit 0
