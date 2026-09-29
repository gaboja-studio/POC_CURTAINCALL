# Shared helpers for harness scripts. Dot-source: . (Join-Path $PSScriptRoot 'Lib/common.ps1')

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$script:TaskIdPattern = '^Task-(\d{8})-(\d{3})'
$script:TaskBranchPattern = '^(?:origin/)?task/(\d{8})-(\d{3})-'

function Get-RepoPath {
    param([Parameter(Mandatory)][string]$Relative)
    return Join-Path $script:RepoRoot $Relative
}

function Get-EnvValue {
    param([Parameter(Mandatory)][string]$Key)
    $envPath = Get-RepoPath '.env'
    if (-not (Test-Path -LiteralPath $envPath)) { return $null }
    $line = Get-Content -LiteralPath $envPath | Where-Object { $_ -match "^$Key=" } | Select-Object -First 1
    if (-not $line) { return $null }
    return $line.Substring($Key.Length + 1).Trim()
}

function Get-Worktrees {
    # Returns [pscustomobject]@{ Path; Branch } for every registered worktree. First entry is the main checkout.
    $raw = & git -C $script:RepoRoot worktree list --porcelain
    $items = [System.Collections.Generic.List[object]]::new()
    $current = $null
    foreach ($line in $raw) {
        if ($line -like 'worktree *') {
            if ($current) { $items.Add([pscustomobject]$current) }
            $current = @{ Path = $line.Substring(9); Branch = '' }
        } elseif ($line -like 'branch refs/heads/*' -and $current) {
            $current.Branch = $line.Substring(18)
        }
    }
    if ($current) { $items.Add([pscustomobject]$current) }
    return $items
}

function Get-MainCheckout {
    return (Get-Worktrees | Select-Object -First 1).Path
}

function Get-WorktreeRoot {
    $configured = Get-EnvValue 'WORKTREE_ROOT'
    if ($configured) { return [System.IO.Path]::GetFullPath($configured.Replace('~', $HOME)) }
    $main = Get-MainCheckout
    return [System.IO.Path]::GetFullPath((Join-Path (Split-Path $main -Parent) 'github-worktrees' (Split-Path $main -Leaf)))
}

function Get-BaseBranch {
    # Single source: Harness/Project/Facts/repository.md
    $facts = Get-RepoPath 'Harness/Project/Facts/repository.md'
    if (Test-Path -LiteralPath $facts) {
        $m = Select-String -LiteralPath $facts -Pattern 'Base\) 브랜치: `([^`]+)`' | Select-Object -First 1
        if ($m) { return $m.Matches[0].Groups[1].Value }
    }
    return 'dev'
}

function Get-AllTaskFolders {
    # Task folders across every worktree: [pscustomobject]@{ Id; Number; State; Path; Worktree }
    $result = [System.Collections.Generic.List[object]]::new()
    foreach ($wt in Get-Worktrees) {
        foreach ($state in 'Active', 'Done') {
            $dir = Join-Path $wt.Path 'Tasks' $state
            if (-not (Test-Path -LiteralPath $dir)) { continue }
            foreach ($folder in Get-ChildItem -LiteralPath $dir -Directory) {
                if ($folder.Name -match $script:TaskIdPattern) {
                    $result.Add([pscustomobject]@{
                        Id = "Task-$($Matches[1])-$($Matches[2])"
                        Number = [int]$Matches[2]
                        State = $state
                        Path = $folder.FullName
                        Worktree = $wt.Path
                    })
                }
            }
        }
    }
    return $result
}

function Get-TaskBranchNumbers {
    $refs = & git -C $script:RepoRoot for-each-ref --format='%(refname:short)' refs/heads refs/remotes
    return @($refs | Where-Object { $_ -match $script:TaskBranchPattern } | ForEach-Object {
        [void]($_ -match $script:TaskBranchPattern); [int]$Matches[2]
    })
}

