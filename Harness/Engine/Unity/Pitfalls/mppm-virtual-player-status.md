---
summary: MPPM 가상 플레이어가 켜져 있으면 unity status에 <project>/Library/VP/... 에디터가 함께 나와 경로 기준으로 에디터를 세는 스크립트가 "모호"로 멈춤
status: active
updated: 2026-10-02
source: Task-20260930-007
---

# MPPM 가상 플레이어가 에디터로 잡힘

## 언제 읽나

Multiplayer Play Mode로 여러 명 테스트하는 중에 저장·검사(`save-work`, `check-work`, `verify-unity.ps1`)가 "이 경로에 연결된 에디터가 2개. 대상이 모호하여 중단."으로 멈출 때. 또는 `unity status`를 경로로 걸러 쓰는 스크립트를 만들 때.

## 증상

- `unity status --json --project-path <p>`가 메인 에디터와 함께 `project: <p>/Library/VP/mppm<id>` 인스턴스를 돌려준다(포트가 다름).
- 경로가 `<p>`로 시작하는 것을 모두 세면 에디터가 2개 이상으로 나온다.

## 원인

unity-cli `1.0.0-beta.12`부터 `unity status`·`unity pipeline list`가 MPPM 가상 플레이어도 보여 준다. 가상 플레이어는 프로젝트 안 `Library/VP/` 아래 복제본이라 같은 경로로 걸린다.

## 대처

1. 스크립트는 `instances`에서 `project`가 대상 경로와 **정확히 같은** 것만 쓴다(`verify-unity.ps1`은 2026-10-02 수정).
2. 수정 전 스크립트를 쓰는 브랜치에서는 저장·검사 전에 MPPM 창에서 가상 플레이어를 끈다.
3. 컴파일만 급히 확인하려면 메인 에디터에 직접 `recompile_status`를 묻는다.

## 확인 방법

가상 플레이어를 켠 채 `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`이 `Editor: ready`로 진행되는지 본다.
