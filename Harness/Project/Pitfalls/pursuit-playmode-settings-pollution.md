---
summary: 실제 대기실 추격 PlayMode가 실패·중단되면 메모리의 GameSettings 추격 단계값이 바뀐 채 남아 이어서 돌린 추격 테스트가 실패한다. 도메인 재로드로 복구하고 오염된 채 설정 에셋을 저장하지 않는다.
status: active
updated: 2026-11-12
source: Task-20261112-117 (PlayMode Pursuit 전체 실행 후 재현)
---

# 추격 PlayMode 실패 뒤 메모리 GameSettings 오염

- 증상: 실제 대기실 기반 추격 PlayMode(DevStartLauncher의 Pursuit* 테스트)가 실패한 뒤 이어서 돌린 독립 fixture 추격 테스트가 실패한다. 추격자가 0.1m/s로 기어가거나 1단계가 최인접·투시처럼 동작한다.
- 원인: 해당 테스트들이 `GameSettings.WaitingRoom.Pursuit.Stages`의 단계 객체를 직접 바꾸고(추격 0.1m/s, 시야 360° 등), 실패·중단되면 원래 값이 메모리에 남는다. 에셋은 dirty가 아니어서 `AssetDatabase.ImportAsset`으로는 복구되지 않는다. `Resources.UnloadAsset`은 GameSettings가 코드 기본 fallback을 캐시해 더 나빠진다.
- 확인: 디스크 에셋과 메모리 단계값(`GameSettings.WaitingRoom.Pursuit.Stages`)을 비교한다.
- 대응: 스크립트 도메인 재로드(`EditorUtility.RequestScriptReload`) 후 다시 읽는다. 오염된 채 설정 에셋을 저장(SaveAssetIfDirty·Designer 저장)하면 잘못된 값이 기록되니, 저장 전에 단계값을 확인한다. 독립 fixture 테스트는 실패 묶음과 분리해 단독 재실행해 판정한다.
- 기록: 2026-11-12 Task-20261112-117.