function Get-MetaField {
    param([Parameter(Mandatory)][string]$MetaPath, [Parameter(Mandatory)][string]$Field)
    if (-not (Test-Path -LiteralPath $MetaPath)) { return $null }
    $m = Select-String -LiteralPath $MetaPath -Pattern "^\s*-\s*\*\*$([regex]::Escape($Field)):\*\*\s*(.*)$" | Select-Object -First 1
    if (-not $m) { return $null }
    return $m.Matches[0].Groups[1].Value.Trim()
}

function Get-UnityCli {
    # 순서: .env UNITY_CLI → 공식 설치 경로(~/.unity/bin) → PATH.
    # PATH에 다른 버전(예: Homebrew)이 먼저 잡힐 수 있다(Harness/Project/Pitfalls/duplicate-unity-cli.md).
    $configured = Get-EnvValue 'UNITY_CLI'
    if ($configured) { return $configured.Replace('~', $HOME) }
    $official = Join-Path $HOME '.unity/bin/unity'
    if (Test-Path -LiteralPath $official) { return $official }
    $cmd = Get-Command unity -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Invoke-NativeWithTimeout {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @(),
        [int]$TimeoutSeconds = 20
    )
    $psi = [System.Diagnostics.ProcessStartInfo]::new($FilePath)
    foreach ($a in $Arguments) { $psi.ArgumentList.Add($a) }
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $process = [System.Diagnostics.Process]::Start($psi)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { $process.Kill($true) } catch { }
        return [pscustomobject]@{ TimedOut = $true; ExitCode = $null; StdOut = ''; StdErr = '' }
    }
    $process.WaitForExit()
    return [pscustomobject]@{
        TimedOut = $false
        ExitCode = $process.ExitCode
        StdOut = $stdout.Result
        StdErr = $stderr.Result
    }
}

function ConvertFrom-UnityJson {
    # Unity CLI may print banner/progress lines; parse from the first '{'.
    param([string]$Text)
    if (-not $Text) { return $null }
    $start = $Text.IndexOf('{')
    if ($start -lt 0) { return $null }
    try { return $Text.Substring($start) | ConvertFrom-Json -Depth 20 } catch { return $null }
}

