---
summary: AI 도구 셸이 Rosetta(x86)로 실행되어 brew install이 실패하고, /opt/homebrew/bin이 PATH에 없음
status: active
updated: 2026-09-29
source: 2026-09-29 하네스 세팅 중 pwsh 설치
---

# AI 셸의 Rosetta 실행

## 언제 읽나

AI가 셸에서 `brew install`을 하거나, `pwsh`를 찾지 못한다고 나올 때.

## 증상

- `Error: Cannot install under Rosetta 2 in ARM default prefix (/opt/homebrew)!`
- `pwsh`가 설치되어 있는데 `which pwsh`가 실패한다.

## 원인

AI 도구가 띄운 셸이 x86_64(Rosetta)로 돌고 있고, ARM Homebrew 경로가 PATH에 없다.

## 대처

- 설치: `arch -arm64 brew install <formula>`
- 실행: `pwsh`가 안 잡히면 절대 경로 `/opt/homebrew/bin/pwsh -File Scripts/<name>.ps1`

## 확인 방법

```
uname -m          # x86_64면 Rosetta
ls /opt/homebrew/bin/pwsh
```
