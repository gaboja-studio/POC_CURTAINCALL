# Handoff

## 지금 상태

- 단계: 인계 완료(2026-10-02, assigned). 구현 전. worktree `github-worktrees/POC_CURTAINCALL/TASK-network-player-sync`(Skill 링크·.env 준비됨, Unity 아직 안 엶)
- 방향: 테스트 캡슐 대신 004 실제 플레이어 프리팹에 온라인 적용. 이후 Task가 이 공개 기능을 씀 (`Harness/Project/Decisions/feature-with-network.md`)
- 참고: `Docs/Domains/player-control.md`(004 공개 함수·혼자 기준 주의점), `Docs/Domains/network-session.md`(`NetworkSessionManager`)
- 막힌 것: 없음
- PR: 없음

## 다음 할 일

1. 이 worktree를 Unity로 열기(첫 Library 생성) → "Task-20260930-007 시작해줘".
2. 004처럼 단계를 쪼개 작업자와 합의(제안): ① 플레이어 프리팹 네트워크 생성·출발 위치 ② 내 캐릭터만 입력 + 카메라·게이지·HUD 내 캐릭터 ③ 위치·점프 공유 ④ 자세(기울기) 공유 ⑤ 호스트 판정 통로 + 추락 상태 공유 ⑥ 래그돌 연출. 단계마다 MPPM 여러 명 확인.
3. 공용 파일(플레이어 프리팹·UI·NetworkManager 프리팹) 수정은 `setup.md` 배정 범위 안에서만.

## PM이 확인할 것

- 확인 요청: 없음
- 대기 중인 결정: 래그돌 물리 결과까지 모두에게 같게 맞출지(기본: 상태만 공유, 연출은 각자)
- 공용 파일 요청: 없음

## Verification

- 구역 검사: 미실행
- 1 문서/규칙: 미실행
- 2 컴파일: 미실행
- 3 테스트: 미실행
- 4 실행 로그: 미실행
- 5 공동 테스트: 미요청