function Test-PathInside {
    param([Parameter(Mandatory)][string]$Child, [Parameter(Mandatory)][string]$Parent)
    $c = [System.IO.Path]::GetFullPath($Child).TrimEnd('/', '\') + [System.IO.Path]::DirectorySeparatorChar
    $p = [System.IO.Path]::GetFullPath($Parent).TrimEnd('/', '\') + [System.IO.Path]::DirectorySeparatorChar
    return $c.StartsWith($p, [System.StringComparison]::OrdinalIgnoreCase)
}

# ---------- GitHub ----------

function Get-GhCli {
    $cmd = Get-Command gh -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($p in '/opt/homebrew/bin/gh', '/usr/local/bin/gh', 'C:\Program Files\GitHub CLI\gh.exe') {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    return $null
}

function Get-GitHubRepo {
    # origin URL에서 owner/repo를 읽는다. gh 없이도 동작해야 한다.
    $url = (& git -C $script:RepoRoot remote get-url origin 2>$null)
    if ($url -notmatch 'github\.com[:/]([^/]+)/([^/]+?)(?:\.git)?/?$') { throw "origin이 GitHub 저장소가 아님: $url" }
    return [pscustomobject]@{ Owner = $Matches[1]; Name = $Matches[2]; Slug = "$($Matches[1])/$($Matches[2])" }
}

function Get-GhState {
    # Ready / NotInstalled / NotLoggedIn / NoRepoAccess
    $gh = Get-GhCli
    if (-not $gh) { return [pscustomobject]@{ State = 'NotInstalled'; Gh = $null; Detail = 'gh가 설치되어 있지 않음' } }
    $auth = Invoke-NativeWithTimeout -FilePath $gh -Arguments @('auth', 'status') -TimeoutSeconds 15
    if ($auth.TimedOut -or $auth.ExitCode -ne 0) {
        return [pscustomobject]@{ State = 'NotLoggedIn'; Gh = $gh; Detail = 'gh 로그인이 필요함' }
    }
    $repo = try { Get-GitHubRepo } catch { $null }
    if (-not $repo) { return [pscustomobject]@{ State = 'NoRepo'; Gh = $gh; Detail = 'origin이 GitHub 저장소가 아님' } }
    $view = Invoke-NativeWithTimeout -FilePath $gh -Arguments @('repo', 'view', $repo.Slug, '--json', 'name') -TimeoutSeconds 15
    if ($view.TimedOut -or $view.ExitCode -ne 0) {
        return [pscustomobject]@{ State = 'NoRepoAccess'; Gh = $gh; Detail = "$($repo.Slug) 접근 권한 없음 (PM에게 저장소 초대 요청)" }
    }
    return [pscustomobject]@{ State = 'Ready'; Gh = $gh; Detail = "gh 준비됨 ($($repo.Slug))" }
}

function Open-Url {
    param([Parameter(Mandatory)][string]$Url)
    if ($IsMacOS) { & open $Url }
    elseif ($IsWindows) { Start-Process $Url }
    else { & xdg-open $Url }
}

function Get-MarkdownSection {
    # '## 제목' 아래 본문(다음 '## ' 전까지)을 돌려준다. 제목은 부분 일치.
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Heading)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    $lines = Get-Content -LiteralPath $Path
    $inside = $false
    $buffer = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        if ($line -match '^##\s+(.*)$') {
            if ($inside) { break }
            $inside = $Matches[1].Contains($Heading)
            continue
        }
        if ($inside) { $buffer.Add($line) }
    }
    if (-not $inside -and $buffer.Count -eq 0) { return $null }
    return ($buffer -join "`n").Trim()
}

function Find-CurrentTask {
    # 브랜치(feat|fix|refactor/YYYYMMDD-NNN-slug)에 대응하는 Tasks/Active 폴더. 기본은 현재 브랜치.
    param([string]$Branch)
    if (-not $Branch) { $Branch = (& git -C $script:RepoRoot branch --show-current).Trim() }
    if ($Branch -notmatch '^(feat|fix|refactor)/(\d{8})-(\d{3})-') { return $null }
    $id = "Task-$($Matches[2])-$($Matches[3])"
    $type = $Matches[1]
    $dir = Get-ChildItem -LiteralPath (Get-RepoPath 'Tasks/Active') -Directory -Filter "$id-*" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $dir) { return $null }
    return [pscustomobject]@{ Id = $id; Type = $type; Branch = $Branch; Path = $dir.FullName; Rel = "Tasks/Active/$($dir.Name)/" }
}

function Get-UsedTaskNumbers {
    # 작업 트리 + 로컬·원격의 dev·integration/* 브랜치에 있는 Tasks 폴더 + task 브랜치 이름에서 사용된 번호.
    $numbers = [System.Collections.Generic.List[int]]::new()
    foreach ($t in Get-AllTaskFolders) { $numbers.Add($t.Number) }
    $refs = & git -C $script:RepoRoot for-each-ref --format='%(refname)' refs/heads refs/remotes
    foreach ($ref in $refs) {
        if ($ref -notmatch '/(dev|integration/.+)$') { continue }
        foreach ($line in (& git -C $script:RepoRoot ls-tree -d --name-only $ref -- Tasks/Active/ Tasks/Done/ 2>$null)) {
            if ((Split-Path $line -Leaf) -match $script:TaskIdPattern) { $numbers.Add([int]$Matches[2]) }
        }
    }
    foreach ($n in Get-TaskBranchNumbers) { $numbers.Add($n) }
    return $numbers
}

function Get-SetupAreas {
    # setup.md의 백틱 경로: Allowed(작업 구역 + 배정된 공용 파일), Forbidden(수정 금지)
    param([Parameter(Mandatory)][string]$SetupPath)
    $pick = { param($text) @([regex]::Matches("$text", '`([^`]+)`') | ForEach-Object { $_.Groups[1].Value.Trim() } | Where-Object { $_ -match '^(Assets|ProjectSettings|Packages|Docs)/' }) }
    return [pscustomobject]@{
        Allowed = @(& $pick (Get-MarkdownSection -Path $SetupPath -Heading '작업 구역')) + @(& $pick (Get-MarkdownSection -Path $SetupPath -Heading '배정된 공용 파일'))
        Forbidden = @(& $pick (Get-MarkdownSection -Path $SetupPath -Heading '수정 금지'))
    }
}

