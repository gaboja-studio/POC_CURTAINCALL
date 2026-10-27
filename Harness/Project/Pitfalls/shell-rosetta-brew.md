---
summary: AI 도구 셸이 Rosetta(x86)로 실행되어 brew install이 실패하고, /opt/homebrew/bin이 PATH에 없음
status: active
updated: 2026-10-27
source: 2026-09-29 하네스 세팅 중 pwsh 설치; Task-20261027-072 Finder PATH 재현
---

# AI 셸의 Rosetta 실행

## 언제 읽나

AI가 셸에서 `brew install`을 하거나, `pwsh`를 찾지 못한다고 나올 때.
Mac 에셋 도우미 앱에서 맞추기가 LFS exit 1로 실패하지만 터미널에서는 정상일 때도 읽는다.

## 증상

- `Error: Cannot install under Rosetta 2 in ARM default prefix (/opt/homebrew)!`
- `pwsh`가 설치되어 있는데 `which pwsh`가 실패한다.

## 원인

AI 도구가 띄운 셸이 x86_64(Rosetta)로 돌고 있고, ARM Homebrew 경로가 PATH에 없다.

## 대처

- 설치: `arch -arm64 brew install <formula>`
- 실행: `pwsh`가 안 잡히면 절대 경로 `/opt/homebrew/bin/pwsh -File Scripts/<name>.ps1`
- Finder 앱은 셸 프로필을 읽지 않아 Homebrew PATH가 빠질 수 있다. PowerShell만 절대 경로로 실행해도 자식 Git은 LFS를 찾지 못한다. 런처가 `/opt/homebrew/bin`과 `/usr/local/bin`을 기존 PATH 앞에 추가·export해야 한다. 앱과 `build-mac-launcher.ps1`의 ZIP 원본을 함께 수정한다.
- 재현: PATH가 `/usr/bin:/bin:/usr/sbin:/sbin`이면 `git lfs version` 실패. 수정된 런처 환경에서는 공통 `Invoke-TransitionGit`의 LFS 조회 성공을 확인했다.

## 확인 방법

```
uname -m          # x86_64면 Rosetta
ls /opt/homebrew/bin/pwsh
```
