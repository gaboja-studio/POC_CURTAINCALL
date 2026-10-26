# 공연 종료 검증 · 2026-10-20

## 실행
- Pipeline 연결 Unity6000.3.25f1에서 컴파일 PASS. UI PlayMode14/14 PASS(성공·실패 표시 순서, 입력 차단/해제, 다음 공간 후 결과 재표시 방지 포함). 배너 배치/검은 덮개 수정 후 다시 통과했다.
- 실제 호스트 API를 사용하는 합성 Play 실행: 외줄·인간대포 각각 ServerKill→사망 알림→실패 결과→검은 화면→WaitingRoomBase. 0.3초 시점 사망 알림/입력 잠금, 2초 결과, 5.6초 검은 화면, 9초 대기실/입력 해제 확인. 프레임 진행도 확인했다.
- 인간대포 성공 합성 실행: AddAudienceReaction40→FinishPerformance(true), 결과 팀 호응40/수입40/생존 호응40 확인. 같은 표시 순서로 대기실로 전환되고 사망 알림은 없었다.
- 자동 테스트는 실제 보상 모델 스냅샷과 로컬 UI 수명을 검증한다. 네트워크 지연/클라이언트 동기화 테스트가 아니다.
- 캡처1920×1080에서 한글·패널·알림 배치를 확인했다. 사망 배너가 결과 제목과 겹쳐 상단으로 이동했다. 기존 개발 IMGUI는 일반 결과 화면에 남는다.
- 로컬 증거: `.harness/local/ending-tightrope.txt`, `ending-cannon.txt`, `ending-cannon-success.txt` 및 단계별 PNG. 실행용 Probe 코드와 상태 주입은 씬/빌드에 포함하지 않았다.
- 구현 중 Installer의 NewSceneMode 타입 컴파일 오류를 수정했고 최종 컴파일 통과했다. 과거 콘솔 로그와 최종 실행 오류를 구분한다.

## 제약
- 실제 키보드 완주/추락/불 접촉, 동료만 사망·관전 유지, 2~4클라이언트, 재접속·느린 로딩·45초 타임아웃, 다른 해상도/빌드는 미실행.
- check-work의 기본 integration 비교는 수신 dev 이력 때문에 구역 FAIL. 현재 변경 비교도 이전 호응 작업과 Task044/062 구역 중첩이 남는다. 검사 우회나 타 Task 변경은 하지 않았다. 개별 컴파일/테스트 결과를 종합 검사 PASS로 표시하지 않는다.
- 변경 목록/사용자 확인 절차: [적용 가이드](../../../Docs/Guides/Ui-scene-integration/performance-ending.md).

## 최종 상태
- 문서/규칙 Fast OK(기존 경고9), 최종 콘솔 cursor1336→1427 신규 오류0. Start 씬·Play 정지·runInBackground=false 복원.
- 검은 화면에 개발 IMGUI 일부 글자가 남는 화면 제약은 후속 개발 패널 정리 대상으로 기록한다.
- 기본 scope는 검증 당시 임시 runInBackground 변경 포함 FAIL290, 복원 후 HEAD 비교 FAIL14(기존 호응 코드11/정책3 중첩).