function Test-PathInArea {
    # area가 '/'로 끝나면 폴더(하위 전체), 아니면 파일. .meta는 대상 파일·폴더의 짝으로 본다.
    param([Parameter(Mandatory)][string]$Path, [string[]]$Areas)
    $base = $Path -replace '\.meta$', ''
    foreach ($a in $Areas) {
        if ($a.EndsWith('/')) {
            if ($Path.StartsWith($a) -or "$base/" -eq $a) { return $true }
        } elseif ($Path -eq $a -or $base -eq $a) { return $true }
    }
    return $false
}

function Get-CodeOwnerPatterns {
    # .github/CODEOWNERS의 경로 패턴(PM 전용 경로)을 정규식으로 바꿔 돌려준다.
    $file = Get-RepoPath '.github/CODEOWNERS'
    if (-not (Test-Path -LiteralPath $file)) { return @() }
    return @(Get-Content -LiteralPath $file | Where-Object { $_ -match '^\s*[^#\s]' } | ForEach-Object {
        $pattern = ($_ -split '\s+')[0].TrimStart('/')
        $regex = [regex]::Escape($pattern).Replace('\*\*/', '(?:.*/)?').Replace('\*', '[^/]*')
        if ($pattern.EndsWith('/')) { "^$regex" } else { "^$regex$" }
    })
}

function Get-PmAccount {
    $team = Get-RepoPath 'Harness/Project/Facts/team.md'
    $m = Select-String -LiteralPath $team -Pattern '개발 PM\s*\|\s*`@([^`]+)`' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($m) { return $m.Matches[0].Groups[1].Value }
    return $null
}

function Get-CurrentGitHubUser {
    if ($env:GITHUB_ACTOR) { return $env:GITHUB_ACTOR }
    $gh = Get-GhCli
    if (-not $gh) { return $null }
    $r = Invoke-NativeWithTimeout -FilePath $gh -Arguments @('api', 'user', '--jq', '.login') -TimeoutSeconds 15
    if ($r.TimedOut -or $r.ExitCode -ne 0) { return $null }
    return $r.StdOut.Trim()
}

function Set-MetaField {
    param([Parameter(Mandatory)][string]$MetaPath, [Parameter(Mandatory)][string]$Field, [Parameter(Mandatory)][string]$Value)
    $text = Get-Content -LiteralPath $MetaPath -Raw
    $text = [regex]::Replace($text, "(?m)^(\s*-\s*\*\*$([regex]::Escape($Field)):\*\*).*$", "`$1 $Value")
    Set-Content -LiteralPath $MetaPath -Value $text -Encoding utf8NoBOM -NoNewline
}

function Set-IntegrationTaskRow {
    # Integration meta.md Tasks 표에서 Task 행을 추가하거나 갱신한다. $Columns: Task, 종류, 담당, 브랜치, PR, 상태
    param([Parameter(Mandatory)][string]$MetaPath, [Parameter(Mandatory)][AllowEmptyString()][string[]]$Columns)
    if (-not (Test-Path -LiteralPath $MetaPath)) { return $false }
    $lines = [System.Collections.Generic.List[string]]::new([string[]](Get-Content -LiteralPath $MetaPath))
    $row = '| ' + ($Columns -join ' | ') + ' |'
    $existing = $lines.FindIndex([Predicate[string]]{ param($l) $l.StartsWith("| $($Columns[0]) |") })
    if ($existing -ge 0) {
        $old = $lines[$existing].Trim('|').Split('|') | ForEach-Object { $_.Trim() }
        $merged = for ($i = 0; $i -lt $Columns.Count; $i++) { if ($Columns[$i]) { $Columns[$i] } else { $old[$i] } }
        $lines[$existing] = '| ' + ($merged -join ' | ') + ' |'
    } else {
        $head = $lines.FindIndex([Predicate[string]]{ param($l) $l -match '^## Tasks' })
        if ($head -lt 0) { return $false }
        $i = $head + 1
        while ($i -lt $lines.Count -and -not $lines[$i].StartsWith('|')) { $i++ }
        while ($i -lt $lines.Count -and $lines[$i].StartsWith('|')) { $i++ }
        $lines.Insert($i, $row)
    }
    Set-Content -LiteralPath $MetaPath -Value (($lines -join "`n") + "`n") -Encoding utf8NoBOM -NoNewline
    return $true
}

