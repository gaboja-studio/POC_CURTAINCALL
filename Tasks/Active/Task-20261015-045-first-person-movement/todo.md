# Todo

## PM 준비

- [x] 테스트 씬·폴더 생성, `setup.md` 작성 (10-15 빈 씬·.meta, CC-131·좁은 파일 배정)
- [x] `plan.md` 공동 테스트 초안 3개 작성
- [x] `plan.md` 공동 테스트 항목 합의 (2026-10-15 PM 정식 배정 요청, 기존 3개 항목 확정)
- [x] 인계 전 기술 점검 완료
- [x] 최신 배정 문서 작업 브랜치 수신 (2026-10-15 #83 Merge commit 42779cc, 통합 71cb485과 tree 일치. 수신 후기는 통합 기록에만 추가)
- [x] 담당자에게 실제 전달 — PM 확인

## 작업자

- [x] "시작해줘"로 작업 시작
- [x] 수정 전 실입력 재현·실패 근거 확보
- [x] 1인칭 기본 이동 최소 수정·3인칭/외줄 유지
  - [x] PlatformControlScheme: 로컬 1인칭일 때만 수평 시선 기준 입력을 코스 축으로 투영
  - [x] 누른 채 시점 전환 시 키를 뗄 때까지 직전 기준 유지(사용자 결정 A)
  - [x] 키가 눌린 채 남는 현상 완화: 커서 잠금 변경·포커스 회복 때 키보드 실제 상태로 재동기화(에디터 확인 PASS)
  - [x] 완화 효과를 실제 키보드로 확인(10-15 사용자 "아주 잘된다", 재발 없음. 근본 원인은 미확정)
- [x] 범위 확대(2026-10-15 PM): 3인칭 고정/비고정
  - [x] CameraViewKind.FreeThirdPerson·CameraViewDirector 마우스 시선 궤도 카메라
  - [x] 기본 이동 시선 기준을 마우스 시선 시점 전체로(UsesMouseLook)
  - [x] FreeThirdPerson.asset, FirstPersonMovement 테스트 씬(1인칭·비고정 구역, 가장자리 벽)
  - [x] PlayMode 비고정 회귀 추가(14/14)
  - [x] 수정 후 사람 실입력 확인(1인칭 W/S/A/D·3인칭)
  - [x] proto_0.0.2 외줄·플랫폼·점프 확인(10-15 AI 플레이, 기존 경로 유지)
- [x] EditMode·PlayMode 회귀 추가, "검사해줘" 통과
  - [x] ControlRulesTests 9개 추가(14/14 PASS)
  - [x] CameraViewsPlayTests 5개 추가, 기존 9개 유지(14/14 PASS)
  - [x] 구역 밖 churn(TMP 폰트·EditorBuildSettings) 사용자 요청으로 되돌림
  - [x] check-work -Mode PlayMode (구역·문서·컴파일 PASS, PlayMode 13/13)
  - [x] Play 후 다시 생긴 EditorBuildSettings churn 처리(사용자 요청으로 되돌림)
  - [x] 범위 확대 후 check-work -Mode PlayMode (구역·문서·컴파일 PASS, PlayMode 14/14, EditMode 전체 55/55)
- [ ] 기능 완성 후 사람 확인 한 번에(FirstPersonMovement 씬 + proto_0.0.2 외줄 + 공동 테스트)
  - [x] FirstPersonMovement 씬 최종 확인(10-15 사용자 "아주 잘된다 이대로 좋아")
  - [x] proto_0.0.2 외줄·공동 테스트 1~2
  - [ ] 공동 테스트 3(호스트 A·참가 B 2명)
- [ ] "제출해줘"로 소속 통합에 Squash PR 제출

## PM 통합

- [ ] PR 병합
- [ ] 공동 테스트 통과
- [ ] 테스트 씬 처리 결정 기록 (`setup.md`)
