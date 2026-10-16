# Handoff

## 지금 상태

- integrated / integrate-work — #121/70e242a 병합·044 HEAD9a0d078 수신, 구역·씬 반환 완료. 기존 제출 기록: [PR #121](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/121), 구현52b3367 push 완료. [제출 검증/위임 근거](History/20261016-submit-verification.md). 최신 사용자 실입력·실제4인 확인은 미기록 유지.
- 공용 실패 생존1명 기준·쿠션·독립 최종4대·모든 대포 잠금 해제·원본 오른쪽10구간 연습(F8/R/F9) 구현.
- 최신 범위 지시 반영: 원본 전체/모든 연습 기본1인칭·운반자유3인칭/놓기1인칭, 모든 대포 탑승3인칭/발사즉시1인칭. 별도 범위가 없으면 기존 연속 코스 전체가 기본. [검증](History/20261016-course-wide-carry-camera.md).
- 최종 가로/수직 톱·최초 진입 공유 시각·후발 발사 유지·접촉당1부위, FINISH 실제 도착→공용 정산/종료 연결. Waiting1인 연습은 별도 연습 판정.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/121 — 대상 integration/main-stage-human-cannon(Squash). 044 전환·병합 완료, 실제4인·사용자 체감 미기록, 실제 공연장/공용 공간 라우팅 자산은 범위 밖.

## 다음 할 일

1. HumanCannon 원본 코스 시작1인칭·잡기3인칭/정지 둘러보기/놓기1인칭 확인. F8→2/7 운반·4~6 승강기에서도 동일 동작 확인.
2. F8→10 최종 대포에서 F 탑승→놓고 새 F 발사→WASD 회피→FINISH 착지, R 재시도·외팔 프리셋 확인.
3. 043 #121 통합 확인·044 이관 완료. 위임 전환8개 자동 FAIL·승인 근거는 PR에 보존한다. 원본 단독 경로·직접 플레이 결과 별도 기록.

## PM이 확인할 것

- 사용자 전달 PM 승인으로 공용 실패 기준1명, 모든 대포 독립. run-rules/finale-result의 기존2명/전역수정금지 문구 갱신 제안.
- 지상2명/중계/호응·보상 수치/2~3인은 후속. 톱 시간·크기는 임시 노출값이며 난도 확정 아님. 실제4인 동기화/완주는 plan의 공동 테스트로 별도 기록.

## Verification

- 환경: doctor FAIL0/WARN3(최초1회). 최종 Stop·HumanCannon 씬 저장 확인.
- 구역 검사: FAIL8 — 위임 전환 문서 통합3/042문서4/043setup1. PM 위임·전체 변경 수동 감사로 그 외 구역밖0 확인. check-work 후속1~3은 SKIPPED, 아래 별도 결과.
- 1 문서/규칙: 별도 PASS — verify-fast, 기존 WARN6/hard0.
- 2 컴파일: 별도 PASS — 전체 운반시점 보완 후 Unity completed/failed:false, 제출 전 재확인.
- 3 테스트: 별도 PASS — 최신 전체 HumanCannon PlayMode23/23, 이후 제품 변경 없음. 이전 전체 EditMode363/363·공용 생존6/6 근거 유지.
- 4 실행: PASS(자동 화면/상태 확인) — F8 없이 원본 호스트에서 Grab3인칭/초록손잡이/몸 표시→Drop눈높이1인칭 확인. 신규 Console오류0(cursor4536→4542), 현재 오류0/경고0. 이번 씬 자산 수정 없음, 최종 Stop·dirty0.
- 5 공동 테스트: 미실행 — 실제4인 동기화·시점 조작감·외팔 단독 완주/회피 난도 확인 필요. LAN호스트1명 공용 정산·Night 전환 자동 검증 완료.
