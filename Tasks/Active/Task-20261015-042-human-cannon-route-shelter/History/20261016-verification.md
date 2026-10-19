# 042 구현 검증 — 2026-10-16

## 대상·방법
- 브랜치 feat/human-cannon-route-shelter, 기반 HEAD dae56afe38f7fa939fcc103f22d158262ad9a664 + 로컬 미커밋 구현.
- Unity 6000.3.25f1, C:/Unity/POC_CURTAINCALL, Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity.
- 실행은 LAN 호스트 1명(actor=0). 실제 4인·원격 참가자·사람 직접 조작 결과가 아니다.
- 위치 fixture는 eval로 캐릭터/장치 위치를 배치했다. 동작 입력은 임시 Keyboard의 Dynamic InputSystem 이벤트(F/R)를 거쳐 PlayerInputReader→기존 조작 규칙→요청 핸들러로 전달했다.
- 메서드를 직접 호출해 입력 성공으로 기록하지 않았다. 이동 경로 전체의 걷기·외팔 접근·3인 합류는 공동 테스트에 남긴다.

## 실행 관찰
| 재현 | 예상 | 실제 |
|---|---|---|
| 손잡이 (-9,0.05,0), F | 트램폴린 소유 | carrier=0, CanStart(0)=true |
| 다시 F | 출발 발판 5초 이동·불꽃 예고 | Moving, 플레이어 z4.19→16.09, Simulated=false, 예고13.9초 |
| 이동 완료 | 목적지·운반 위치와 조작 복구 | Arrived, 플레이어(-9,0.05,19), 장치(-9,0.05,21.8), Simulated=true |
| 아래 승강 발판에서 잠긴 채 F | 상승 차단 | Waiting, unlocked=false, CanStart=false |
| 쉼터 스위치 옆 F | 해제 유지, 자동 출발 없음 | unlocked=true, lift=Waiting |
| 아래 발판에서 적재·F | 합류 조건 충족 시 상승 | solo alive=0/arrived=0, Moving→Arrived, y4.05, 다음 대포 허용 |
| 쉼터 출발 발판 F | 다음 운송·2구간 불꽃 시작 | transport2 Moving, fire2 예고13.9초 |
| 다음 발판 이동 중 R | 모든 운송/불꽃/조작 초기화 | Waiting 3개, unlocked=false, fire1/2 대기, 무주인, Simulated=true |
| 시작 대포 앞 F→F | 탑승→발사 | seated=true·좌석(0,1.2,3)→flying=true·(0,9.26,13.29) |
| 첫 받기 위치에 미리 놓은 트램폴린 | 접촉·수직 반동 | LocalBounceCount=4, (0,7.45,25.02), Normal·flying=true |
- 로그: .harness/local/task042-runtime-console.json, transport0 시작/도착·쉼터 해제·transport1 시작/도착·transport2 시작 기록.
- 정상 재진입 후 게임 스모크 Console cursor109064→109073의 error 항목 0. 과거 에디터 버퍼의 오류 숫자와 구별했다.

## 자동 검사
- check-work -Mode PlayMode: scope FAIL10 → 문서/컴파일/테스트 SKIPPED. 위임된 관리 전환8파일(041 4개·Integration 3개·042 setup) + 기존 폰트/MPPM 2개.
- 별도 수동 감사: 지정 HumanCannon 코드/씬·042 문서·위 전환 문서·기존 2파일 외 변경 없음. 자동 FAIL을 PASS로 덮지 않았다.
- 별도 verify-unity -Compile: PASS(up_to_date), 저장 씬 dirty0.
- 최종 verify-fast PASS(hard0/범위 밖 기존 WARN5), diff --check PASS. 이번에 추가된 문서 크기 경고는 없다.
- EditMode 최종 184/184 PASS, failed/skipped/inconclusive 모두0. .harness/local/test-results-editmode.json.
- 새66개: 운송 상태27·발판/불꽃 경계27·저장 씬 배치8·재시작 응답 순서4. 기존118개도 통과했다.
- PlayMode: NO_PROJECT_TESTS(total0), .harness/local/test-results-playmode.json. 실제 플레이 PASS가 아니다.
- 최초 발판 경계 오차 2건은 허용오차 수정 후 해결. 씬 검사8개 Inconclusive는 저장 씬 Additive 검사로 바꿔 해결했다.
- 재시작 메시지 순서 문제는 token 응답 장벽과 연결 시 재전송으로 수정했다. 실제 네트워크 지연/재접속 시험은 미실행이다.

## 검증 도구 제약·정리
- Play 중 테스트 파일 재컴파일로 세션 static/InputSystem 참조가 끊기며 Assert/NullReference가 발생했다. Stop→컴파일 완료→새 Play로 복구했고 정상 스모크에서는 재현되지 않았다.
- CLI simulate_key 및 물리 Keyboard 이벤트가 Editor 포커스에 소비되어 F가 동작하지 않았다. 임시 가상 Keyboard로 실제 InputSystem 경로를 검증했다.
- 포커스 우회는 메모리 InputSettings 복제본에만 적용했다. ProjectSettings/Input 에셋을 저장하지 않았다.
- Play 종료 때 자동 복원 콜백에서 원래 메모리 객체가 먼저 소멸해 ArgumentNullException 1회 발생. 기본 입력 모드 복원 확인 후 임시 디바이스 제거·스크립트 재로드로 콜백을 정리했다. 게임 코드 오류와 구분한다.
- 정리 후 확인: 임시 키보드0, 원래 입력 모드, Missing Script0, dirty0. 재로드 직후 Pipeline 요청 타임아웃은 재연결 후 해소했으며 새 Play→Stop에서 추가 오류0(cursor109082 이후) 확인.

## 미실행
- 실제4인 호스트/참가자 역할 교환, 3명 합류 차단, 외팔/두 다리 손실 접근, 역할 교대, 원격 운반·불꽃 동기화, 불꽃 속도/접촉 밸런스.
- 커밋·push·PR·병합. 다음 작업 PR 대상 integration/main-stage-human-cannon, Squash.
