---
summary: C6 상호작용 안내판·내부 진행 링 정본과 표시 계약. 배치·호출·독립 검증 전에
status: active
updated: 2026-10-20
source: Task-20261019-061-cheer-gauge-ui
---
# C6 상호작용 안내판
## 언제 읽나
- C6 프리팹을 배치하거나 진행 링·표시 API·제작 원본·검증 제약을 확인할 때.
## 사실
- 정본: `Assets/Resources/Prefabs/UIs/Interaction/InteractionPrompt.prefab`. 독립 씬: `Assets/Scenes/Tests/Interaction/Interaction.unity`. 공개 Preview/안전 Builder/PlayMode 테스트: `Assets/Scripts/Systems/Interaction/`.
- 제공 Runtime/Builder는 private `Assets/_ThirdParty/curtaincall_interaction_v1/Assets/CurtainCallUI/Interaction/` 원위치 재사용. namespace는 `CurtainCall.UI.InteractionV1`/`.Editor`. 공개 복제하지 않는다.
- `InteractionPromptView.Show(key, action, availability, holdRequired, progress01, helperText)` / `SetProgress01` / `Hide` / `HideImmediate` / `ResetForRuntime`. Available/Unavailable × 일반/길게 네 조합. 대상·입력·누르기 시간·완료 판정은 호출자가 소유한다.
- 일반·불가는 fill0, 불가+길게는 빈 Track 유지. 100%에서도 자동 행동·숨김 없음. 새 대상은 Show에 명시적 진행률을 전달한다. 거부된 SetProgress는 캐시하지 않는다.
- Show 반복은 등장 모션을 재시작하지 않는다. 120ms 등장/100ms 퇴장·아래4UI·EaseOutCubic·unscaled·자동 수명 없음. Hide 반복 비재시작, 중간 Hide/Show는 현재 alpha/좌표에서 전환한다.
- C8과 달리 무효 enum/비유한 진행률/편집/비활성/미설정을 먼저 검사한다. 이 검사에 통과한 빈 키·행동만 즉시 숨김. 유한값 Clamp, 실제 제어문자·U+2028/U+2029는 공백, richText/parseCtrlCharacters Off.
- 기본 보조: 길게는 `길게 누르기`, 불가는 `사용 불가`, 일반 가능은 숨김. 보조 없음 행동 Y=-24, 있음 Y=-12. 불가 글자 alpha1.
- Overlay1920×1080/Match0.5/Canvas PPU100/Sprite PPU200. 중앙384×80·(0,-144), 키캡40×40·(24,-20), 링64×64·(12,-8)로 중심 일치. Radial360/Top/Clockwise. 안내판 11레이어, Medium24/보조18.
- 실제 sorting HUD0/C6 3/Controls10. 기존 Notifications5보다 먼저 표시하는 계약이며 독립 C6 씬에는 알림을 추가 연결하지 않는다. C6 raycast Off, Controls만 raycaster·EventSystem/InputSystemUIInputModule1개.
- 키트 meta 제외49실파일/PNG15, 생산8=신규 링2+기존 PerformanceHud 정본6. 신규2만 importer 설정, 재사용6은 PNG SHA·Sprite/importer 대조 후 읽기 전용. Source/Tools/Docs/Previews/폰트/라이선스 전체 보존. 누적98종과 현재 키트 파일 수를 구분한다.
- 생성 진입점 `InteractionProjectBuilder.Build()`: Play/컴파일/import/PrefabStage/dirty 씬/기존 prefab·scene 충돌 사전 차단. 소유 자산만 저장, 전역 SaveAssets/PlayReset/자동 삭제 없음. 실패 시 부분 결과를 보존하며 기존 출력 재생성 금지.
- Static Medium 전용 생성. 문맥 C1의 Dynamic TMP는 새 씬 인스턴스에서만 Static으로 대체. 정본 폰트·HUD·ProjectSettings/Packages는 수정하지 않는다.
- 선행 Awake의 Show는 enabled+activeInHierarchy로 검사해 자기 Awake 전에도 허용하고 기존 runtimePresentation을 보존한다. reload 설정은 변경하지 않으며 양쪽 OFF 지원은 주장하지 않는다.
- 삭제·커밋·push·실게임/네트워크·실제 키 입력/진행 타이머/완료 이벤트·사운드·딤은 이번 범위 밖이다.
## 확인 방법
- [전수 목록](../../../Tasks/Done/Task-20261019-061-cheer-gauge-ui/interaction-inventory.csv), [검증 기록](../../../Tasks/Done/Task-20261019-061-cheer-gauge-ui/interaction-verification.md).
- 신규 PlayMode16/16 통과. 선행 Awake·초기 컴파일/조립·화면/입력 수집 실패는 검증 문서에 보존한다. 화면·기존 회귀·표준 검사 최종 결과는 해당 문서가 기준이다.
- 문맥 HUD는 Start/전환/Reset에서 합성값으로 표시하며 게임 연결이 아니다. 기존 안전한 Static 문맥 폰트는 유지하고 Dynamic만 새 씬에서 대체한다. Controls는 중앙 C6/P1/W6·좌하단 인벤토리를 피하는 양쪽 배치다.
- Pipeline0.8.0-exp.1의 simulate_key 호출 성공만으로 Submit 성공을 판정하지 않는다. Enter는 정상 프레임에 KeyboardState를 QueueStateEvent한 뒤 호출 수로 확인한다. 직접 onClick/OnSubmit 호출은 실제 입력 증거가 아니다.
- 공유 Dynamic폰트2개/과거 Buttons 글리프 실패 원인 미결·private 무관 dirty는 그대로 보존. 최신 테스트 통과와 원인 해결을 혼동하지 않는다. 사람 외형 승인·공동·실게임 검증은 별개다.
