# Urgent Exception

- **Approved by:** PM @MoHoDu (사용자가 채팅에서 PM 허락받았다고 직접 전달)
- **Approved at:** 2026-10-22 (정확한 구두 승인 시각 미제공)
- **Requested by:** @RagSpirin
- **Reason:** 사용자 요청에 따라 인간대포·외줄타기 봉에 기존 상호작용 아웃라인과 F키 UI를 연결한다. 후속 사용자 요청으로 봉 잡기 거리의 여유 확대도 포함한다. 신규 UI/프리팹/에셋 생성·삭제는 제외한다.
- **Paths:** `Assets/Scripts/UI/SceneIntegration/CommonInteractionUI.cs`, `Assets/Scripts/UI/SceneIntegration/HumanCannonUiBinding.cs`, `Assets/Scenes/UI/CommonInteractionUI.unity`, `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity`, `Assets/Scripts/MainStage/HumanCannon/`, `Assets/Scripts/MainStage/Tightrope/Rider/`, 두 공연의 기존 PlayMode 테스트 파일 및 이 Task 문서.
- **Branch:** fix/cannon-saw-gate-window — 기존 Task-044의 이번 수정으로 연결. 기존 제출 이력과 다른 Task 소유권은 유지한다.
- 기존 로컬 폰트·재질·MultiplayerManager 변경은 사용자 변경으로 보존하며 이번 구현에 포함하지 않는다. PM은 제출 시 예외 경로를 사후 확인한다.
- 2026-10-22 후속 사용자 승인: 기존 봉 톱 왕복4→6초 시험 완화, 기존 상호작용 외곽선 밝기/두께·표시점/그리기 순서 보완. 에디터에서 기존 씬 값만 저장하며 새 에셋·UI는 만들지 않는다.
- 2026-10-22 PR #210 병합 후 같은 사람 확인의 4인 피드백: 최신 dev에서 fix/four-player-course-feedback 생성. 사용자가 지상 장애물/전기톱 벽 외 공중 장애물 제거, 중간 일반 봉 축소 선택, 외줄 봉 낮춤·범위 내 F 잡기, 톱 개수 유지/경로 분산을 직접 지시했다. `Assets/Scripts/MainStage/Tightrope/Rope/TightropeCourse.PolePreview.cs`, `Assets/Scripts/MainStage/Tightrope/Rider/`, `Assets/Scripts/MainStage/Tightrope/Saws/TightropeSaws.cs`, `Assets/Resources/Settings/GameSettings/Tricks/TightropeSettings.asset` 및 기존 HumanCannon 씬/코드가 추가 대상이다. 새 UI/에셋 생성과 commit/push는 이번 요청에 포함하지 않는다.
- 일반 봉은 휴식용5개+관문 연결2개=총7개라는 사용자 정정에 따른다. 기존 `Assets/Scripts/UI/SceneIntegration/TightropeControlsPanel.cs`의 안내 문구도 새 범위 F 동작에 맞춰 수정한다.
- 2026-10-22 후속 지시: 외줄 인간 그네의 동료 잡기 판정/입력을 봉과 동일하게 통일한다. 기존 `SwingPoleMath.InPoleReach` 호출 시그니처 정리를 위해 `Assets/Scripts/MainStage/Tightrope/Rope/Editor/Tests/TightropeSwingGapTests.cs`의 기존 호출부도 함께 갱신한다. 테스트는 실행하지 않는다.
- 2026-10-22 사용자 직접 범위 확대: 묘기 내 모든 상호작용을 대상 중심의 구형 거리로 정리하고 목마 대상 플레이어에 기존 F/아웃라인 표시를 적용한다. 기존 `Assets/Scripts/Systems/Player/PlayerInteraction.cs`의 몸통 중심/구형 거리 조회, `Assets/Scripts/MainStage/Tightrope/Piggyback/PiggybackSystem.cs`, `Assets/Scripts/MainStage/Tightrope/Rider/`, `Assets/Scripts/UI/SceneIntegration/CommonInteractionUI.cs`, `Assets/Scripts/UI/SceneIntegration/InteractionOutline.cs`의 기존 기능 수정에 적용한다. 새 UI·셰이더·에셋 생성 없이 기존 외곽선에 SkinnedMeshRenderer 처리를 보완한다. 실제 플레이/자동 테스트는 실행하지 않는다.
- 2026-10-22 제출 전 사용자 중단/추가 수정 지시: 외줄 점프를 반복해 게이지 패널티를 회피하는 문제를 수정한 뒤 멈춘다. `Assets/Scripts/Systems/Player/PlayerBalance.cs`, `Assets/Scripts/Systems/Player/PlayerCommandDebugHud.cs`, `Assets/Scripts/MainStage/Tightrope/Rope/CourseControlSwitcher.cs`, `Assets/Scripts/MainStage/Tightrope/Rope/Editor/Tests/TightropeBalanceFeedbackTests.cs`의 기존 구현/회귀 항목을 수정한다. 일반·옆줄·목마 점프 중 균형/위험 누적 지속, 공중 초록 회복 없음, 봉/장치 외부 이동의 균형 정지는 유지한다. 이전 저장·제출 진행은 보류하며 commit/push/PR 생성하지 않는다.

