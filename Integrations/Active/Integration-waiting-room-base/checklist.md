# Checklist

## 준비·배정

- [x] 5개 Task 문서·빈 테스트 공간 검사(verify-fast OK, hard 0) 및 준비 커밋 push — a864410
- [x] 동일 준비 SHA에서 5개 작업 브랜치 생성·push, 4열 SHA 표 등록 — a864410 기준
- [x] ⑥·⑦ 위치 PM 확인 — 백스테이지, ⑥은 테스트 대체 통로만 (2026-10-15)
- [x] 1차 테스트 씬 = waiting_room.unity 복사본 WaitingRoomBase.unity (2026-10-15)
- [ ] dev PR #94(60초 소집 경보 결정) 병합 후 최신 부모 수신 — 047 개방 전
- [ ] Task-046만 기술 배정(`assign-task.ps1 -UsePreparedBranch`), 배정 문서 Merge commit sync 수신, 실제 전달 PM 확인

## 순차 병합·다음 단계 개방

1. 046 공간·공연 왕복 → 047 탐색 제한 시간 → 048 상점·소품실 → 049 밤 정비·스케줄표 → 050 전문가 서비스.
2. 각 작업 PR은 이 통합으로 Squash. 앞 단계 integrated·실제 4인 공동 검증 근거·소유 반환 전 후속은 scaffolded 유지.
3. 다음 한 Task만 Active Task/Allowed/공용 소유 부여, 실제 Jira 카드 URL 연결 후 배정 문서를 준비 브랜치로 Merge commit sync.
4. 충돌 시 담당자와 해결하며 씬·프리팹 병합을 AI에 맡기지 않는다.

## 공동 테스트 항목

각 Task plan의 최대 3개를 실제 실행한다. 준비 문서/빈 씬 검사는 기능 통과가 아니다.

| Task | 항목 | 결과 |
|---|---|---|
| Task-046 | 접속·동선, 전원 집결 후 공연 진입, 복귀·외팔 부활·반복 | 미실행 |
| Task-047 | 조기 시작, 늦은 인원 정리, 표시·경보·재진입 불가 | 미실행 |
| Task-048 | 구매·수령, 운반 제한, 보관 유지 | 미실행 |
| Task-049 | 밤 진입, 스케줄 작성·수정, 다음 날 진행 | 미실행 |
| Task-050 | 구매·입장, 의료 복구, 부재중 | 미실행 |

## 부모 통합 반영 전

- [ ] 공동 테스트 실제 통과·근거 기록 (실패/미연결은 BLOCKED 또는 후속 Task)
- [ ] Task별 테스트 씬 처리 및 소유권 이관 이력 기록
- [ ] WaitingRoomBase.unity → 원본 waiting_room.unity 반영 방법·시점 PM 결정(Task-024 통합 이후, 씬 병합은 담당자와 함께·AI 단독 금지)
- [ ] integration/waiting-room-base → integration/waiting-room PR, Merge commit (dev 직행 금지)

Base가 dev가 아니므로 기존 close-integration/Done 이동/브랜치 삭제 절차는 실행하지 않는다. 빈 준비 통합의 부모 반영도 하지 않는다.
