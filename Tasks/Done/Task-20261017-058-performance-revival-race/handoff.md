# Handoff

## 지금 상태
- 단계: PR #155 병합(8ac410a), 공동 테스트 PASS(10-18). integrated, 통합 마감 대상.
- 원인: 대포 씬 로드 직후 호스트 낙하선 판정(HumanCannonRouteSystem)이 원격 P2의 출발점 이동 도착 전 옛 위치로 사망 처리.
- 수정(CampaignPerformanceEntry): 호스트는 출발점 확인 전 사망을 거절, 사망 상태 진입자는 부활 뒤 재배치.
- 막힌 것: 없음. 중간 공간에서 몸이 계속 떨어지는 현상은 범위 밖(통합 result 기록).
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/155

## 다음 할 일
1. 통합 마감·dev 반영(PM).

## PM이 확인할 것
- 확인 요청: 없음.
- 대기 중인 결정: 없음. 게임 규칙·순서·카메라·UI 불변.
- 공용 파일 요청: 없음. TightropeCourse·TightropeRun·RopeLaneJumpRules·PlayerMover·NetworkPlayer는 배정했으나 수정하지 않았다.
- 교훈: Harness/Project/Pitfalls/remote-player-judged-before-placement.md 기록.

## Verification
- 구역 검사: PASS (10-17)
- 1 문서/규칙: PASS (10-17)
- 2 컴파일: PASS (10-17)
- 3 테스트: PASS (PlayMode, 75/75, 10-17)
- 4 실행 로그: 10-18 실제 2인 플레이로 확인. 신규 재현 3개는 1명 LAN 합성(수정 전 실패·수정 후 3회 통과).
- 5 공동 테스트: PASS (10-18, 실제 2인, PM 보고)

## 공동 테스트 결과
- 일시: 10-17 수정 전 FAIL → 10-18 수정 후 PASS
- 참여자: 실제 2인(호스트 PM)
- 재현: 줄타기 전멸(P2 양다리 절단 후 불 사망) → 외팔 부활 → 대기실 → 백스테이지 즉시 집결 → 인간 대포
- 예상: 두 명 모두 생존·출발점
- 실제: 3항목(원래 경로·반대 사망 순서·단독 실행) 통과(PM 보고).
