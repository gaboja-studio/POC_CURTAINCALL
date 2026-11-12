# Log

이전 기록(2026-11-10~11: Task 생성, 기물 연결, 환풍구, 공간 프리팹화, dev 병합): [History/001-log-until-20261111.md](History/001-log-until-20261111.md)

## 2026-11-12 추격자 배치를 방 공간 프리팹에 포함(사용자 요청)
- 문제: 마네킹 배치 26개(PursuitPlacement + WatchPoint)가 씬 `PursuitSystem/<방>` 아래에 있어 공간 프리팹에 없었음. 에디터의 WaitingRoom 미저장 변경은 사용자 지시로 버리고 진행.
- 이동: 각 방 프리팹 루트 아래 `PursuitPlacements`로 복제(월드 위치·회전·감시 지점 유지). 방 프리팹이 없는 영역은 Lobby→S01_Lobby, F8_Cargo·ServiceGallery→FacilityConnections. 프리팹 14개 변경. `PursuitRoom.Placements`를 프리팹 안 배치로 다시 연결하고 씬 쪽 옛 배치 삭제.
- WaitingRoomBase: 같은 프리팹 배치로 다시 연결. 25개는 값 동일, 폐기물 보관소 Mannequin_Male_03만 Base 값(위치·회전·외형)이 달라 인스턴스 오버라이드로 유지. WaitingRoomPursuit는 공간 프리팹을 쓰지 않아 그대로.
- 배치 창: `PursuitAuthoring` 추가·이동·삭제·감시 지점 편집이 공간 프리팹 인스턴스면 0.4초 뒤(드래그 종료 후) 해당 배치만 프리팹에 적용. 새 배치는 방 프리팹의 `PursuitPlacements`에 생성. 부모가 방이 아니어도 소속 방을 찾도록 `PursuitPlacement.OwnerRoom()` 추가(기즈모·먼 경로 판정).
- 검증: 추가·삭제를 실제로 실행해 프리팹 자식 수 반영·되돌린 뒤 프리팹 파일 해시 동일 확인. 추격 씬과 통합 씬 배치 비교에서 24개 동일, 차이 2개는 기존 차이(소품실 Male_04 Size=Medium, 폐기물 Male_03 위치).
- 테스트: EditMode Pursuit 141 중 140 통과. 실패 1 `UnifiedWaitingRoom_PreservesPursuitAuthoringAndSceneReferences`는 위 기존 차이 2개 때문(순서 비교는 AreEquivalent로 변경). WaitingRoom 전체 409 중 406 통과, 나머지 2는 기존 문구 파일 실패.
- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(check-work 자동 테스트 미선택, 위 EditMode 별도 실행)
- 2026-11-12 대기실 최종본 기획 정리·사용자 답변 반영(plan "대기실 최종본"). 컴퓨터 입력은 기존 4자리 키패드(ExplorationPanel)를 2칸 입력으로 확장해야 함.

## 2026-11-12 대기실 최종본 1·2단계: 통로 ↔ 작업실·소품실 맞바꿈(사용자 구조도, 미커밋)
- 톤앤매너는 Confluence 6881292(폐공장·교도소 느낌 대기실, 따뜻한 전구색) 기준. 옛 구조 A 컨셉 이미지는 쓰지 않음(사용자).
- 도구 `FinalLayoutBuilder`(PassageGimmicks/Editor). 1단계: S06·S07을 로비 중심 반 바퀴+서쪽 3.7m로 북쪽 이동(입구 x-14.4~-5.0, z91.65~), 작업실(x-23.4~-5.2, z51.35~61.66, 문 동쪽)·소품실(x-2.1~10.2, z51.35~61.95, 문 서쪽)을 로비 남쪽 전실(x-5.2~-2.1) 양옆으로 이동. 방 내부·크기는 그대로(인스턴스 위치 덮어쓰기). 딸린 탐색 영역·추격 지점·씬 루트 기물 15개 함께 이동, 바깥 어두운 상자 z176까지 연장.
- 2단계: 로비 원형 벽 사본 메시(LA_Frame8LobbyWall_Final)에 북쪽 입구를 내고, 남쪽 옛 입구는 전실 폭만 남기고 막음. 북쪽 쐐기 바닥·마감 벽, 전실 바닥·벽·지붕, F8_PropRoom·WorkshopBranch 영역과 작업실 철창을 전실로 이동.
- 코드: `WaitingRoomRehearsal.Lockdown` 봉쇄 철창·통로 모니터 위치를 씬 표식(FinalLayout_Swap)으로 구분(Base 씬은 옛 값 유지).
- 남은 것: 새 공간(긴 복도·창고·이전 세대 생활 장소) 제작, 새 공간 탐색 영역, 추격 NavMesh·TraversalLinks 재굽기(PursuitSceneBuilder 경로 수정 필요), 오클루전 재굽기, UnifiedWaitingRoomSceneTests 기대값, 플레이 확인. WaitingRoomBase는 옛 배치 그대로.

