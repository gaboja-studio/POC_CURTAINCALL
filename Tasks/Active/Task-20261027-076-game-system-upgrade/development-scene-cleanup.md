# 개발 씬 정리

## 상태

2026-10-28 사용자 정리 요청에 따른 목록. 최초 삭제와 요약 이후 재시도는 권한 확인에서 차단되어 미삭제로 유지했다. 이후 사용자가 현재 대화에서 **목록에 있는 씬 46개와 전용 테스트 8개 삭제를 승인해**라고 직접 승인한 뒤 Unity AssetDatabase로 아래54개와 대응 `.meta` 삭제를 완료했다. 삭제된 씬의 Build Settings 항목도 제거했다. 검증: 대상 파일/meta 잔존0·삭제 씬 빌드 항목0·enabled11개 존재·Levels5개 및 disabled DummyEscapeRoute 보존·미저장 씬0. 폴더·머티리얼·프리팹·외부 에셋은 삭제하지 않았다.

## 유지

- Start 실행 및 공통 추가 로드: Build Settings enabled 11개(Start, Lobby, Tightrope, HumanCannon, WaitingRoomBase, CommonPerformanceUI, PerformanceTransitionUI, CommonWaitingRoomUI, CommonInteractionUI, CommonMenuUI, 외부 performance_background).
- `Assets/Scenes/Tests/Systems/DummySpaces/DummyEscapeRoute.unity`: 탈출 설정의 실참조가 남아 삭제 보류. 기존 빌드 목록 disabled 상태도 유지. 정식 탈출 연결 완료로 보고하지 않는다.
- `Assets/Scenes/Levels/` 5개: 맵 배치용 작업 씬이며 개발 기능 테스트 씬과 구분해 보존. 미연결 escape_route를 이름만 보고 정식 탈출로로 채택하지 않는다.
- `_ThirdParty` 전체: 삭제 대상 아님.

## 삭제 승인 대상 — 씬46개

```text
Assets/Scenes/Prototypes/proto_0.0.2.unity
Assets/Scenes/SampleScene.unity
Assets/Scenes/Tests/BackstageHud/BackstageHud.unity
Assets/Scenes/Tests/BasicPanels/BasicPanels.unity
Assets/Scenes/Tests/Captions/Captions.unity
Assets/Scenes/Tests/CheerGauge/C1CheerGauge.unity
Assets/Scenes/Tests/CommonButtons/CommonButtons.unity
Assets/Scenes/Tests/CommonIcons/CommonIcons.unity
Assets/Scenes/Tests/Guides/Guides.unity
Assets/Scenes/Tests/Interaction/Interaction.unity
Assets/Scenes/Tests/MainStage/Tightrope/Piggyback/Piggyback.unity
Assets/Scenes/Tests/MainStage/Tightrope/TightropeDamage/TightropeDamage.unity
Assets/Scenes/Tests/MainStage/Tightrope/TightropeFire/TightropeFire.unity
Assets/Scenes/Tests/MainStage/Tightrope/TightropeResults/TightropeResults.unity
Assets/Scenes/Tests/MainStage/Tightrope/TightropeRider/TightropeRider.unity
Assets/Scenes/Tests/MainStage/Tightrope/TightropeSaws/TightropeSaws.unity
Assets/Scenes/Tests/MainStage/Tightrope/TightropeUI/TightropeUI.unity
Assets/Scenes/Tests/Notifications/Notifications.unity
Assets/Scenes/Tests/PerformanceHud/PerformanceHud.unity
Assets/Scenes/Tests/ResultsUi/ResultsUi.unity
Assets/Scenes/Tests/ScheduleUi/ScheduleUi.unity
Assets/Scenes/Tests/SettingsUi/SettingsUi.unity
Assets/Scenes/Tests/ShopUi/ShopUi.unity
Assets/Scenes/Tests/Spectator/Spectator.unity
Assets/Scenes/Tests/Systems/CameraViews/CameraViews.unity
Assets/Scenes/Tests/Systems/Campaign/CampaignFlow.unity
Assets/Scenes/Tests/Systems/ControlRules/ControlRules.unity
Assets/Scenes/Tests/Systems/DevMode/DevMode.unity
Assets/Scenes/Tests/Systems/DummySpaces/DummyExploration.unity
Assets/Scenes/Tests/Systems/DummySpaces/DummyPerformance.unity
Assets/Scenes/Tests/Systems/DummySpaces/DummyWaitingRoom.unity
Assets/Scenes/Tests/Systems/FirstPersonMovement/FirstPersonMovement.unity
Assets/Scenes/Tests/Systems/GameplayTelemetry/GameplayTelemetry.unity
Assets/Scenes/Tests/Systems/HumanoidCharacters/HumanoidCharacters.unity
Assets/Scenes/Tests/Systems/HumanoidCharacters/MixamoAnimationPreview.unity
Assets/Scenes/Tests/Systems/HumanoidCharacters/SequentialHumanoidCharacters.unity
Assets/Scenes/Tests/Systems/NetworkPlayerSync/NetworkPlayerSync.unity
Assets/Scenes/Tests/Systems/NetworkSession/NetworkSession.unity
Assets/Scenes/Tests/Systems/PerformanceLink/PerformanceLink.unity
Assets/Scenes/Tests/Systems/PlayerControl/PlayerControl.unity
Assets/Scenes/Tests/Systems/Reward/PerformanceReward.unity
Assets/Scenes/Tests/Systems/SpaceLink/SpaceLink.unity
Assets/Scenes/Tests/Systems/Survival/InjurySurvival.unity
Assets/Scenes/Tests/Tooltips/Tooltips.unity
Assets/Scenes/Tests/TransitionCurtain/TransitionCurtain.unity
Assets/Scenes/Tests/WorldLabels/WorldLabels.unity
```

