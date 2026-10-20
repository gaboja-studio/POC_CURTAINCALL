# Handoff

## 지금 상태

- 실제 WaitingRoom 활성·그레이박스/더미 선택 UI 비활성. 실제 로비 스폰 4개, 무대 통로 퇴장 경계, R10 집결, 기존 야간 셔터·스케줄표·카메라 연결 저장 완료.
- 첫 개인 퇴장은 팀 타이머만 시작하고 대기실 씬·잔류자를 유지한다. 더미 fixture는 별도 탐색 씬 정책을 명시했다.
- 개인 퇴장 이력과 외부 위치로 소유자 재진입을 보정한다. 다음 쉬는 시간에 초기화한다. 원격 소유자 공동 검증은 남았다.
- 사용자 승인으로 Task-064 6bd83ca와 Task-065 e22c088을 작업 브랜치에 push. 기존 Vivox Settings.json은 제외·로컬 보존.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/191 (dev 대상, 미병합)

## 다음 할 일

1. 공동 테스트 최대 3항목: 부활·관전 해제 / 한 명 퇴장·타이머·잔류·집결 / 밤 잠금·F 스케줄·전원 준비.

## Verification

- 구역: PASS(13파일), verify-scope.ps1 -Base integration/waiting-room-physical-flow. 최초 11건 실패는 Task-064 승인 커밋 분리로 해소했다.
- 문서/규칙: PASS, 기존 길이 WARN 8/hard 0. 원격 integration 없음으로 check-work 대신 개별 계단식 검증.
- 컴파일: PASS(completed), 열린 씬 1/dirty 0. 최종 참조 검사: missing scripts 0, 활성 스폰 묶음 1, 4개 스폰 캡슐 충돌 없음, 셔터·스케줄표 참조 유효.
- PlayMode: SpaceLink 6/6, 부활 가드 4/4, 실제 인간대포 사망→대기실 외팔 부활·관전 해제 1/1 통과. 전체 EditMode 815/815 통과.
- 실제 씬 회귀: 가상 키보드 F→스케줄표, 첫 퇴장·씬 유지·개인 재진입 차단·전원 집결, 야간 셔터 충돌체·스케줄 확정/준비·다음 아침 첫 퇴장까지 통과.
- 기존 별개 줄타기 시간 초과 3건이 있는 PerformanceLink 전체 fixture는 이번에 재실행하지 않았다. 해당 부분까지 PASS라는 뜻이 아니다.
- 중간 실패: 첫 밤 테스트는 1일차 밤 2·3일차 전부 채워야 하는데 2일차만 채운 fixture 오류. 수정 후 통과. F 입력의 delta bitfield 오류는 KeyboardState 전체 이벤트로 수정했다.
- 실제 통로: 기존 장애물을 보존하고 캡슐 공간 격자 탐색으로 로비→R10 연속 경로 확인. 실제 조작 통행과는 구분한다.
- 합성 위치/API 테스트는 사람 입력·NGO 2인 공동 플레이 PASS가 아니다. 늦은 입장자의 과거 퇴장 이력 복원은 미검증.

## 공동 테스트 결과
- 원격 2프로세스 미실행: MPPM ScenarioConfig 에셋·로드된 설정 모두 0개. 공식 Scenario 구성 추가는 구역 밖이라 수행하지 않았고 내부 VirtualProjectsApi 강제 실행도 제외했다.
- 재개 전 구역·문서·컴파일 PASS(up_to_date), EditMode/dirty 0 유지. 원격 소유자 보정 동기화와 위 3개 사람 확인 항목은 미검증.
- 전문가 시설은 기존 별도 배치를 보존했다. 새 방·시설 서비스 재설계 및 서비스 전체 재검증은 이번 범위 밖이다.
