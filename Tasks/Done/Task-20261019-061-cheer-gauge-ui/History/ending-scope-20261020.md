## 2026-10-20 종료 연출 (사용자 직접 추가 지시)
- 두 공연 로컬 사망 알림, 종료 대기/결과 표시 시간, 임시 검은 전환 씬 추가.
- 기존 SceneIntegration/Scenes/UI 구역과 PerformanceRewards 전환 지연을 사용한다.
- `Assets/Scripts/Systems/Settings/PerformanceEndingSettings.cs` 및 meta, `Assets/Resources/Settings/GameSettings/Tricks/Rewards/performance-ending.asset` 및 meta를 추가한다.
- EditorBuildSettings에는 전환 UI 씬을 추가한다. 게임 성공/실패 판정·원본 UI 프리팹·그래픽은 유지한다.


