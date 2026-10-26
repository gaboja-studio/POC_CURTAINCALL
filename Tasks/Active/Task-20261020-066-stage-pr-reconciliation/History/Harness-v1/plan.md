# Plan

## Context
2026-10-20 원본 저장소의 dev 기준 fix/stage-pr-conflicts에서 #192 리소스·양쪽 코드 해결본 적용. HumanCannon.unity는 사용자 예외 결정 후 Unity Editor API로 재적용·저장했다. 외줄 EditMode 226/226, 인간대포 479/479 통과. 전체 EditMode는 857/858이며 실패 상세 재수집 중이다.

## Approach
1. 기존 private/에디터 변경을 보존하고 연결 Task에서 작업한다. 추가 worktree나 별도 Unity 환경 없음.
2. #192 코드 충돌2개·dev 추가 충돌 해결, 독립 tree/diff와 리소스 결합본 보존.
3. #193 코드 충돌9개 해결. HumanCannon 씬은 원본 PR/base/현 dev의 차이를 Editor에서 재적용한다. 씬 YAML 직접 편집·테스트 기준 완화 없음.
4. 최신 dev 결합본을 구역→문서→컴파일→EditMode→PlayMode→실제 공동 테스트 순으로 검증한다. 선행 실패 후 후속은 SKIPPED.
5. 검증과 기존 변경 보존 조건 충족 후 dev 제출·통합한다. 필수 리뷰/CI 및 clean 가드를 우회하지 않는다.

## Risks
- private 재질219개는 렌더링 변경 포함. private PR #1은 정확한 로컬 백업이 아니므로 임의 폐기·포인터 교체 금지.
- 기존 public 재질2개는 작업 산출물과 섞지 않는다. save-work의 git add -A 및 new-pr의 dirty 차단 때문에 현재 그대로 제출할 수 없다.
- 외줄 씬의 기존 #192 참조 override 6개 미적용. 다른 씬 소유권은 유지하므로 현재 HumanCannon 예외로 외줄 씬을 수정하지 않는다.
- dev는 승인 리뷰1건 및 verify-third-party 필수. 자동 검사만으로 사람의 승인을 대체하지 않는다.

## Auto Verification
- git diff --check, 충돌 마커, 양쪽 요구사항 보존, GUID/참조 및 기존 222파일 SHA256 검사.
- 외줄 표현·소각/복원·2/4인 관문·불연속 ID/명부 변경/3인 거부 회귀.
- CannonSeatTests, PoleSwingVisualTests, RouteCourseTests, FinalCannonPlayTests 및 공용 PlayerMover 회귀.
- 이전 PR 결과를 이번 결합 검증 PASS로 재사용하지 않는다.

## 공동 테스트 항목
1. 실제 호스트/참가자 2인 및 4인: 외줄 관문/로프/톱 판정, 인간대포 일반 점프 방향 전환·봉 도약 수평 관성·대포 발사·원격 동기화. 예상은 기존 승인 규칙 유지, 실제 인원·역할·입력/결과·로그를 handoff에 기록.
2. 두 콘텐츠 공연 종료→부활→WaitingRoomBase→물리 출구: 탐색 타이머·재진입 차단, 옛 씬 재사망 없음. 호스트/참가자별 실제 결과 기록.
3. 두 콘텐츠 LAN 음성: 생존자 거리 감쇠·사망자 분리·부활/씬 전환 후 복구. 호스트/참가자의 실제 청취 결과 기록. 합성 검증을 사람 테스트로 표기하지 않음.
