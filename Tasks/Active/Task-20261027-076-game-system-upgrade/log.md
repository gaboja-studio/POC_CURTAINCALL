# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-10-27 환경 준비
- 시작 doctor(-SkipEditor): FAIL 1(task-ids 061·062·070), WARN 2(unity-cli 문서 버전 차이·skill-links 없음).
- task-ids: 원인은 병합 완료된 `integration/task-document-closeout` worktree의 `Tasks/Active`에 남은 빈 폴더 3개였다(Done 이동 후 Git이 빈 폴더를 남김, 파일 0개). 빈 폴더만 삭제했다.
- Unity CLI `1.0.0-beta.13` → `beta.14` self-update. 변경 사항을 `Harness/Engine/Unity/Facts/unity-cli.md`에 기록했다. `verify-unity.ps1`은 폐기 예정인 status 행 `project` 대신 `projectPath`를 먼저 읽게 했다. 에디터를 연결한 상태로 확인하지는 않았다.
- 재실행 doctor(-SkipEditor): FAIL 0, WARN 1(skill-links 없음, 로컬 선택 설정).
- `setup-links.ps1` 실패: Windows 심볼릭 링크에 관리자 권한 또는 개발자 모드가 필요하다(개발자 모드 꺼짐). 실패로 남은 빈 `.claude/skills` 폴더는 지웠다. 사용자가 개발자 모드를 켠 뒤 다시 실행한다.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)
