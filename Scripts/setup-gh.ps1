# GitHub CLI(gh) 준비 상태를 확인하고, -Install이면 설치를 1회 시도한다.
# 종료 코드: 0 준비됨, 10 미설치, 11 로그인 필요, 12 저장소 권한 없음, 13 설치 실패
# 재시도 횟수 관리와 사용자 확인은 Skill(Harness/Core/Skills/setup-gh.md)이 맡는다.
[CmdletBinding()]
param([switch]$Install)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Lib/common.ps1')

function Install-Gh {
    if ($IsMacOS) {
        $brew = @('/opt/homebrew/bin/brew', '/usr/local/bin/brew') | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $brew) {
            Write-Output 'Homebrew가 없어 자동 설치할 수 없음. https://cli.github.com 에서 직접 설치 필요.'
            return $false
        }
        # AI 도구의 셸이 Rosetta(x86)로 돌면 ARM Homebrew 설치가 거부된다 (Harness/Project/Pitfalls/shell-rosetta-brew.md).
        $armHardware = ((& sysctl -n hw.optional.arm64 2>$null) -eq '1')
        $x86Shell = ((& uname -m) -eq 'x86_64')
        if ($armHardware -and $x86Shell -and $brew -eq '/opt/homebrew/bin/brew') {
            & arch -arm64 $brew install gh
        } else {
            & $brew install gh
        }
        return ($LASTEXITCODE -eq 0)
    }
    if ($IsWindows) {
        & winget install --id GitHub.cli -e --accept-source-agreements --accept-package-agreements
        return ($LASTEXITCODE -eq 0)
    }
    Write-Output 'Linux는 배포판별 설치가 필요함: https://github.com/cli/cli/blob/trunk/docs/install_linux.md'
    return $false
}

$state = Get-GhState
if ($state.State -eq 'NotInstalled' -and $Install) {
    Write-Output 'gh 설치 시도...'
    if (-not (Install-Gh)) {
        Write-Output 'GH: INSTALL_FAILED'
        exit 13
    }
    $state = Get-GhState
}

Write-Output "GH: $($state.State) — $($state.Detail)"
switch ($state.State) {
    'Ready' { exit 0 }
    'NotInstalled' {
        Write-Output '다음: pwsh -File Scripts/setup-gh.ps1 -Install'
        exit 10
    }
    'NotLoggedIn' {
        Write-Output '다음: 사용자가 직접 로그인 (브라우저가 열리고 화면의 코드를 입력)'
        Write-Output '      Claude Code 입력창: ! gh auth login --web --git-protocol https'
        Write-Output '      일반 터미널:        gh auth login --web --git-protocol https'
        exit 11
    }
    'NoRepo' {
        Write-Output '다음: 이 폴더의 origin이 GitHub 주소인지 확인 (git remote -v). PM에게 알림'
        exit 14
    }
    'NoRepoAccess' {
        Write-Output '다음: PM에게 GitHub 저장소 초대를 요청 (재시도로 해결되지 않음)'
        exit 12
    }
}
