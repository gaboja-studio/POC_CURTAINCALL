# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-06
- 메인 폴더에서 에디터 빌드 진행 중이라 worktree에서 작업. 메인 프로젝트 파일·Unity는 건드리지 않음.
- 확인: 3개 ps1 구문 오류 0, `launch.ps1 -SelfTest`(메인 프로젝트 읽기 전용: Unity 6000.3.25f1 경로, 버전 0.2.2, 0.2.2 대상 설문 2개, 에디터 열림 감지 True), 설정 JSON 왕복 동일·무변경 시 쓰기 안 함, Unity Roslyn+6000.3.25f1 DLL로 PlaytestBuildRunner.cs·DevMode.cs(정의 포함) 컴파일 성공, 메인 창·설문 등록 창 캡처 확인.
- 미검증: 실제 Unity batchmode 빌드 실행(에디터 사용 중), Dev 빌드에서 Dev Mode 배지 표시, 로그 전송 설정이 있는 PC에서 LIVE 로그 도착.

- 2026-11-06 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)
