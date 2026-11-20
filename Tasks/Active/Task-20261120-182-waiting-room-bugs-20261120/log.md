# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-20 배관 구역 벽 깜빡임: 뒤쪽 복도(BackCorridor) 북벽의 `WallPaintLow`·`WallPaintHigh`·`ChairRail`이 북벽 끝(x 10.3)을 지나 x 13.35까지 뻗어 배관 구역 얇은 벽 뒤에 들어가 있었고, 칠의 뒷면(z 50.85)이 배관 구역 안쪽 벽면(`Shell/WallSurfaces`, z 50.85)과 같은 평면이었음. 3개를 x -13~10.3으로 줄임(씬 Transform). `FinalLayoutBuilder.Details`도 북벽 칠을 `PipeZoneWestX`까지만 만들게 맞춤. 복도 쪽에서는 그 구간이 원래 배관 구역 벽에 가려 있어 겉모습 변화 없음. 캡처 `.harness/local/captures/20261120/`(수정 전).
- 단장 모니터 소리: `RingmasterMonitor`의 소리가 2D 15% 섞임 + 로그 감쇠라 최대 거리 밖에서도 들렸음. `SoundMuted` 추가, `LobbyMonitorDirector`가 내 캐릭터 구역이 `Lobby`·`Area_Lobby`·`Area_LobbyEast`(구역 상자 밖 `Area_Main`은 모니터 20m 안)일 때만 켬. 화면은 그대로, 소리 판단은 화면마다 따로.
- 리허설 마지막 구간(3, 공중그네): 예전에는 다음 봉을 잡으면 도착 처리 후 도착대로 순간 이동(2026-11-05 "현행 유지" 결정). 사용자 2026-11-20 지시로 바꿈 — 도착대에 실제로 서야 도착, 순간 이동 삭제, 봉 잡기 기록은 도착 필수 조건에서 뺌(빈칸 너머는 봉 비행으로만 닿음). 계산: 수업 둘째 봉에서 앞쪽 45° 이상으로 놓으면 줄 높이 착지 26.6~35m(빈칸 끝 26m, 도착대 35~38m). 안내 문구(코드 기본값·`WaitingRoomTexts.txt`·`TightropeTrapeze.View`) 수정. `Facts/Waiting-room/rehearsal-performance-reuse.md`의 "다음 봉 잡기로 완료"는 이 결정으로 대체됨(문서 갱신 전).
- 화물 선반: 추격자 직접 오르기가 "발부터 가장자리까지 끊김 없는 수직 벽"(`HasWallSupport`)을 요구해 단 사이가 빈 선반을 거부. `PursuitClimbPath.HasFrameSupport` 추가 — 오르는 쪽 1.1m 안에 잡을 것이 2.2m 안쪽 간격으로 이어지면 허용(직접 오르기에만, 구운 링크는 벽만). 선반 윗면 표본 227곳: 수정 전 직접 오르기 계획 90곳 → 수정 후 227곳(바닥 NavMesh 출발 기준). 아래가 통째로 빈 통로 바닥판은 간격 초과로 계속 거부.
- 로비 천막(`SlumpedConeTent`): 면 경사 53~78°로 걸을 수 없는 면인데, 둥근 몸통이 닿으면 접지로 판정되어 점프를 이어 꼭대기(5.3m)까지 올라설 수 있었음. 추격자가 설 평평한 윗면이 없어 따라 오르게 하는 대신 올라서지 못하게 함. `SteepSurfaceShed`(FinalLayout/Manual/TentTopShed, 대기실 전용, PlayerMover보다 먼저 실행)가 천막에 닿은 몸을 바깥으로 떼어 놓음. 편집 모드 캡슐 시뮬레이션: 천막 위 107곳에 떨어뜨리면 모두 1.2초 안에 내려옴, 점프 연타로 닿는 최고 높이 5.3m → 1.8m. 공용 이동 코드 변경 없음.
- 숙소 상자 점프 끊김: 미해결. 숙소 상자 3개(`dormitoryBox1/2`, `steelBox`) 주변에 숨은 충돌체 없음. 편집 모드에서는 `Time.deltaTime`이 0이라 실제 `PlayerMover`를 돌려 재현하지 못함. 추정 원인: 걸을 수 없는 접촉(상자·의자·스탠드 모서리)도 착지로 판정해 낙하 속도를 매번 초기화(`PlayerMover` 접지 판정). 고치려면 공용 이동 코드 수정과 플레이 재현이 필요 → 사용자 확인 대기.
- 검사: 컴파일 PASS, 공통 설정 PASS. WaitingRoom EditMode 570/576. 실패 6: 문구 파일 3건·`RehearsalCannonSourceTests.W6`(어제와 같음), `WaitingRoomSpawnRecoveryTests.MorningRevival…` 2건(스폰 복귀, 이번 변경 파일과 무관해 보이나 dev 기준 재현은 확인 안 함). check-work는 문서/규칙 FAIL(기존 Task-20261105-107 `guides` 폴더명)로 컴파일 단계 건너뜀. 플레이 확인 전. 커밋 안 함.
- 겹침: 다른 팀원 작업(추격 사망 기물 유지 등)과 같은 파일 없음(현재 기준). `origin/dev`에 새 커밋 9개(인간대포·외줄 톱날 코드, 이 작업 파일과 겹침 0) — 에디터가 열려 있어 아직 받지 않음, 제출 전에 받는다.
- 무관한 변경(커밋 대상 아님): 폰트 에셋 3개·`escape-labels.asset`(TMP 동적 글리프), `_ThirdParty` 서브모듈.
- 추격 NavMesh는 다시 굽지 않음(충돌체 변경 없음).
- 2026-11-20 dev 반영(12커밋, PR #459~462): 에디터 종료 확인 후 미커밋 변경을 stash → fast-forward → pop, 충돌 없음. `WaitingRoom.unity`는 양쪽 변경이 다른 자리라 자동 병합(표식 0, fileID 중복 0, TentTopShed 유지). dev에 같은 번호 Task-181(waiting-escape-bugs)이 들어와 이 Task를 182로 바꿈. 병합 뒤 컴파일·테스트는 다시 돌리지 않음.