## 검증 정리

실제 WaitingRoom/HumanCannon/Tightrope·공통 UI/배경·순수 로직 검증은 유지한다. 실제 대기실 HUD 실패는 제외하지 않는다.

이관한 코드:
- SpaceLinkPlayTests: 개발 허브·더미 전용3건 제거, 순수 경로와 실제 대기실5건 유지.
- CameraViewsPlayTests: 개발 CameraViews/proto 전용17건 제거, 실제 대기실 재진입 유지.
- PerformanceLinkPlayTests: prototype 대신 실제 Tightrope 사용. 저장 설정·공연별 매핑 검증 유지.
- PerformanceBackgroundPlayTests: 더미 씬 대신 임시 빈 씬의 수명 이벤트 사용.
- AnimationClipPreviewTests: 삭제 대상 씬 존재 assertion만 제거, Avatar·클립·prefab 검증 유지.

아래 전용 fixture8개는 `.cs`와 대응 `.meta` 삭제를 완료했다. 해당 개발 씬 전용 검증은 더 이상 컴파일·테스트 검색 대상에 포함되지 않는다.

```text
Assets/Scripts/Systems/DummySpaces/Tests/PlayMode/DummyPerformancePlayTests.cs
Assets/Scripts/Systems/DummySpaces/Tests/PlayMode/DummySpaceCanvasPlayTests.cs
Assets/Scripts/Systems/DummySpaces/Tests/PlayMode/DummySpaceFlowPlayTests.cs
Assets/Scripts/Systems/DummySpaces/Tests/PlayMode/DummySpaceRealtimePlayTests.cs
Assets/Scripts/Systems/CommonIcons/Tests/PlayMode/CommonIconsPlayTests.cs
Assets/Scripts/Systems/Notifications/Tests/PlayMode/NotificationsPlayTests.cs
Assets/Scripts/Systems/BackstageHud/Tests/PlayMode/BackstageHudPlayTests.cs
Assets/Scripts/Systems/PerformanceHud/Tests/PlayMode/PerformanceHudPlayTests.cs
```

## 남은 작업과 제약

- AssetDatabase 삭제·Build Settings 정리·전용 fixture8개 검증 제외 완료. 실행 검사는 아래 최종 결과와 Task log를 따른다.
- 공용 prefab·폰트 생성과 개발 씬 생성이 결합된 빌더는 보존한다. `*Builder.cs`에서 InitializeOnLoad/DidReloadScripts/AssetPostprocessor 자동 재생성 훅은 발견되지 않았다. 기존 수동 개발 메뉴는 실행하지 않으며 메뉴 자체 폐지·빌더 분리는 후속 정리 대상이다.
- 실제 회귀의 삭제 예정 씬 경로 참조0개. 남은 개발 씬 이름 TestCase는 로드가 아닌 순수 판정 검증이다.
- 최종 검사 완료: check-work 안전성·문서/규칙·컴파일 PASS, 전체 EditMode1049/1049. 실제 씬 회귀는 PerformanceLink11·SpaceLink6·PerformanceRoute5·PerformanceBackground11·CameraViews1·WaitingRoomHud2, 합계36/36 통과(실패/생략0). 전체 PlayMode·실제 입력·2인 공동/사람 플레이는 미실행.
