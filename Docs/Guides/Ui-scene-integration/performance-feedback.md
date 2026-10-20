# C10 피드백·공연 기본 자금
2026-10-21 · 실제 외줄/인간대포와 공통 UI 적용. 아직 commit/push 하지 않음.

## 구성·그래픽
- `CommonPerformanceUI.unity/UI/CommonFeedbackCanvas/40_TransientFeedback/C10_Feedback`에 신규 `Assets/Resources/Prefabs/UIs/Feedback/Feedback.prefab` 인스턴스.
- 완성 C10 프리팹이 없는 것을 먼저 확인했다. Assets/_ThirdParty의 제공 FeedbackView/Motion과 Builder 규격(12 Image/2 TMP, 360×80, 중앙 Y=-48, 900ms)을 사용해 조립했다. 다른 인계 package는 사용하지 않았다.
- 제공 PNG 4장을 동일 파일명·바이트로 `Assets/Resources/Images/UIs/Feedback/`에 복사하고 신규 복사본만 Sprite 200PPU/무압축/원본 크기로 import. 원본은 Default Texture/NPOT 축소 상태이므로 직접 변경하지 않았다. 기존 별·엄지 아이콘 Sprite/폰트 재사용, 새 외곽선 재질만 생성. 원본·9-slice 변경 없음.
- 편집: 위 C10_Feedback RectTransform 위치 조절. FeedbackView Inspector의 환호/야유/손 잡아줌 편집 미리보기 사용 가능. Play 시작 시 미리보기 초기화.

## 데이터·수명
- PerformanceRewards.Changed 스냅샷의 실제 개인 Amount 변화 → 단원 Slot 색의 환호 +값/야유 −값. 상한/하한으로 잘린 경우 실제 변화량만 표시한다.
- 첫 상태·중복 revision·건너뛴 revision·규칙 변경은 동기화로 취급한다. 사망/이탈로 0점이 되는 것은 야유가 아니다. 네트워크 상태가 중간 revision을 건너뛰면 C1은 최신 값을 표시하고 C10은 해당 누적 변화를 생략한다.
- 최대4개/3초 이내의 짧은 대기열로 동시 반응을 표시. 관전·입력 차단·종료/공간 해제 시 표시/대기열 초기화. 기존 C1/C2/C3/P1 로직·사용자 배치 유지.
- 실제 PiggybackSystem.HandholdLanded → 손 잡아줌(숫자 없음). 임의 점수 추가 없음. 대체된 손잡기 IMGUI/목마 디버그는 공통 C10이 소유하는 동안 억제, 해제 시 복원.
- Dev 음성 상자는 공연 C3 아래로 이동, 메뉴·관전·결과에서는 숨기고 공통 UI 해제 시 복원. 기능이 남은 외줄 하단 조작/관문 안내는 유지.

## 기본 자금·C12
- 모든 공연 정산: **시작 참가 명부 수 ×10 + 기존 개인 Settlement 합계**. 사망·이탈·실패에도 기본 자금 지급. 중도 접속 인원으로 시작 명부를 바꾸지 않는다.
- 실패의 기존 호응 정산은0. 예: 4인 실패=40, 4인 성공/기존 보상70=110, 1인 Dev 실패=10.
- PerformanceResult.BaseReward/BonusReward/Income을 호스트 확정 장부에 한 번만 반영한다. 기존 공연 ID 중복 정산 차단·세대 초기화 유지. 전송된 참가 결과에서 동일 공식을 복원한다.
- C12: 기본 자금(N명×10), 호응 보상, 이번 공연 총액과 공동 잔액 변화. 개인 거리 옆 +값은 기존 개인 호응 정산이며 기본 자금은 공통 행에서만 표시한다.

## 최소 검증·사용자 확인
- 컴파일 PASS. 보상 EditMode47/47, C10 동기화 필터1/1 PASS.
- 실제 두 공연 연결 PlayMode: C10 환호/야유·자동 숨김·손잡기 숫자 없음·Dev창 억제, 불 시작 기록·실패 기본금10·DTO 왕복·C12·커튼→대기실 검사1/1 PASS(32.99초). 합성 보상/종료 API를 사용하며 사람 완주 검증은 아니다.
- 캡처 `.harness/local/c10-Tightrope.png`, `c10-HumanCannon.png`, `c12-solo-Tightrope.png`, `c12-solo-HumanCannon.png`. 실행 프레임 진행 및1920×1080 화면 확인.
- 사용자: Dev 배지로 두 공연 진입 후 실제 회피/절단/매달리기 등 기존 호응 조건에서 C1 변화와 C10 일치 확인. 손잡기 착지 시 숫자 없는 안내, 사망 후 야유 잔상 없음, 실패도 결과/대기실 장부 기본금 지급 확인.
- 미실행: 실제2~4클라이언트·끊김/재접속, 물리 입력으로 손잡기/완주, Mac·다른 화면비·빌드·전체 프로젝트 테스트. 과거 임시 씬 제외.

## 수정 파일
- `Assets/Scenes/UI/CommonPerformanceUI.unity`, 신규 `Resources/Prefabs/UIs/Feedback/`, `Resources/Images/UIs/Feedback/` 및 meta.
- `UI/SceneIntegration/PerformanceFeedback.cs`, `Editor/PerformanceFeedbackInstaller.cs`, `Editor/PerformanceFeedbackTests.cs` 및 meta.
- `UI/SceneIntegration/CommonPerformanceUI.cs`, `PerformanceSceneBinding.cs`, `PerformanceEndingPresentation.cs`, `UiRuntimeFontFallback.cs`, `Tests/PlayMode/PerformanceResultsPlayTests.cs`.
- `Systems/Results/PerformanceResult.cs`, `Systems/Reward/Logic/PerformanceRewardModel.cs`, `Systems/Reward/Tests/EditMode/PerformanceRewardModelTests.cs`, `AudienceReactionTests.cs`.
- `MainStage/Tightrope/Piggyback/PiggybackSystem.cs`, `Systems/Voice/VoiceChatDevOverlay.cs`, 이 가이드·결과/계획 가이드·Task 문서.
