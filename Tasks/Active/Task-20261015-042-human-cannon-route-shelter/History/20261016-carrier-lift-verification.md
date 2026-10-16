# 042 트램폴린 소지 승강 조건 — 2026-10-16

- 사용자 요청: 트램폴린을 가진 상태에서 승강기를 타야 작동하도록 추가한다. 이전 자동 승강 결정 중 장치 없는 출발만 대체한다.
- 모든3대의 호스트 CanStart에 현재 트램폴린 Carrier와 탑승자 일치 조건 추가. 무주인·다른 담당자·장치 없음이면 대기한다.
- 대기 중 놓으면 기존 Boarding.Tick의 자격 상실 경로로 취소, 다시 잡으면 새1.5초가 필요하다. 이동 확정 후 중도 놓기에 의한 취소는 추가하지 않았다.
- 쉼터 합류 집계는 실제 carrier만 제외한다. 빈손 탑승자를 임의로 합류 인원에서 제외하던 예외를 제거했다. 전원 도착/과승 함정/탑승 색상 규칙 유지.
- 씬 배치·네트워크 메시지·공용 설정 변경 없음. 코드2파일과042문서 수정. 독립 읽기 검토에서 확정 결함 없음.

## 검사

- doctor는 같은 작업 최초 FAIL0/WARN2를 재사용. check-work -Mode PlayMode는 기존과 같은 scope FAIL13, 후속 SKIPPED.
- 수동 감사: 위임된 전환 문서8개+기존 공용 변경5개 외 구역 밖 변경 없음. 기존 폰트/MPPM/NewPlayModeScenario hash3개 동일.
- 별도 verify-fast PASS(hard0/기존 WARN5), 컴파일 PASS(completed), EditMode269/269 PASS(failed/skipped0), PlayMode NO_PROJECT_TESTS.
- 단일 LAN 호스트1명으로3대 각각: 빈손 탑승2초 후 Waiting/countdown=false, 잡기 후countdown=true, 0.65초 뒤 놓기→취소, 다시 잡고1.25초까지Waiting→1.5초 이후Moving 확인.
- 위치 배치와 RequestGrab/RequestDrop 직접 호출을 사용한 합성 로직 검증이다. 실제 F 입력·원격 클라이언트·다인 합류 증거로 보고하지 않는다.
- 로그 .harness/local/task042-carrier-lift-runtime-console.json, seq109237~109261.3대 모두 COMPLETE, Console 오류0/경고0. 콜백 해제·재시작 후Stop.
- 실제 확인: 빈손으로2초 대기 / 트램폴린 들고 자동 출발 / 대기 중 놓기 취소. 이후 쉼터 합류·2인 과승과 원격 담당 교환은 plan의 공동 테스트에서 확인한다.
- commit/push/PR 미실행, human-verify 유지.
