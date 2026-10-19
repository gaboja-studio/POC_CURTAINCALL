# Result

## 병합 결과

| 순서 | Task | PR | 결과 | 비고(충돌 등) |
|---|---|---|---|---|
| 1 | Task-20261016-052 | [#136](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/136) | MERGED | PM이 2026-10-16 10:40 UTC Squash 병합, `d23d57e`. 로컬 fast-forward, 충돌 없음 |

## 통합 자동 검사

- 2026-10-16, `integration/start-lobby`의 `d23d57e` 기준. PR 검사 5개 SUCCESS.
- `verify-fast`: PASS. 기존 다른 문서의 길이 WARN 5개, hard 0.
- `verify-scope`: SKIP — PM @MoHoDu의 integration 브랜치(작업 PR의 구역 검사는 PASS).
- `verify-unity -Compile -RunTests -Mode PlayMode`: 컴파일 PASS, 기존 회귀 테스트 46/46 PASS, skipped 0. StartLobby 전용 자동 테스트는 없음.

## 도구 기반 2인 LAN 확인

- 일시: 2026-10-16 19:49 KST 검증용 보조 Editor 실행 후 진행. AI가 로컬 메인 Editor + MPPM 보조 Editor 2개를 제어. 사람 참여 없음.
- 실행: 실제 Start → LAN 버튼 콜백 → `127.0.0.1:7777` 참가. 호스트 DevMode OFF(최소 2명); 참가자는 기존 DevMode 캐시 ON(최소 인원 판정은 호스트).
- 입력 구분: 메뉴는 Button.onClick 콜백, 명단/레버는 근처로 위치를 옮긴 뒤 합성 F 입력. 차단 조건은 Use/Prompt 직접 호출. 마우스 클릭·사람 이동 조작감 검증 아님.

| 재현 | 예상 | 실제 |
|---|---|---|
| 호스트 혼자 서명 후 레버 | 최소 2명이라 시작 거부 | PASS — min=2, usable=False, Waiting 유지 |
| 참가자 LAN 접속 | 두 화면 인원·자리·기존 서명 일치 | PASS — Host/Client 각 players=2, slot 0/1, 호스트 기존 서명 전달 |
| 한 명 미서명 / 참가자 레버 | 시작 거부 | PASS — 미서명 안내 / 방장 전용 안내, Waiting 유지 |
| 참가자 명단 F | 준비 상태 동기화 | PASS — 양쪽 ready=2, AllReady=True |
| 준비한 참가자 나가기 → 재입장 | Start 복귀, 호스트 명단 제거, 재입장 가능 | PASS — 호스트 players=1/ready=1 → 재입장 players=2, 새 clientId=2, F 재서명 |
| 전원 준비 후 호스트 레버 F | 두 화면 첫 공연 진입 | 씬·상태 전환 PASS — 양쪽 Playing/Performance, DummyPerformance. 캐릭터 낙하는 아래 별도 이슈 |
| EndPerformance → StartEscape → EndEscape(true) API 단축 | 두 화면 엔딩 로비 복귀 | PASS — Lobby/Ended/Ending, 같은 연결 유지, ready=0, 위치 (-1.80,0.05,1.40)/(-0.60,0.05,1.40) |
| 로비 레버 F → 양쪽 재서명 F → 레버 F | 같은 방에서 두 번째 런 시작 | PASS — 양쪽 Playing/Performance, DummyPerformance, players=2 |
| 호스트 LeaveToStart | 두 화면 Start/Offline 및 이유 표시 | PASS — 호스트 '방을 닫았습니다.', 참가자 'Disconnected due to host shutting down.'(영문) |

- Console: 클라이언트 error 조회 결과 없음. 메인에는 MPPM `Unknown Player` Assert 및 `MainPlayerSystems.Listen` NullReferenceException이 발생함. 시나리오 등록 없이 보조 에디터를 직접 실행한 검증 경로에서 발생했으며, 게임 오류 0/전체 Console 정상으로 처리하지 않음.
- 정리: Play 종료, 검증용 보조 Editor 종료, 기존 DevMode ON 복원. Start 씬 dirty=False, 게임 코드·씬·설정 파일 변경 없음. Library/VP 캐시는 삭제하지 않음.

## 공동 테스트

- 일시·참여: 미실시. 위 2인 런타임 제어를 사람 공동 테스트로 대체하지 않는다.
- 결과: SKIPPED. Task 상태 submitted 유지, Integration testing. 아래 PM 예외 승인으로 dev 선반영을 진행하되 integrated/done·브랜치 정리는 하지 않는다.
- 남은 확인: plan의 화면·조작감/카메라, 온라인 방 코드·잠금/해제·재참가, 진행 중 신규 참가 거부, 설정 저장·빌드 화면 모드. 4인·별도 PC 연결도 미검증.
- 2026-10-18 후속: 실제 2인(호스트 PM)이 Integration-performance-scene-link 공동 테스트에서 시작→로비→공연→엔딩 로비 복귀 흐름 PASS(PM 보고). 위 남은 확인 중 온라인 방 코드·설정 저장 등은 별도 기록 없음.

## 남은 이슈

- 실제 공연 연결 미완료: 현재 저장된 공간 전환은 Single, 대상은 DummyPerformance(→ 2026-10-17~18 Task 057·058에서 실제 줄타기·인간 대포 연결과 재사망 수정으로 해결). 양쪽 캐릭터는 로비 좌표를 유지한 채 낙하했고, 활성 Collider 조회에 플레이어 외 바닥 충돌체가 없었음. 이번에는 전환만 검증했으며 정상 공연 플레이 PASS가 아니다. 기존 더미 연결의 제약으로 보이고, 실제 공간 연결/스폰 배치 범위는 별도 Task 배정 필요(이번에 코드·씬 수정 또는 후속 Task 생성하지 않음).
- MPPM 검증 환경: `unity status`에는 보조 인스턴스가 안 나왔지만 정확한 복제본 project-path로는 연결됨. 공식 Scenario 경유 재검증 권장. 환경 오류를 제거한 사람 공동 테스트 필요.
- 호스트 종료 안내는 참가자 화면에 영문으로 표시됨. 표시 동작은 확인했으나 UX 승인은 아직 없음.

## 테스트 씬 처리
- Task-20261016-052: `Assets/Scenes/Start_Lobby/Start.unity`, `Lobby.unity` 유지(실제 게임 씬). PM @MoHoDu, 2026-10-16, setup 기록.

## dev 선반영 예외 승인
- 2026-10-16 PM @MoHoDu: "현재 상태로 먼저 병합". 사람 공동 테스트·온라인 미검증과 DummyPerformance 낙하를 알고 dev 선반영 승인.
- Task submitted / Integration testing·Active 유지. 테스트 PASS 변경, integrated/done 처리, 후속 Task 임의 생성, 브랜치 삭제는 하지 않는다.
- 최신 dev `8e814b6`(인간 대포 통합 포함)와 `e8b6794`의 `git merge-tree --write-tree` 충돌 없음. 이는 Git 병합 가능 확인이며 해당 조합의 Unity 검증 완료가 아니다.
- 실제 제출·검사·병합 결과는 [시작·로비 → dev PR](https://github.com/gaboja-studio/POC_CURTAINCALL/pulls?q=is%3Apr+head%3Aintegration%2Fstart-lobby+base%3Adev)에서 확인한다. Merge commit으로 반영하며 보호 규칙·검사 우회 없음.

## 정리

- [ ] 작업 브랜치 삭제
- [ ] `integration/*` 브랜치 삭제
- [x] `dev` 병합 완료 (PR #145, d2d002c, 2026-10-16)
- [x] 이 폴더를 `Integrations/Done/`으로 이동 (2026-10-18 기록 정리, 브랜치 삭제는 미실행)
