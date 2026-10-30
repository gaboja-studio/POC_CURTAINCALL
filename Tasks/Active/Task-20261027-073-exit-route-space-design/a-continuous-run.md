# A 체크포인트 없는 실제4인 연속 검증

2026-10-30 사용자 후속 요청. 초기 별도 Windows 빌드 방식에서 사용자와의 정정 후 Unity Multiplayer Play Mode(메인 Editor+가상 Editor3개)로 변경했다. 정상 입력·정상 재시작만 사용하며 위치 재배치(setup)는 금지한다. 모든 세션의 연속 위치/상태와 입력 원문은 `.harness/local/a-continuous-run/`에 보존한다.

- 시작 전 dev fetch: cf8bcab4(캐릭터 외형2커밋), private94c98cb9. 기존 로컬 겹침을 stash/patch로 보존한 뒤 --no-commit 통합. 외형 코드의 운반/다인 그네 변경은 복원, 지식 색인 양쪽 항목 유지. 폰트 충돌은 동적 캐시 차이여서 새 dev 정본을 불러와 Unity API로 기존 캐시 초기화 상태 복구. private11개 importer 변경은 새 dev와 동일하여 적용 후 clean. 복구용 stash는 유지한다. commit/push 없음.
- doctor: 기존 Task077/079 중복·private 미저장 FAIL, CLI버전·skill링크 WARN. private 미저장은 최신화 후 해소. 게임 실행은 별도 컴파일/빌드 확인.
- MPPM은 네 실제 Editor의 NGO 세션 접속을 확인했다. A가 Build Settings에 없어 참가자의 Scene Hash 조회가 실패한 문제는 기존 씬 목록을 보존하고 A를 추가하여 수정했다. 각 프로세스의 명시적 개발 검토 설정과 백그라운드 실행을 연결했다.
- mppm2: 출발점→첫 대포→첫 외줄→첫 고정 그네까지 네 명이 정상 입력으로 진행했다. 이후 두 명이 상부 외줄 착지에 실패했다. 컨베이어와 이후 구간의 무이동 연속 완주 근거는 없다.
- 최신 사용자 지시로 네 Editor의 Play를 모두 중지하고 검토용 EditorPrefs를 각 프로세스에서 해제했다. 자동 입력·플레이를 재개하지 않는다. 컨베이어 동선/벽/추락 복귀 보수가 현재 작업이다. **연속 완주 미완료**.
