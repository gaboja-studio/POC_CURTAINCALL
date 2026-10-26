# M2 Windows 개발자·로그 빌드 — 2026-10-26

- PM 요청: 0.2.0 Development Build + 게임 Dev Mode + 로그 수집. 직접 플레이 검증 예정. 설문 끔 유지.
- Unity build_734ef67ce97a: StandaloneWindows64, Development + DetailedBuildReport, Succeeded, 오류0/경고11, 53.133초. 기존 활성 씬 목록 유지.
- 출력: `Builds/Telemetry-0.2.0-Windows-TEST/CURTAINCALL.exe`. `개발자 모드로 실행.lnk`는 해당 exe에 `-devmode`를 전달한다. 일반 exe 직접 실행은 Dev Mode가 꺼진다. 폴더 안 테스트 안내.md 포함.
- 내장 manifest: gameVersion0.2.0, LOGS만 포함, logVersions=*. 전송 설정 유효 여부만 확인했으며 비밀값 출력 없음.
- 실제 exe를 -devmode로 시작해 수집 시작·자동 전송 설정 완료·CHECKPOINT 로컬 저장 확인. 공연/방 생성·업로드 ACK는 이번에 대행하지 않았다. 종료 원인은 확인하지 않았으며 사용자 직접 플레이로 인계한다.
- Google 기존 수신 URL v35(호스트 명단·도착 계약), 대시보드 URL v36(타임라인·설문 범위/회차) 배포 완료. 권한/소유자/기존 URL 유지, 편집기 최종 소스는 dashboardEnabled=true. 실제 폼 편집·설문 제출 없음.
- 증거: `.harness/local/m2-windows-build.json`, `.harness/local/m2-dashboard-v36.png`, 빌드 폴더 `검증 실행.log`.
- 빌드 전 설정/폰트44개 백업 `.harness/local/m2-build-backup-20261026/`. 빌드 후 ProjectSettings 변경은 요청한 bundleVersion0.1.0→0.2.0만. 추가 폰트/URP/씬/Packages 자동 변경 없음.
- commit/push 없음. 두 PC·일반 플레이 검증은 PM 진행. 로그 전송은 호스트로 방을 만든 뒤 확인한다. 대시보드 TEST/0.2.0, 최근 완료 후 최소5분 갱신 규칙 유지.
