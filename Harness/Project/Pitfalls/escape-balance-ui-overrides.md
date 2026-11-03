---
summary: 같은 균형 HUD 프리팹이어도 원본 묘기 씬 오버라이드가 탈출로에 빠질 수 있다.
status: active
updated: 2026-11-03
source: Task-20261027-073
---

# 외줄 균형 UI 씬 오버라이드

- 원본 Tightrope 씬과 A는 같은 PerformanceHud 프리팹을 쓰지만 원본 씬의 위험 트랙/채움 높이는23/15px, 프리팹 기본은12/4px였다. CanvasScaler도 원본은Expand, A는MatchWidthOrHeight였다.
- 프리팹 경로가 같다는 확인만으로 표시 일치를 판단하지 않는다. 실제 원본 씬의 오버라이드/스케일과 런타임 BalanceRiskBlink/BalanceZoneColors/HudLayout 연결까지 비교한다.
- A의 EscapeRopeBalanceDisplay는 외줄에 올라간 동안만 원본 균형 값을 전달하며 탈출 타이머는 별도다. 전체 공연 HUD를 설치하면 불필요한 공연 진행/보상 표시가 섞일 수 있다.
- A 작성 도구의 InstallOriginalBalanceDisplay에도 위 치수/스케일을 반영했다. 원본 UI가 다시 변경되면 실제 씬을 대조해 필요한 표시 설정만 맞춘다.
- 같은 유형의 차이가 A의 별도 타이머 Canvas와 장치 알림 Canvas에도 있다(원본Expand/A MatchWidthOrHeight). 공용 씬을 실제로 공유하는 C6·메뉴·인벤토리와 로컬 복제 UI를 구분해 조사한다. Task-073 ui-reuse-audit.md 참조.
- 후속 보수로 A 세 Canvas 모두Expand, 알림 폰트는 공용 참조로 맞췄다. PerformanceSceneBinding.PresentationOnly는 공용 관전/장치 안내만 활성화하며 HasPerformanceSpace는 공연 정산·보상용 맥락만 뜻한다. 두 조건을 다시 혼용하지 않는다.
