# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: done — PR 병합·공동 테스트 기록 확인 후 2026-10-05 PM 요청으로 Done 보관. dev 반영은 통합 종료 때 별도로 진행.
- 동작하는 것: `Tightrope/Rider/RopeLaneJumpRules.cs`(씬에 하나 두면 내 캐릭터에 연결) — ① 옆줄 점프 판정(이동 가능 구간·옆 줄 있음·막히지 않음, 아니면 입력 무시) ② 착지 자리 동료와 겹치면 뒤쪽 착지(줄지어 있으면 맨 뒤), 설 자리 없으면 착지 순간 추락 ③ 손잡기(착지 지점 같은 줄 동료 앞뒤 1.0m 안이면 충격·흔들림 50% 감소, 뛰는 순간 판정)
- 공용 파일 수정: `PlayerMover`(`GetLaneLandingPosition`, `LaneLandingShift`, 보정 시 앞뒤도 목표에서 멈춤), `TightropeSettings` 손잡기 묶음(코드·에셋)
- 테스트 씬 `TightropeRider.unity` = 005 `Tightrope.unity` 복사 + `RopeLaneJumpRules` 오브젝트
- 막힌 것: 없음 / PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/21

## 다음 할 일

- 이 Task의 구현·제출·병합 대기는 종료했다. 아래 목록은 이전 인계 기록이며, 통합 전체의 dev 반영·테스트 씬 처리 결정은 PM이 통합 종료 때 진행한다.

1. PM의 PR 검토·통합 병합 및 공동 테스트. 최신 통합 반영·작업 브랜치 push 완료
2. 4번 완료: `TestLaneLanding.cs`·`.meta` 삭제, `NetworkPlayerSync.unity`는 해당 컴포넌트만 제거(빈 오브젝트 유지), 씬 저장됨

## PM이 확인할 것

- 실게임용: `RopeLaneJumpRules`를 005 코스 프리팹에 붙이기(PM 요청 필요). 지금은 씬마다 직접 둔다
- 006 인계: 겹치면 **목마 합체 우선**(`tightrope-rules.md` 2번). `RopeLaneJumpRules`에서 겹치는 동료를 찾은 직후, 뒤쪽 보정 전에 "합체 가능하면 합체"를 넣는다. 합체가 안 될 때(뛰는 사람이 목마 중, 상대 4층 등)의 뒤쪽 착지·자리 없으면 추락은 그대로 유지. `BlockLaneJumpOntoPlayer`는 이 컴포넌트가 이미 끈다
- 순서 배정: `TightropeSettings`(012와 공유), `PlayerMover.cs`(020과 공유) — 먼저 병합되는 쪽 다음에 다른 쪽이 최신 통합을 받는다
- 이 Task 다음: 006 목마 → 010 목마 예외(같은 갈래 B)

## Verification

- 2 컴파일: PASS (10-04)
- AI Play(LAN 호스트 1명 + PlayerMover만 붙인 가짜 동료): 바깥 줄·막힌 줄 무시, 뒤쪽 착지 9.35m·8.70m 정확, 줄 시작 0.3m 추락, 손잡기 충격 17.5·×1.375 vs 혼자 35·×1.75. 이동 불가 구간은 코스가 0~100m 전부 허용이라 미확인
- 5 작업자 플레이(2026-10-04 @MoHoDu): 안내한 항목에 대해 "모두 잘 되는것 같아"라고 확인. 접속 인원·방식은 별도 확인하지 않음
- 0 구역·1 문서/규칙: PASS. 3 자동 테스트: NO_PROJECT_TESTS(PlayMode, 열린 에디터 list_tests Count=0). check-work의 별도 테스트 에디터 실행은 프로젝트가 열려 있어 거부됨
- 4번: 씬 저장·dirty 0, 누락 스크립트 0(에디터 조회). Assets에 삭제한 스크립트 GUID·타입 참조 없음. 정리 후 Play 로그 검증은 SKIPPED(미실행)
- 참고: 균형 입력 없이 옆줄 착지하면 약 2초 뒤 균형 붕괴 추락(004 기존 동작)
