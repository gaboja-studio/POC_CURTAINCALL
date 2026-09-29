---
summary: Unity 에디터·URP·Pipeline·Input System 버전 고정값
status: active
updated: 2026-09-29
source: human (프로젝트 생성 시 선택)
---

# Unity 버전

## 언제 읽나

에디터 설치·실행, 패키지 추가/업그레이드, 버전 관련 오류를 조사할 때.

## 결정

- 2026-09-29 / 프로젝트 생성자:
  - Unity Editor: `6000.3.25f1`
  - URP: `com.unity.render-pipelines.universal` `17.3.0` (PC/Mobile 렌더러 에셋 2종, `Assets/Settings/`)
  - Unity Pipeline: `com.unity.pipeline` `0.8.0-exp.1`
  - Input System: `com.unity.inputsystem` `1.20.0` (Active Input Handling = Input System only)
  - Test Framework: `com.unity.test-framework` `1.6.0`

## 이유

프로젝트 생성 시 선택한 템플릿/버전. 변경 이유가 생기면 여기에 적는다.

## 바꾸려면

- 사람의 결정이 필요하다(`Harness/Core/Policies/human-decision.md`).
- 전용 Task에서 `ProjectSettings/`, `Packages/`를 Exclusive Assets로 소유하고 진행한다.
- 변경 후 이 파일, `Harness/Engine/Unity/Facts/unity-cli.md`, `Scripts/doctor.ps1` 결과를 확인한다.
