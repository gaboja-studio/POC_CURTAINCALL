# Handoff

## 지금 상태

- Task-064, fix/waiting-room-revival-guard. PM 직접 로컬 수정, 커밋·push·PR 없음.
- 확인한 결함: 밤 정비 부활은 공연 씬 해제보다 빠르며, 기존 가드는 출발점 확인 이후 공연 종료 여부를 검사하지 않아 옛 씬 피해가 부활자를 다시 죽일 수 있었다.
- 최소 수정: CampaignPerformanceEntry의 기존 Fallen 가드에 IsCurrentPerformance 결합. 카메라·NetworkPlayer RPC·부활 위치·게임 규칙은 변경하지 않음.
- 기존 Vivox Settings.json 변경 보존. Task-062 문서·미완료 공동 검증 기록 보존. 필요한 세 구현/테스트 파일만 현재 Task에 재배정.
- Task 발급용 로컬 integration/waiting-room-revival 및 최초 fix/waiting-room-revival 브랜치는 생성만 되었고 원격·커밋 없음. 실제 작업 브랜치는 meta 참조.

## 다음 할 일

1. 실제 호스트/참가자 공동 플레이 확인(plan.md의 3항목). 자동 검증은 완료.

## PM이 확인할 것

- 실제 다중 클라이언트 검증은 자동 호스트 회귀와 별개다.
- 부활 이전 지연 RPC를 reviveCount로 거부하지 않는 별도 공백은 이번 범위에서 변경하지 않았다. 원격 공동 확인 후 후속 여부 판단.

## Verification

- 구역 검사: PASS (10-20)
- 1 문서/규칙: PASS (10-20)
- 2 컴파일: PASS (10-20)
- 3 테스트: EditMode 815/815, 핵심 PlayMode 4/4+1/1 PASS (10-20). 넓은 전환 회귀는 8/11이며 줄타기 시간 초과 3건은 수정 제외 비교에서도 동일 실패(plan.md 참조).
- 4 실행 로그/자동 회귀: 기존 부활→대기실 로그 확인. 수정 제외 시 대기실 Fallen 재현, 수정 적용 시 외팔·Normal·조작 가능·관전 해제 확인. 상태·위치는 합성.
- 5 공동 테스트: SKIPPED(실제 다중 클라이언트·사람 조작 미실행)

## 공동 테스트 결과

- 참여자: 미실행
- 재현: 첫째날 인간대포 낙하 사망 → 밤 정비 대기실 복귀.
- 예상: 외팔·Normal·조작 가능·관전 해제 유지. 다음 공연의 정상 사망은 유지.
- 실제: 미검증. 자동 테스트의 상태·위치 주입을 실제 2인 검증으로 취급하지 않는다.
