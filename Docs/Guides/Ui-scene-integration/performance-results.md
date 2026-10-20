# 공연 기록·C12 결과창
2026-10-21 · W3 저장: 61f4aa7, origin/feat/ui-scene-integration push 완료.

## 구성
- `Assets/Scenes/UI/PerformanceTransitionUI.unity` → `PerformanceTransitionUI/UI/C12ResultsCanvas`: Assets/Resources/Prefabs/UIs/ResultsUi/ResultsPanel.prefab 정본 인스턴스. 1~4인 결과 표시.
- 최신 사용자 지시: 1인도 C12에 실제 한 명만 표시. DevSoloResultCanvas·BlackTransitionCanvas 제거. 정본 인스턴스의 표시 컴포넌트를 SceneIntegration/ResultsPanelView·ResultsData(1~4인)로 대체하며 원본은 보존.
- 사망 안내 → 기존 대기 → 결과 → 검은 전환 → 공간 이동 순서는 유지. 확인 버튼은 표시하지 않고 자동 이동 초를 표시한다. 전원 준비/확인으로 전환하는 새 규칙 없음.
- 원본 프리팹·PNG·9-slice·폰트·GUID 수정 없음. 필요하면 Tools/Curtain Call UI/Install C12 results로 기존 씬에 멱등 적용.

## 데이터
- PerformanceRewards 호스트가 매 프레임, 사망 관찰 직전, 정산 직전에 기록을 요청한다. PerformanceSceneBinding이 공간 데이터로 샘플링하며 결과창 표시 여부에 의존하지 않는다.
- 외줄: 실제 불 타이머의 Elapsed, TightropeCourse.GetDistance/FinishDistance. 시작 전 종료는 ‘타이머 시작 전’ 표시.
- 인간대포: 첫 ArmedAt부터 Route.Clock 경과. 구간 변경/불의 안전 경계 정지에도 초기화하지 않는다. 기존 C3의 출발·도착 앵커와 수평 진행축을 재사용한다.
- 개인 거리는 0~코스 길이로 제한한 최댓값. 후퇴해도 감소하지 않으며 사망/이탈 확정 후에는 갱신하지 않는다. 공중 높이·측면 이동은 진척에 더하지 않는다.
- PerformanceResult의 ElapsedSeconds/CourseLength, 참가자의 MaximumDistance를 정산 시 불변 스냅샷으로 고정한다. RewardMessages에서 존재 여부와 함께 전달해 누락 기록을 0으로 오인하지 않는다. 기존 최근10개 공연 History에도 보존한다.
- C12 제목·일차·묘기·완료 시간·개인 최대거리·생존/사망/연결 종료·개인 정산·팀 호응·공동 자금 변화 연결. 별도 보너스를 임의 계산하지 않는다. 사망 지점과 최대 지점은 다를 수 있어 최대 지점에 추락 표식을 붙이지 않는다.

## 사용자 확인
1. Dev 배지 또는 Windows Ctrl+Shift+U / Mac Cmd+Shift+U로 원하는 일차 외줄/인간대포 진입.
2. 불 타이머 시작 전/후 종료를 비교한다. 공연 중 전진 후 후퇴·추락해도 결과 최대거리가 줄지 않아야 한다.
3. 1~4인 모두 C12의 실제 단원 수·완료 시간·거리·나 표시·정산을 확인한다.
4. 결과 대기 뒤 검은 전환과 다음 공간 복귀, 중복 결과/중복 지급이 없는지 확인한다.

## 검증
- 컴파일 PASS. 기록 모델 핵심 EditMode 2/2 PASS: 최대값/범위 제한, 사망 뒤 동결, 정산 뒤 동결, 타이머 미시작, 다음 공연 초기화.
- 실제 두 공연 씬의 최소 PlayMode 1/1 PASS(c12-play.json). 시작/종료 API로 기록, JSON 왕복, 1인 요약 및2인 합성 결과 표시 확인. 실제 조작·다중 접속 검증과 구분. 현재 Console 오류0/경고0. 캡처 c12-Tightrope.png·c12-HumanCannon.png, 정본 인스턴스 연결/문구 배치 확인.
- 실제 다중 클라이언트 동기화·재접속, 사람의 전체 공연 완주, 다른 화면비·Mac·빌드 미실행. 과거 임시 씬 미대상.

## 수정 파일
- 씬: Assets/Scenes/UI/PerformanceTransitionUI.unity.
- 표시/수집: Assets/Scripts/UI/SceneIntegration/PerformanceEndingPresentation.cs, PerformanceSceneBinding.cs.
- 설치: 같은 폴더 Editor/PerformanceResultsInstaller.cs 및 meta.
- 모델/전송: Assets/Scripts/Systems/Results/PerformanceResult.cs, Systems/Reward/PerformanceRewards.cs, RewardMessages.cs, Logic/PerformanceRewardModel.cs.
- 테스트: Systems/Reward/Tests/EditMode/PerformanceRewardModelTests.cs, UI/SceneIntegration/Tests/PlayMode/PerformanceResultsPlayTests.cs 및 meta.
- 문서: 이 가이드·next-plan.md 및 현재 Task setup/plan/todo/handoff.
