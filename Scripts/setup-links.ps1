# Harness/**/Skills/*.md를 Claude Code가 인식하는 .claude/skills/<name>/SKILL.md 심볼릭 링크로 연결한다.
# .claude/skills는 로컬 어댑터이며 git에서 무시한다. 원본은 항상 Harness/ 아래 파일이다.
[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

$target = Get-RepoPath '.claude/skills'
$skills = @(Get-ChildItem -LiteralPath (Get-RepoPath 'Harness') -Filter '*.md' -File -Recurse |
    Where-Object { $_.FullName -match '[\\/]Skills[\\/]' })

$names = @{}
foreach ($skill in $skills) {
    if ($names.ContainsKey($skill.BaseName)) { throw "Skill 이름 중복: $($skill.BaseName)" }
    $names[$skill.BaseName] = $skill.FullName
}

# 원본이 사라진 링크만 정리한다(실제 파일/폴더는 건드리지 않음).
if (Test-Path -LiteralPath $target) {
    foreach ($dir in Get-ChildItem -LiteralPath $target -Directory) {
        $link = Join-Path $dir.FullName 'SKILL.md'
        $item = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue
        if ($item -and $item.LinkType -and -not $names.ContainsKey($dir.Name)) {
            if ($PSCmdlet.ShouldProcess($dir.FullName, '오래된 Skill 링크 제거')) {
                Remove-Item -LiteralPath $link -Force
                Remove-Item -LiteralPath $dir.FullName -Force
                Write-Output "Removed stale: $($dir.Name)"
            }
        }
    }
}

foreach ($name in $names.Keys | Sort-Object) {
    $dir = Join-Path $target $name
    $link = Join-Path $dir 'SKILL.md'
    $relativeSource = [System.IO.Path]::GetRelativePath($dir, $names[$name])
    $existing = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue
    if ($existing) {
        if (-not $existing.LinkType) { throw "링크가 아닌 실제 파일이 있어 교체하지 않음: $link" }
        if (@($existing.Target)[0] -eq $relativeSource) { continue }
        if ($PSCmdlet.ShouldProcess($link, '링크 교체')) { Remove-Item -LiteralPath $link -Force }
    }
    if ($PSCmdlet.ShouldProcess($link, "링크 생성 → $relativeSource")) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        New-Item -ItemType SymbolicLink -Path $link -Target $relativeSource | Out-Null
        Write-Output "Linked: $name → $relativeSource"
    }
}
Write-Output "Skill links: $($names.Count) (target $target)"
