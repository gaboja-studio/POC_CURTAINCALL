---
summary: Unity CLI가 두 개(Homebrew beta.6, ~/.unity/bin beta.8) 설치되어 PATH 순서에 따라 다른 버전이 실행됨
status: active
updated: 2026-09-29
source: 2026-09-29 하네스 세팅 중 doctor.ps1 경고
---

# Unity CLI 중복 설치

## 언제 읽나

`doctor.ps1`의 `unity-cli`가 WARN이거나, 셸마다 `unity --version` 결과가 다르거나, 같은 명령이 환경에 따라 다르게 동작할 때.

## 증상

- 기본 셸: `unity --version` → `1.0.0-beta.8`
- `/opt/homebrew/bin`이 PATH 앞에 있는 셸(pwsh 등): `1.0.0-beta.6`

## 원인

- `/opt/homebrew/bin/unity` (Homebrew, beta.6)
- `~/.unity/bin/unity` (공식 설치·self-update, beta.8)

## 대처

- 하네스 스크립트는 `Scripts/Lib/common.ps1`의 `Get-UnityCli`로 `.env UNITY_CLI` → `~/.unity/bin/unity` → PATH 순서로 고른다.
- 직접 명령을 칠 때도 `~/.unity/bin/unity`를 쓴다.
- 하나로 정리하려면 사람이 결정한다(예: `brew uninstall unity`). AI가 임의로 제거하지 않는다.

## 확인 방법

```
which -a unity
~/.unity/bin/unity --version
```
