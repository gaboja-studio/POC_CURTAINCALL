# Handoff

## 지금 상태
- 단계: working — 2026-10-15 start-work로 최신 원격·배정 문서·실제 전달 gate 통과, 담당 @RagSpirin.
- 작성한 것: 대포/WASD·트램폴린 정면 운반/회전·점프/놓기·지상 점프 반동·무주인/인계·호스트 요청 검증. 추가: 쿠션 윗면 정상 착지→호스트 확정/비행 해제/지상 조작 복귀, 일반 바닥 사망 유지, 안전 착지 횟수 HUD/R 초기화.
- 사용자 확인 범위: 기존 F 1~4·거리/WASD 정상. 10-15 트램폴린/지상 점프 1~9는 혼자 플레이 확인으로 명시, 추가 쿠션 1~5(착지/조작/횟수/바닥 사망/R) 모두 정상 보고. 4인 공동 검증은 미확인.
- 반복 테스트: R → 방 유지·전원 복구·점유/비행/키 정리·장치 복원. 오류 점검 수정: 빈 대포도 revision 증가/Reset 전송, 이전 탑승 거부·늦은 발사/취소 위치를 출발점 복구. 발사 revision+반동 segment 검증, 운반자 정상 점프 제외.
- 씬: HumanCannon.unity 대포1·트램폴린1(4×4m/높이0.5m/z25.03)·주황 쿠션1(4×4m/높이0.2m, x6/z25.03)·발판/눈금/출발4/LAN/HUD 저장. root10, dirty=false. 잡기2m·대포 수평8/상승12·WASD3m/s, 반동 전진0/상승12, 공용 점프0.8m 유지.
- 환경/해결: Unity/Pipeline 7801 ready(10-15), 시작 doctor 미연결 복구. CLI beta.8·gh 설치/사용자 로그인 완료(@RagSpirin). 사용자 승인으로 입력 version/priority·main_stage URP 자동 추가 2개 원복, 두 파일 Git 차이 0. 검사 차단 없음.
- PR: [#71 배정 문서](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/71)·[#72 전달 확인 문서](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/72) Merge commit 수신 완료. 기능 작업 PR 미제출.
## 다음 할 일
1. 각 기능 오류 점검 완료: R 초기화 누락1건 수정, 구역/문서/컴파일·EditMode64 PASS, 씬 참조/누락 검사 정상, 현재 Console 오류/경고0. 검사 범위 내 잔여 오류 미발견. 10-15 사용자 제출 승인, commit/push/PR 준비 중.
2. 통합 후 멀티: 역할 교환·이동/점프 받기·다인/무주인 반동·사망/접속해제·공중 인계·쿠션 착지. plan 3묶음 보존, 041~044 착수는 별도 배정 후.
## PM이 확인할 것
- 확인 요청: 최종 씬 처리·지상 2명·중계 대포·호응/보상·2~3인 후속 미정. 전달 체크의 5개 작업 브랜치 sync 완료, 후속 구현 개방은 아님.
- 교훈 제안(record-knowledge): TMP 라벨 생성 시 공용 LiberationSans 폰트가 자동 재직렬화됨. 전용 사본으로 연결 후 사용자 승인으로 공용 원본 원복. Systems/Resources·외줄·main_stage.unity 변경 없음.
## Verification
- 구역 검사: PASS (10-15)
- 1 문서/규칙: PASS (10-15)
- 2 컴파일: PASS (10-15)
- 3 테스트: NO_PROJECT_TESTS (PlayMode)
- 추가 로직 검증: EditMode 64/64 PASS(10-15). 기존61+R 빈/점유/발사 후 이전 요청·상태 거부3. .harness/local/test-results-editmode.json. 씬 missingScripts0·직렬화 참조 정상·dirty0. 실제 조작·멀티 통과 근거는 아님.
- 4 실행 로그: SKIPPED(새 Play 미실행). 현재 Console 오류/경고0, 과거 로그8개는 수정 전 컴파일/7777 충돌(현재 포트 미점유). 사용자 기존1~9·쿠션1~5 정상 보고. 실제 4인은 전체 통합 후.
- 5 공동 테스트: SKIPPED(사용자 지시로 전체 기능 통합 후 진행). plan 3묶음은 계획이며 통과 근거 아님.
## 공동 테스트 결과
- 일시: 미실행.
- 참여자: 미실행 (예정: 호스트1·참가자3, 지상 역할 교환).
- 재현: 미실행 (plan 3묶음).
- 예상: 역할별 착지·입력·각1회 반동·네 화면 일치.
- 실제: 미실행.