- 2026-10-22 점프 회피 수정 후 사용자가 저장·제출을 다시 명시적으로 지시했다. 이전 보류 해제, 이번 수정만 선별 커밋·push·dev 대상 PR 제출을 진행한다. 사용자 폰트/재질 로컬 변경은 보존한다.
- 2026-10-22 PR #211 병합 후 사용자 직접 후속 수정: 새 브랜치를 만들지 않고 기존 fix/four-player-course-feedback 유지, 최신 dev 07e5dc5 pull 완료. 인간대포 일반 봉5개를 대포에서 떨어진 코스 전후/좌우로 분산하고, 트램폴린은 중앙 천 하강 교차가 아니라 프레임/손잡이를 포함한 전체 외형에 몸통이 닿으면 반동하도록 완화한다. 기존 HumanCannon 씬/코드/검사 항목만 수정하며 플레이·자동 테스트는 실행하지 않는다.
- 2026-10-22 사용자 조사안 승인·후속 범위 정정: 남은 팔다리4/3/2/1개와 좌우 조합 전체에서 기존 협동 흐름으로 진행하도록 수정한다. 목마 상층→봉→하층 동료 잡기→교대 순서를 유지하고 단독 진입을 차단하며, 기존 `PiggybackSystem.cs`/`PiggybackFollow.cs`의 해제·위치/이동 인계를 재사용한다. 기존 Rider 그네/연결봉 범위 하한 및 HumanCannonPole 범위 조회·소유 HumanCannon 씬의 지상 톱12개 높이만 보완한다. 공용 부상/사망 규칙·새 UI/에셋·자동/플레이 테스트 실행·commit/push/PR 갱신은 포함하지 않는다.
- 2026-10-22 후속 사용자 요청: PR #213의 기존 내용과 이번 부상별 협동 진행 수정을 함께 저장·제출하도록 명시 승인했다. 기존 브랜치에서 관련 파일만 커밋·push하고 동일 PR #213의 제목/설명을 최종 범위에 맞춰 갱신한다. 폰트·재질5개는 제외/보존, 실제/자동 테스트는 계속 사용자 수행.
- 2026-10-22 PR #213 병합 후 사용자 버그 후속 승인: 최신 dev를 기존 작업 브랜치에 받고, 잡은 뒤 1인 그네 W/S·Space 입력 유지, 그네 아래 전기톱 배치 삭제, 여러 톱의 연속 절단 간격, 트램폴린 잡기4m 및 지상 잡기/공중 반동 우선순위를 보완한다. 기존 HumanCannon 코드/씬, `TightropeTrapeze.cs`, `CourseControlSwitcher.cs`, `TightropeSaws.cs`, `TightropeSettings.asset`과 이 Task 문서가 대상이다. 톱 삭제는 이전 개수 유지 지시의 이번 예외다. 사용자 Play 종료 확인 후 변경하며, 새 UI/에셋·플레이/자동 테스트·commit/push/PR 제출은 하지 않는다.
- 2026-10-22 사용자 후속 “저장하고 제출해” 요청으로 이번 그네/트램폴린 충돌 수정을 선별 커밋·push하고 dev 대상 새 PR로 제출한다. PR #213은 이미 병합됐으므로 같은 작업 브랜치에서 후속 PR을 만든다. 사용자 폰트·재질4개 변경은 제외/보존하고 실제/자동 테스트는 계속 실행하지 않는다.
