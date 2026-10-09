# (선택) 같은 PC에서 사람과 AI가 동시에 다른 Task를 할 때만 쓰는 worktree 추가·정리.
#   -Add <브랜치>: WORKTREE_ROOT 아래에 별도 폴더로 체크아웃 (별도 Unity 프로젝트로 연다)
#   -Remove <브랜치>: 미커밋 변경·열린 에디터가 없을 때만 제거
[CmdletBinding(SupportsShouldProcess)]
param([string]$Add, [string]$Remove)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

if (-not $Add -and -not $Remove) { Get-Worktrees | ForEach-Object { Write-Output "$($_.Branch)`t$($_.Path)" }; exit 0 }

if ($Add) {
    $root = Get-WorktreeRoot
    $path = [System.IO.Path]::GetFullPath((Join-Path $root ($Add -replace '/', '-')))
    if (Test-Path -LiteralPath $path) { throw "이미 있는 경로: $path" }
    & git -C $RepoRoot fetch origin --quiet
    & git -C $RepoRoot show-ref --verify --quiet "refs/heads/$Add"
    $local = $LASTEXITCODE -eq 0
    if (-not $PSCmdlet.ShouldProcess($path, "worktree 추가 ($Add)")) { return }
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    if ($local) { & git -C $RepoRoot worktree add $path $Add } else { & git -C $RepoRoot worktree add --track -b $Add $path "origin/$Add" }
    if ($LASTEXITCODE -ne 0) { throw 'git worktree add 실패' }
    Update-ThirdParty -Path $path
    Write-Output "추가: $path"
    Write-Output "Unity로 열기: unity open `"$path`" (Library는 worktree끼리 공유하지 않음)"
    exit 0
}

$wt = Get-Worktrees | Select-Object -Skip 1 | Where-Object Branch -eq $Remove | Select-Object -First 1
if (-not $wt) { throw "$Remove 브랜치의 worktree가 없습니다." }
if (Test-PathInside -Child (Get-Location).Path -Parent $wt.Path) { throw '정리할 worktree 안에서는 실행할 수 없습니다.' }
if (& git -C $wt.Path status --porcelain) { throw "저장하지 않은 변경이 있어 제거하지 않습니다: $($wt.Path)" }
$unity = Get-UnityCli
if ($unity) {
    $s = Invoke-NativeWithTimeout -FilePath $unity -Arguments @('status', '--json', '--no-banner', '--project-path', $wt.Path) -TimeoutSeconds 15
    if (@((ConvertFrom-UnityJson $s.StdOut).data.instances).Count) { throw '이 worktree를 연 Unity 에디터가 실행 중입니다.' }
}
if ($PSCmdlet.ShouldProcess($wt.Path, 'worktree 제거')) {
    & git -C $RepoRoot worktree remove $wt.Path
    if ($LASTEXITCODE -ne 0) { throw 'git worktree remove 실패' }
    Write-Output "제거: $($wt.Path)"
}