function Assert-CleanTree {
    param([string]$Hint = '먼저 "저장해줘"로 저장하세요.')
    $dirty = & git -C $script:RepoRoot status --porcelain
    if ($dirty) { throw "저장하지 않은 변경이 있습니다. $Hint`n$($dirty -join "`n")" }
}

function Get-AssetMetaPaths {
    # Assets/ 경로에 필요한 .meta 목록: 자기 자신 + Assets와 자신 사이의 모든 상위 폴더.
    # 예: Assets/_Sandbox/Curtain/Test.unity → Assets/_Sandbox.meta, Assets/_Sandbox/Curtain.meta, …/Test.unity.meta
    param([Parameter(Mandatory)][string]$Path)
    $clean = $Path.TrimEnd('/')
    if ($clean -notlike 'Assets/*' -or $clean -like '*.meta') { return @() }
    $parts = $clean.Split('/')
    $result = for ($i = 2; $i -le $parts.Count; $i++) { ($parts[0..($i - 1)] -join '/') + '.meta' }
    return @($result)
}

# ---------- 커밋 작성자 (Harness/Core/Policies/git-workflow.md "작성자") ----------

function Get-GitHubIdentity {
    # gh 로그인 계정 → 커밋 작성자. 이메일은 계정과 항상 연결되는 noreply 주소.
    $gh = Get-GhCli
    if (-not $gh) { return $null }
    $r = Invoke-NativeWithTimeout -FilePath $gh -Arguments @('api', 'user', '--jq', '"\(.login) \(.id)"') -TimeoutSeconds 15
    if ($r.TimedOut -or $r.ExitCode -ne 0 -or -not $r.StdOut.Trim()) { return $null }
    $login, $id = $r.StdOut.Trim().Split(' ')
    return [pscustomobject]@{ Login = $login; Name = $login; Email = "$id+$login@users.noreply.github.com" }
}

function Set-GitIdentity {
    # 이 저장소(local)의 커밋 작성자를 gh 계정으로 맞추고 커밋 차단 훅을 켠다.
    param([Parameter(Mandatory)]$Identity)
    & git -C $script:RepoRoot config --local user.name $Identity.Name
    & git -C $script:RepoRoot config --local user.email $Identity.Email
    & git -C $script:RepoRoot config --local core.hooksPath .githooks
}

function Assert-CommitIdentity {
    # 커밋하는 스크립트가 커밋 전에 호출한다. 작성자가 gh 계정이 아니거나 훅이 꺼져 있으면 멈춘다.
    $identity = Get-GitHubIdentity
    if (-not $identity) { throw 'GitHub 연결이 필요합니다. 커밋은 내 GitHub 계정으로만 할 수 있어요. AI에게 "GitHub 연결해줘"라고 말하세요.' }
    $name = (& git -C $script:RepoRoot config user.name)
    $email = (& git -C $script:RepoRoot config user.email)
    $hooks = (& git -C $script:RepoRoot config core.hooksPath)
    if ($name -ne $identity.Name -or $email -ne $identity.Email -or $hooks -ne '.githooks') {
        throw "커밋 작성자($name <$email>)가 GitHub 계정(@$($identity.Login))과 다르거나 커밋 검사가 꺼져 있습니다. pwsh -File Scripts/setup-gh.ps1 을 실행하세요."
    }
}