## 2026-11-12 대기실 최종본 3단계: 새 공간 제작(미커밋)
- 사용자 승인 뒤 `FinalLayoutBuilder.BuildNewRooms`(다시 실행 가능, `FinalLayout/NewRooms` 통째로 다시 만듦).
- 긴 복도(x-13~13.35, z45.85~50.85, 높이 5.2): 배관 연결 통로 서쪽 끝을 터서 이음. 잠긴 장식 문 3개(서쪽 끝·북벽·남벽), 전구 3개.
- 이전 세대 공연자 생활 장소(x-20~2, z37~45.35) + 남쪽 화장실(x-20~-9.25)·소품실(x-8.75~2, z30.5~36.5). 복도 남벽 문(x-3~-1)으로 들어감.
- 창고(x-26.7~-20.5, z35~45.35): 생활 장소 서벽 문, 무대 장치 보관실 동벽(CargoOuterReturn)에 문(z39~41.2).
- 가구 21개는 숙소 침대·사물함·옷장, 화장실 설비, 소품실 선반·의상, 드럼통을 복제(게임 스크립트 제거, 겉모습·충돌체만).
- 탐색 구역 3개(Area_N1_BackCorridor·N2_OldQuarters·N3_Storage)를 타이머가 흐르는 구역으로 등록, HUD 이름 추가.
- 벽 메시 사본: LA_Frame8PipeLinkWalls_Final. `DormVentBuilder.CutWall/Split`을 internal로 공개. 주의: DormVentBuilder를 다시 실행하면 _VentOriginal 복구 때문에 이 문 구멍도 되돌려질 수 있음.
- 확인: 편집 화면 캡처(final-layout-step3.png, final-quarters.png)만. 플레이·추격 NavMesh·오클루전·테스트는 아직.
- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함). log 이전 기록은 History/001로 옮김.

