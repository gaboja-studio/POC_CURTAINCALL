# Handoff

## 지금 상태

- 2026-10-15 사용자 실제 전달 완료·Unity 저장 확인 후 착수. Status working.
- 수정: PlatformControlScheme.Tick이 CameraViewDirector.Current가 1인칭·LocalPlayer가 이 플레이어·ViewForward 유효일 때만 시선 기준 입력을 길이 1로 제한 후 CourseForward/Right에 투영(ToCourseInput). 그 외(3인칭·관전·다른 플레이어·카메라 없음·코스 방향 없음)는 기존 SetMoveInput(Posture, Move) 그대로. 이동 키를 누른 채 시점이 바뀌면 키를 모두 뗄 때까지 직전 기준 유지(MoveBasis, 사용자 결정 A). PlayerMover·카메라·입력 에셋·외줄 규칙 미수정.
- 범위 확대(2026-10-15 PM): CameraViewKind.FreeThirdPerson 추가(enum 끝, 기존 에셋 값 불변). CameraViewDirector — 비고정은 1인칭과 같은 마우스 yaw/pitch·커서 잠금, 카메라는 FreeThirdPersonPose(발+LookHeight 중심, pitch 0일 때 고정 3인칭과 같은 거리·높이를 시선만큼 회전, 시선 앞 LookAhead를 봄). UsesMouseLook(1인칭·비고정)이면 ViewForward가 yaw 기준이고 기본 이동이 시선 기준. 1인칭↔비고정 전환은 시선 이어짐, 관전·소멸 중에는 1인칭·비고정 요청 제외. 기본 3인칭·외줄 구도·관전은 고정 유지.
- 소유 공용 파일: PlatformControlScheme.cs, ControlRulesTests.cs, CameraViewsPlayTests.cs, CameraViewPreset.cs, CameraViewDirector.cs 수정(.meta 변경 없음), 새 FreeThirdPerson.asset(+.meta, Kind FreeThirdPerson·PitchLimit 35, 나머지 기본값). 전용 FirstPersonMovement.unity: CameraViews 씬을 에디터 SaveScene 복사(GUID d0cb69f… 유지) 후 루트 이름 FirstPersonMovement Test, 3인칭 비고정 구역 z 18~26(+경계 표시), 바닥 가장자리 벽 4개 추가·저장(dirty false). 빌드 목록 미추가.
- 키 고착 완화(2026-10-15, 사용자 부재 중 자율 진행): 사용자 보고 "고정↔1인칭 이동 때 A 방향 미끄러짐, 안 누르면 미끄러지고 누르면 멈춤" = 게임 쪽 키보드 상태에서 키가 눌린 채 남고 반대 키가 상쇄하는 패턴(지난 W 고착과 같음). CameraViewDirector가 커서 잠금 변경·잠금 해제·앱 포커스 회복 때 모든 Keyboard에 RequestSyncCommand를 다음 프레임·0.25초 뒤 2회 보내 실제 키 상태로 다시 맞춘다(실제로 누르는 키는 유지 → A 방식 영향 없음). 실제 에디터 확인: 실제 키보드에 '눌린 채 남은 A'를 넣고 3인칭→1인칭, 1인칭→3인칭, 비고정→고정 전환 각각 0.7초 안에 posture −1→0 해제, 전환이 없으면 유지. 물리 키 고착 자체는 합성 입력으로 재현 불가(전환 직후 0/50/150/400ms 떼기·Game 뷰 포커스 이탈 모두 고착 없음, 포커스 이탈 시 Input System이 장치를 리셋) — 근본 원인 미확정, 실제 키보드 확인 필요.
- 진단 도구: 전용 폴더 FirstPersonMovementInputProbe.cs(테스트 전용, 씬 미저장). Play 중 붙이면 프레임별 키·입력·시점·커서·포커스 창·InputGate와 포커스/장치 이벤트를 Logs/fpm-input-probe.csv(git 제외)에 남긴다. 붙이는 법: eval로 `new GameObject("FPM Input Probe (temporary)").AddComponent<CurtainCall.Player.Tests.FirstPersonMovementInputProbe>()`.
- 구역 밖 churn: EditorBuildSettings는 Play 때 활성 씬이 빌드 목록에 자동 추가됨(CameraViews·FirstPersonMovement 모두 확인, Netcode 추정). 사용자 지시대로 매번 되돌림(TMP 폰트 포함). 제출 전 git status 확인.
- PR #83으로 배정 문서 수신 완료, 착수 HEAD 42779cccabb0b95c45b3c656c339f9ae6cedc0fb. 사용자 지시에 따라 반복 sync 없이 현재 브랜치에서만 작업. fetch/switch/merge를 수행하는 start-work.ps1은 실행하지 않고 확인된 착수 상태를 문서에 반영.
- fix/first-person-movement → integration/common-game-systems (Squash). commit/push/PR은 별도 요청 전 금지.
- Jira CC-131, 좁은 수정 구역·회귀·공동 테스트 초안 3개는 setup/plan 참조
- PM 전용 빈 FirstPersonMovement 씬·스크립트 폴더 생성, 제품 이동 코드는 변경하지 않음
- PR: 없음

## 다음 할 일

