---
summary: 버전 표시를 씬 목록이나 마이크 표시 조건에 종속시키지 않기
status: active
updated: 2026-11-16
source: Task-20261116-138
---

# 전 씬 버전 표시

- `BuildVersionUI`는 BeforeSceneLoad에서 생성하고 DontDestroyOnLoad로 유지한다. CommonMenu/VoiceStatus의 지원 씬 조건에 넣으면 Start 또는 임의 씬 직접 Play에서 버전이 사라진다.
- 표시값은 `Application.version`과 `Application.isEditor || Debug.isDebugBuild`를 사용한다. Editor/Development Build는 TEST, 일반 빌드는 LIVE. 기존 TelemetryRuntime/PlaytestSurveyRuntime과 동일하며 게임 내 개발자 기능 활성화 여부와 구분한다.
- 1920x1080 기준 하단 왼쪽 (24,18), 16pt. 공통 마이크의 (24,70) 아래에 놓는다. GraphicRaycaster를 추가하거나 텍스트 raycastTarget을 켜면 다른 UI 입력을 가로챌 수 있다.
- 버전의 원본은 PlayerSettings.bundleVersion이다. Editor API 변경 후 메모리 값뿐 아니라 ProjectSettings/ProjectSettings.asset 저장도 확인한다.
