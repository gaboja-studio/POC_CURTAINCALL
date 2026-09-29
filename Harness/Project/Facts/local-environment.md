---
summary: 개발 머신(macOS ARM) 도구 경로 — pwsh, Unity CLI, 설치된 에디터
status: active
updated: 2026-09-29
source: 2026-09-29 환경 점검
---

# 로컬 개발 환경

## 언제 읽나

스크립트 실행이 안 되거나, 도구 경로·설치 여부를 확인할 때.

## 사실

- OS: macOS (Apple Silicon, arm64)
- PowerShell: `7.6.6`, `/opt/homebrew/bin/pwsh` (Homebrew formula `powershell`)
- Unity CLI: `~/.unity/bin/unity` (beta.8, 기준). `/opt/homebrew/bin/unity`(beta.6)도 있음 → `Pitfalls/duplicate-unity-cli.md`
- Unity Hub 에디터 경로: `/Applications/Unity/Hub/Editor/<version>/Unity.app`
- 설치된 에디터: 6000.0.84f1, 6000.3.25f1(프로젝트 버전), 6000.6.3f1, 6000.7.0b2
- 스크립트 실행: `pwsh -File Scripts/<name>.ps1`

## 확인 방법

`pwsh -File Scripts/doctor.ps1`