## 2026-11-12 대기실 최종본 3단계 수정·4단계: 세부 공간과 기계실 배선(미커밋)
- 사용자 지시 반영(`FinalLayoutBuilder.Details`). 창고: 1층 방을 없애고 높이 9.5m 마당에 컨테이너 3단, 맨 위(바닥 5.6m)가 창고. 마당 동벽 철 계단(약 40°) → 캣워크 → 컨테이너 남쪽 입구 → 서쪽 창문(z40.2~41.8, y6~8, 턱 0.4m) → 무대 장치 보관실 캣워크. 보관실 동벽 1층 문은 되돌리고 같은 자리에 창문 구멍.
- 화장실: 샤워실 겸용. 변기 3·칸막이는 서벽, 세면대·거울은 동벽에 붙이고 남벽에 샤워 칸 3개(배관·밸브·배수구). 설비 통째 복제 대신 개별 복제.
- 긴 복도: 사무실 복도(허리 높이 초록 칠, 낮은 천장 3m, 카펫, 형광등 4개 중 1개 꺼짐, 불투명 유리창 9개, 닫힌 사무실 문 4개 = 북벽 2·남벽 1·서쪽 끝 1). 문은 장식.
- 4단계 `BuildMachineWiring`: 기계실 서벽 배관함 북쪽(z94.15~95.3)에 배관함을 복제한 녹슨 벽(BreakableWall `final:machine-rust-wall`, 뽑개 6/맨몸 15 그대로). 부수면 안쪽 배선(EscapeCuttableCable, 기존 cable-red 재질)이 켜짐. PortableSaw 인스턴스에 배선 연결.
- 코드 1곳: `EscapeExitSaw.Projectiles`에 `openTarget` 문자열 추가. 필수 배선을 다 자르면 `ExplorationState.MarkVisited(openTarget)`. 대기실 절단기 값은 `structure-a:management-door`(문 자체가 장부에서 열려 재시작에도 유지). 탈출로 절단기는 빈 값이라 영향 없음.
- 하지 않은 것: 전체 조명 1회 소등·3초 뒤 점등(기존 기능은 깜빡이는 포획 암전뿐, 새 코드 필요 → 사용자 결정), 관리실 열쇠 삭제, 톱날 발사기 작업실 이동.
- 확인: 편집 화면 캡처(final-office-corridor·final-washroom·final-container-yard·final-container-window·final-rust-wall.png)만. 플레이(계단 오르기, 창문 넘기, 절단기 들고 지하 이동, 절단→문 열림) 미확인. 절단기 안내 문구는 탈출로용("열린 출구로 이동하세요") 그대로. NavMesh·오클루전 재굽기 전.

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-12 검사(세부 공간·기계실 배선): 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-12 조명 소등·창고 재설계(미커밋)
- 조명: `NightPower`에 `blackoutCables`·`blackoutSeconds`(3초) 추가. 연결된 배선이 잘리는 순간 회로 전체가 꺼졌다가 켜짐(배선 상태가 동기화되므로 각 참가자 화면에서 처리). 기계실 숨은 배선을 연결하고 새 공간 조명도 회로에 넣음. 주의: 새 공간을 다시 만들면 4단계(`BuildMachineWiring`)도 다시 실행해야 회로·배선 연결이 유지됨.
- 창고(사용자 참고 이미지): 방을 나누지 않고 생활 장소를 높이 9.5m 홀로 만들어 서쪽 끝에 컨테이너 사무실/창고가 서 있는 구조로 변경. 생활 장소 서벽 제거, 컨테이너 3단(z37.2~44.6) 동쪽 긴 면에 창·바깥 계단(남→북, 약 41°)·발판·옆문. 맨 위 단 안쪽 서쪽 창문 → 무대 장치 보관실 캣워크는 그대로. 보관실 캣워크 높이(5.6m) 때문에 3단임(이미지는 2단).
- 확인: 편집 화면 캡처(final-container-hall·landing·window.png)만. 플레이 미확인.

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-12 검사(조명 소등·창고 재설계): 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-12 추가 수정(사용자): 컨테이너 2단으로 변경(위 단 바닥 2.9m, 홀 천장 7.5m, 계단 z39.6→43). 창문(y3.3~5.2)은 보관실 캣워크(5.6m)가 아니라 그 아래로 나가 바퀴 받침(1m)→바닥으로 내려섬. 한 방향(보관실에서 되돌아 올라올 수 없음). 복도 붉은 카펫은 사용자가 지운 것이라 빌더에서도 삭제. 주의: `BuildNewRooms`는 `FinalLayout/NewRooms`를 통째로 다시 만들어 그 안의 수동 편집을 지움.

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-12 검사(2단 컨테이너·카펫 삭제): 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-12 origin/dev 병합(씬 충돌 해소)
- 충돌: `WaitingRoom.unity`·`WaitingRoomBase.unity` 각 11곳. dev `c1589ff7`(연습실 트램펄린 반출·인간대포 발사 시야)이 고친 연습실 물체가 내 쪽에서는 공간 프리팹으로 빠져 있었음.
- 해소: 두 씬은 내 쪽(공간 프리팹 구조) 유지. dev 값 11개를 `S09_PracticeRoom.prefab`에 옮김 — 입구 계단 3단·경사 충돌체 폭 2.2→6, 입구 쪽 난간 윗대 2·기둥 2·깃발 2·전구 1 비활성. 물체는 이름·부모 경로·위치가 모두 같은 것으로 대응(각 1개씩 일치). `WaitingRoomPursuit.unity`는 자동 병합.
- 양쪽이 함께 고친 파일은 위 씬 2개뿐(코드 겹침 없음).
- 검사: 안전성=PASS; 문서/규칙=FAIL(dev에서 온 Task-073 log.md 132줄, 한도 130); 컴파일=ENV_BLOCKED(Unity 에디터 미실행); 테스트=SKIPPED. 에디터에서 컴파일·연습실 입구 계단 확인 필요.
