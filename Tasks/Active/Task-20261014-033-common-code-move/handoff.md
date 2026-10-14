# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 구현 끝(Damage 이전), 플레이 확인 일부 완료
- 동작하는 것: 공용 4개(`BodyDamageEffects`·`BodyPenaltyPolicy`·`LimbDismemberment`·`ModelLimbs`)를 `.meta`째 `Systems/Damage/`로 이전(GUID·namespace 유지). 외줄 전용 2개는 남김. 공통 코드의 외줄 정책 직접 생성을 없애고 `TightropeBodyPenalty`가 시작 전 `BodyDamageEffects.DefaultPolicyFactory`에 등록(결과 동일). 지도 3개 갱신.
- 막힌 것: 없음
- PR: 없음

## 결과 명부 경계안 (031로 넘김, 2026-10-14 PM)

- 공통: 참가자 명부·불변 결과 스냅샷·호스트 공유/검증·이력 10개. 외줄 전용: `TightropeEndReason`, 참가자 결과 판정(`Arrived` 기준), 남은 시간·묘기 경과 의미, 메시지 이름.
- 분리하면 `Rope/TightropeRun`·`Telemetry/`·`UI/TightropeDemoUI`도 고쳐야 함. `TightropeResults.unity`는 카메라·조명만 있는 빈 씬.

## 다음 할 일

1. 확인 씬은 실제 흐름이 다 있는 `Assets/Scenes/Prototypes/proto_0.0.2.unity`(2026-10-14 사용자). 남은 확인(사용자 플레이): 느려짐·흔들림·기어가기 패널티, 결과 화면. 기어가기가 안 나오면 `TightropeDamage.unity` 숫자키로 보충.
2. "제출해줘" → `integration/common-game-systems`.
3. dev 반영 후 공간 담당에게 Damage 경로 변경 알림.

## PM이 확인할 것

- 확인 요청: 플레이 중 Unity가 구역 밖 2개를 바꿈(`Pretendard-Medium SDF.asset` 폰트 자동 변경, `CampaignFlow.unity` 조명에 URP 데이터 자동 추가). 커밋에서 빼고 되돌릴지 PM 결정. 031 Task에 위 결과 명부 경계안 반영(033은 다른 Task 문서를 수정하지 않음). 033 Goal의 "공연 결과 명부" 부분은 이 결정으로 범위에서 빠짐.
- 대기 중인 결정: 없음
- 공용 파일 요청: 없음

## Verification

- 구역 검사: PASS (10-14)
- 1 문서/규칙: PASS (10-14)
- 2 컴파일: PASS (10-14)
- 3 테스트: NO_PROJECT_TESTS (EditMode)
- 4 실행 로그: PASS (10-14) 사용자: `proto_0.0.2.unity` 톱날 절단·패널티·결과 정상. 에디터 검사: Player·BlockDoll 프리팹, proto·TightropeDamage 씬 Missing 스크립트 0, Console 오류 0(경고는 _ThirdParty 메시만)
- 5 공동 테스트: 미요청
