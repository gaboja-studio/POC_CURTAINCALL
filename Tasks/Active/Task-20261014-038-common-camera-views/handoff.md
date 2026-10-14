# Handoff

## 지금 상태

- 단계: PR 제출 (`submitted`, 2026-10-15). 통합 최신(037·030) merge 후 재검사 통과
- 동작하는 것: 시점 preset·요청 판정(높은 priority → 동률 최근 등록)·즉시/시간 전환, 1인칭 pose·마우스·커서, 시점 구역, 로컬 모델 render 중 숨김, 외줄 구도·가림 예외 MainStage 이전(GUID 유지), 리타이어도 관전(merge로 받은 PM 결정), CameraViews 테스트 씬
- 공용 파일: LocalPlayerCamera.cs(director에 로컬 전달), PlayerFollowCamera.prefab(기존 유지 + director·pose·visibility). Unity 저장 확인
- 환경: CLI beta.6 status 검색은 인스턴스 0이나 직접 명령·check-work 연결 정상, caller/skill 인자 미지원
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/66 (→ integration/common-game-systems)

## 다음 할 일

1. PM: PR 리뷰·병합, 리타이어 관전 포함 공동 테스트
2. 037 시선 기준 이동·입력 차단(`ViewForward`·`SetInputBlocked`) 연결은 후속 통합. 이번 완료는 이동 조작 완료가 아님

## PM이 확인할 것

- 후속: TightropeSettings 카메라 칸 정리, Confluence 19398663 갱신
- 지식 후보(Harness 밖이라 미작성): PlayMode 테스트 뒤 씬 복원으로 내용 변화 없이 dirty → 다음 실행 때 "Scene(s) Have Been Modified" 대화상자가 Pipeline 명령을 timeout시킴. 실행 직전 dirty 확인

## Verification

- 시작 doctor: WARN (FAIL 0, WARN 3: CLI 버전 차이·초기 에디터 미발견·skill-links 없음)
- 구역 검사: PASS (10-15)
- 1 문서/규칙: PASS (10-15)
- 2 컴파일: PASS (10-15)
- 3 테스트: PASS (PlayMode, 8/8, 10-15)
  - merge 후 EditMode 16/16 PASS(카메라 11 + 통합 쪽). PlayMode 8개(리타이어 관전 추가)는 실제 씬 + LAN 호스트, 이동은 순간이동·요청/사망은 공개 API(합성, 실제 키·마우스 아님). 첫 실행에서 구역 preset 미연결 발견 → 연결·저장 후 통과
- 4 실행 로그: PASS(자동 근거만, 10-15) — 리그·proto·CameraViews Missing 0, 두 씬 PlayMode 중 Console 오류 0. 수동 플레이 미실행
- 5 공동 테스트: PASS (10-15, 사용자 확인 3/3: 외줄 구도·관전·가림 / 구역 1·3인칭·상대 화면 모델 / 에셋 값 즉시 반영). merge·리타이어 반영 전 결과
  - 관찰: 당시 CameraViews 조작이 줄타기 규칙(Player 기본값). merge된 037에서 기본 이동 규칙이 플랫폼으로 바뀜(재확인은 PM 공동 테스트)
