# Todo

기준: [최종 승인 규칙·Jira 개발 순서](approved-rules.md). 이전 체크리스트: [History](History/001-before-final-approved-todo.md).

## 확정·등록
- [x] 최종 기획서·현재 구현 대조
- [x] 전 단계/소구간 공통 투표·도착자 제한·기물 체크포인트·그네·발판·재연습·둘째 공연 선택 확정
- [x] 작업 문서·지식 문서에 승인 반영
- [x] CC-199/기존 하위 7개 갱신·CC-218/219/220 추가·개발 순서 정렬

## 구현·검증
- [x] CC-210: 코드 반영. 합성 원격 3명 PlayMode `W6_Rehearsal_VoteAndPersonalCheckpoint` PASS(2026-11-03 저녁). 실제 다인 접속은 사람 확인
- [x] CC-211: 코드 반영. 같은 테스트로 기물 끝3m 체크포인트 저장·1.5초 복귀·새 구간 초기화 PASS. 배치 기준은 checkpoint-placement.md
- [x] CC-212: 중간 발판 재사용. `W6_Rehearsal_CoursesFollowStageDiagrams` PASS(철창 검사는 완료 뒤로 이동). 실제 입력 체감은 사람 확인
- [x] CC-213: 단독/연속 그네·트리거 유지. 같은 테스트로 2-1~2-3 배치·트리거 연결/해제 PASS
- [x] CC-214: 네 줄/짝 트리거. 같은 테스트로 3단계 엇갈린 연결·완료→길안내→집결→첫 공연 PASS. 다인 완주는 사람 확인
- [x] CC-218: 가이드 문구·단장 3초 코드 반영/컴파일 PASS. 시간 전환 체감은 사람 확인
- [x] CC-215: 첫날 둘째 공연 선택·확정(ScheduleBook 1일차 둘째 칸, 집결 게이트, 퀘스트 second-pick). `W6_FirstDay_GuideSecondPerformanceAndNightQuests` PASS
- [x] CC-219: 이수 후 호스트 재연습 StartPractice/EndPractice·연습 종료 버튼·바닥 복귀. `W6_Rehearsal_PracticeAfterMasteryKeepsRecordAndEndsFreely` PASS
- [ ] CC-216: 자동 검사는 위 5건+EditMode 39건 PASS. 실제 다인 입력·철창 우회·그네 체감·둘째 공연 선택 UI 조작은 사람 확인 대기
- [ ] CC-220: 인간대포 상세 기획·리허설은 위 우선 구현 후 진행

## 남은 주의
- 원본 대기실/밤 안내 복구 후 마지막 회귀는 running 결과로 중단됨. 완료 확인이 필요하다.
- 기존 외줄 PlayMode 회귀 및 실제 다인 협동·공중 잡기·철창 전체 우회는 최종 규칙으로 검증한다.
- 이전 성공 결과/완료 체크는 새 요구 완료를 뜻하지 않는다. 미정 수치는 원문 공란으로 유지한다.