1. 남은 사람 확인: proto_0.0.2 외줄 위·플랫폼 이동, Space 점프가 변경 전과 같은지 + 공동 테스트 1~3(plan, 3은 호스트 A·참가 B 2명). FirstPersonMovement 씬 최종 확인은 완료. 키 고착 재발 시 진단 기록기를 붙인다.
2. Play로 생긴 EditorBuildSettings churn은 되돌림. 반복 sync 금지. "제출해줘" 때 integration/common-game-systems로 Squash PR. 035 범위에 포함하지 않는다.

## Verification

- 0 구역: PASS (10-15 키 고착 완화 반영 후 check-work, EditorBuildSettings churn 되돌린 뒤)
- EditMode 전체: 55/55 PASS (10-15 키 고착 완화 반영 후)
- 사용자 보고(범위 확대 후 1차 사람 확인, 10-15): "고정↔1인칭 이동 때 A 방향 미끄러짐, 안 누르면 미끄러지고 누르면 멈춤" → 키 고착 완화 반영.
- 4 실행 로그(키 고착 완화 후, 사람 실입력 최종, 10-15): PASS — FirstPersonMovement 씬 1인칭·고정·비고정 사용자 "이제 아주 잘된다 이대로 좋아". 미끄러짐 재발 없음(근본 원인은 미확정, 완화로 해소).
- 1 문서/규칙: PASS (10-15)
- 2 컴파일: PASS (10-15)
- 3 테스트: PASS (PlayMode, 14/14, 10-15)
- Console: 오류 0(이전 Pipeline timeout 기록 3건 제외), 경고는 _ThirdParty 모델 import뿐.
- 4 실행 로그(수정 전, 사람 실입력): PASS-재현(10-15 CameraViews LAN 호스트·기본 Platform 규칙, 1인칭 구역. 시선 (-0.10,-0.99)에서 W → z 7.34→10.73(시선 반대=코스 +Z), S → z 10.43→7.98, 시선 (-0.50,-0.87)에서 D → x -0.96→3.06(코스 +X), W → z 9.50→14.35 후 z14 이탈로 3인칭. 속도 약 3m/s). 합성 simulate_key는 Game 뷰 포커스 설정(PointersAndKeyboardsRespectGameViewFocus)으로 액션에 미전달되어 증거에서 제외.
- 4 실행 로그(수정 후, 사람 실입력): FAIL-사용자 체감(10-15, 구역 밖 churn 2개 사용자 요청으로 되돌린 뒤). 사용자: "W 후 미끄러지는 느낌, 다른 키는 그 방향으로 안 가고 버벅이거나 계속 미끄러짐, 지형에서 떨어짐". 기록: 시선 +X 1인칭에서 W 3.3초 연속(Move=1) → x 0.55→10.27 약 3m/s 시선 방향 정상 → x=10 구역 옆면 이탈로 3인칭 전환 → 같은 W가 코스 +Z로 90° 꺾임 → 이후 W 반복으로 z 30(바닥 끝) 넘어 추락. 52~68초 동안 리더에 S/A/D 입력은 잡히지 않음(Move 1/0, Posture 0). 새 Console 오류·경고 없음. 기록기가 0.3초마다 eval(Roslyn)을 메인 스레드에서 실행해 프레임 끊김을 만들었을 가능성 있음(검증 도구 영향, 미분리).
- 결정·반영: 사용자 추천안 A — 이동 키를 누른 채 시점이 바뀌면 키를 모두 뗄 때까지 직전 기준 유지(PlatformControlScheme.MoveBasis, 플레이어별 상태는 규칙 내부 ConditionalWeakTable). 재컴파일 PASS, EditMode ControlRulesTests 14/14, PlayMode CameraViewsPlayTests 13/13(신규: 누른 채 구역 이탈 시 시선 유지·재입력 시 코스 기준). 이번 실행 후 구역 밖 churn 없음.
- 미해결 관찰: 사용자는 추락 전 S를 계속 눌렀다고 기억하나 리더는 Move=+1(W)만 읽음. Move 바인딩은 키보드 W/S뿐이라 게임 쪽 키보드 상태에서 W가 눌린 채 남고 S가 미전달된 것으로 보임(이동 규칙보다 앞단). 원인 미확정. 2차 확인에서 재발 없음(장치 키 기록 열은 Keyboard.current가 실제 장치가 아니어서 비어 무효).
- 4 실행 로그(A 반영 후, 사람 실입력 2차, 기록 간격 약 1초): PASS(사용자 "그 외에는 모두 좋았어"). 1인칭 시선 +X·−Z 각각에서 W/S/A/D 시선 기준 앞·뒤·왼쪽·오른쪽, 약 3m/s. W 누른 채 z=6 이탈 → 3인칭 전환 후에도 −Z 유지, 재입력 시 코스 +Z. Game 뷰 포커스 유지, 키 고착·미끄러짐 재발 없음. 사용자 체감: 뒤를 보고 나간 뒤 재입력 때 방향이 반전되어 잠시 토글처럼 느껴짐 → A 설계상 동작으로 설명, 사용자 A 유지 결정. 3인칭 D·W+D 대각은 1초 간격이라 기록 판정 불가(대각 속도는 PlayMode 자동 테스트로 확인).
- 5 공동 테스트: SKIPPED(미실행, 사용자 확인으로 진행 예정). proto_0.0.2 외줄 사람 확인도 미실행.
