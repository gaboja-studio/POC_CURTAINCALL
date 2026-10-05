# Log

중요한 사건만 1줄(30~50단어)로 적는다. 기본 읽기 대상이 아니다. 자세한 내용은 `History/`로 옮긴다.
배운 함정·결정·사실은 여기 묻지 말고 지식 폴더로 보낸다(`record-knowledge`).

## 2026-09-30

- Task 생성.

## 2026-10-05

- 목마·균형·분리 점프·카메라 연출을 3e4365b로 중간 저장·push. 최종 회귀 1~4는 사용자 통과 보고, PC 종료로 로그 유실하여 AI 로그 재검증 없음. 012 톱날·발판 연계 대기, BaseGameSettings 임시값 유지.
- 최신 통합 9794468 병합 충돌 3건을 양쪽 보존으로 해결·stage. Unity에서 설정 에셋 저장 후 기존 목마·카메라 및 통합 신체 손상·톱날 값 전체 일치 확인. Piggyback 씬에 톱날 프리팹 배치·저장, Base 0/0/0 유지.
- Compile up_to_date·Fast OK·NO_PROJECT_TESTS. Scope FAIL(112, 통합 유입 다른 Task 변경 및 .gitkeep.meta 누락), 병합 커밋·push 보류. 목마·수직 톱날·발판 네트워크 연계 Play는 사용자 확인 대기.
- PM 승인으로 병합 유입 구역 판정·점 파일 meta 검사·검증 fingerprint 보완 및 Navy/Red 재질 소수점 원복. Git 모의 회귀 22개 PASS, 실제 Scope OK(396)·Fast OK(WARN 5/hard 0, 기록 갱신 전)·Compile PASS(up_to_date), 현재 Editor 테스트 0개(NO_PROJECT_TESTS). 연계 Play·Base 초기화·커밋·push 미실행.
- 연계 QA 준비: Piggyback 씬 저장 상태·목마/톱날 구성 확인, Compile up_to_date 재통과. 콘솔 기준 cursor 2, 기존 Unity Connect 설정 오류 1건(접속 영향 미확인). 실제 호스트·클라이언트 플레이 결과 대기, Base 임시값·기존 변경 유지.
- 차단 해소 승인 후 doctor의 worktree 간 Active/Done 오탐 수정·Project Pitfalls 기록. 합성 회귀 6개·실제 Task 복사본 123개·구문 검사 통과, Doctor FAIL 0/WARN 1·Scope OK(399)·Fast OK(WARN 5/hard 0, 인계·지식 기록 후)·Compile PASS(up_to_date)·NO_PROJECT_TESTS. 콘솔 cursor 151: 서비스 인증 오류 4건·Pipeline timeout 1건, 접속 영향 미확인. 연계 Play·Base 원복·병합 커밋·push 미실행.
- 사용자가 Piggyback 씬 연계 Play 3개 항목 모두 통과 보고: 목마 분리·수직 톱날·발판 착지, 발판 하강·아래층 통과·양쪽 동기화, 피격·절단·재시작. 사용자 확인으로 기록하고 AI 실행 로그 검증·콘솔 오류 해소와 구분. Base 임시값 원복·임시 로그 정리·최종 검사·병합 커밋·push·PR은 미실행.
- 종료 정리 승인에 따라 Base 균형값 15/5/0.5 원복·에디터 재조회·단일 에셋 저장 및 세 필드 diff 확인. 임시 균형 로그·전용 상태 제거, 균형 주입 유지·잔여 참조 없음. Task 문서 정리 후 Scope OK(401)·Fast OK(WARN 5/hard 0)·Compile PASS(completed)·공백 검사 통과. NO_PROJECT_TESTS, 원복 후 새 Play·인증 오류 해소 미검증. 병합 커밋·push·PR 미실행.
- 저장·제출 요청으로 최신 통합 포함 병합 커밋 66d7070 및 작업 브랜치 push 완료. Scope OK(401)·Fast OK(WARN 5/hard 0)·Compile PASS(up_to_date)·공백 검사 통과. Navy/Red 소수점 변경 2건은 커밋 제외·로컬 보존; PR 등록의 미저장 변경 검사 차단으로 제출 보류. 임의 원복·검사 우회 없음.
- 사용자 승인으로 Navy/Red 두 재질을 세션 scratchpad/material-backup-20261005/에 백업·cmp 검증 후 소수점 변경만 원복. 최신 통합 이미 반영, gh Ready. check-work 구역·문서·컴파일 PASS, CLI 테스트는 열린 Editor 중복 실행 차단으로 실패·XML 없음. Editor list_tests(all) 0개 확인; 프로토타입 결정에 따라 NO_PROJECT_TESTS로 구분하고 실패 이력 보존, 검사 스크립트 수정·AI Play 실행 없음.
