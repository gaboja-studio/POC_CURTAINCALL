# Urgent Exception

- **Approved by:** PM @MoHoDu (사용자가 채팅에서 PM 허락받았다고 직접 전달)
- **Approved at:** 2026-10-22 (정확한 구두 승인 시각 미제공)
- **Requested by:** @RagSpirin
- **Reason:** 사용자 요청에 따라 인간대포·외줄타기 봉에 기존 상호작용 아웃라인과 F키 UI를 연결한다. 후속 사용자 요청으로 봉 잡기 거리의 여유 확대도 포함한다. 신규 UI/프리팹/에셋 생성·삭제는 제외한다.
- **Paths:** `Assets/Scripts/UI/SceneIntegration/CommonInteractionUI.cs`, `Assets/Scripts/UI/SceneIntegration/HumanCannonUiBinding.cs`, `Assets/Scenes/UI/CommonInteractionUI.unity`, `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity`, `Assets/Scripts/MainStage/HumanCannon/`, `Assets/Scripts/MainStage/Tightrope/Rider/`, 두 공연의 기존 PlayMode 테스트 파일 및 이 Task 문서.
- **Branch:** fix/cannon-saw-gate-window — 기존 Task-044의 이번 수정으로 연결. 기존 제출 이력과 다른 Task 소유권은 유지한다.
- 기존 로컬 폰트·재질·MultiplayerManager 변경은 사용자 변경으로 보존하며 이번 구현에 포함하지 않는다. PM은 제출 시 예외 경로를 사후 확인한다.
- 2026-10-22 후속 사용자 승인: 기존 봉 톱 왕복4→6초 시험 완화, 기존 상호작용 외곽선 밝기/두께·표시점/그리기 순서 보완. 에디터에서 기존 씬 값만 저장하며 새 에셋·UI는 만들지 않는다.
