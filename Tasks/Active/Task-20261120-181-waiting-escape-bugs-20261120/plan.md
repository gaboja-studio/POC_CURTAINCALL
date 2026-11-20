# Task-20261120-181

- **Title:** 대기실·탈출로 버그 묶음 수정 (2026-11-20)
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/waiting-escape-bugs-20261120
- **Base:** dev

## Context
2026-11-20 사용자 요청 버그 8건 + 대기실 간판 정리(사용자가 씬에서 직접 한 간판 제거·추가 변경)를 한 PR로 제출한다.

## 계획
1. 대기실 추격자 포획 사망(Retired) 때 소지 기물 유지 — `PropCarry`가 탈출로 리타이어만 기물 삭제.
2. 장소 전환 중 좌하단 "목마…" 테스트 텍스트 제거 — `PiggybackSystem.OnGUI` 디버그 출력 삭제.
3. 탈출문 밖 새벽 하늘 — escape_route 씬 `47_GrandSunlitExit/새벽 하늘 배경판` + `DawnBackdrop.shader`·`escape-dawn-backdrop.mat`(어두운 원경 벽 앞, 깊이 기록·안개 미적용).
4. 성공 런 후 로비 복귀 때 레버·출연 명단 초기화 — `LobbyRoom`이 몸 복구와 함께 `ReturnToLobby()` 1회.
5. 탈출 성공 달리기에서 절단기 운반 자세 제거 — 도착 순간 운반 해제 + 운반 자세는 Normal 상태만.
6. 런 포기 = 실패 엔딩 경로 — `RunEndReason.Abandoned`를 `IsFailureEnd`에 포함.
7. 던져진 시체가 사라진 뒤 남는 보이지 않는 벽 — Fallen/Retired/EscapeCaptured 몸통 `PlayerMover.OutOfPlay`로 통과.
8. 결승 추격자 끼임 — 추격자 이동 장애물 판정에서 다른 추격자·잘린 팔다리·래그돌 제외, 동료 회피 옆걸음.

## 미정·주의
- 런 포기 결과 화면 문구는 기한 초과와 같은 "탈출 실패"를 그대로 쓴다. 포기 전용 문구는 필요 시 별도 결정.
- 7번은 모든 장소의 사망·리타이어 몸통에 통과 규칙이 적용된다(외줄 Fallen 규칙 확장).
