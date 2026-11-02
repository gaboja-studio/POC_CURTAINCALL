# Log

- 이전 Play 관찰·카메라·부상·배치 기록: [History005](History/005-camera-and-live-followup-log.md).

## 2026-11-03 대기실 씬 선택
- 사용자 이동 상태(InGame Start/Lobby/Pursuit 및 대량 에셋 이동)를 보존. 에디터 clean/정지 후 정상 종료, origin/dev fetch 성공. 현재 커밋과 dev가 갈라져 ff-only 중단; 이후 dev 병합은 사용자 의도대로 별도 진행. 동일 Task doctor 반복 없음, PID10344 재개방.
- BaseGameSettings/SpaceTransition.waitingRoomVariant(Base 기본/Pursuit)로 대기실·통합 탐색 경로 선택, 기존 커스텀 더미 씬 문자열 호환 유지. Tools/Dev Mode/대기실 씬 설정 창·Dev 시작 패널 대상 표시. 호스트 SpaceRoute로 로드하므로 클라이언트 개인 설정이 로드를 바꾸지 않음.
- 공통 대기실 판정을 카메라/인벤토리/ESC/로그/설문/음성 및 진입 성능 관찰에 연결. PursuitAuthoring은 이동된 InGame 경로. Base는 기존 타이머 흐름, Pursuit만 Enabled와 함께 추격 런 규칙을 사용. Base 전용 저작 도구·튜토리얼 씬 원문은 변경 없음.
- Edit3/3 PASS(.07초): 양쪽 라우팅·에셋/빌드·음성·커스텀 경로. 실제 호스트 Dev 시작 Base→Pursuit Play1/1 PASS(19.54초): UI 단일 인스턴스·카메라·로그 분류·설정 전환. 원격 클라이언트 씬 선택 실기는 미검증.
- Start→Pursuit 2회 재로드 ESC/인벤토리 Play1/1 PASS(20.38초). 회귀는 테스트별 씬 선택을 메모리에서 설정·복원하여 공유 설정에 의존하지 않도록 보완. 설정 창 열림/SerializedObject 속성 확인, 현재 Base 선택 저장·대기실/탐색 Base·추격 false·Start 씬 clean/Play 정지. 자동 회귀 고유5개 통과, 실제 다인 원격 수신은 미검증.

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- check-work 첫 실행은 새 설정 필드 앞 빈 줄 공백으로 diff-check 실패, 컴파일 검사는 선행 실패로 생략됨. 공백 제거 후 최종 검사 재실행.

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-03 GUID 이동 대응·더블클릭 Scene 포커스
- 기존 Path 상수는 이동 시 깨지는 한계가 있어 Base/Pursuit GUID로 AssetDatabase 조회, 빌드에서는 SceneUtility 경로 조회. PursuitAuthoring도 동적 경로로 변경. Unity .meta/GUID 유지하는 폴더 이동 지원, 런타임 씬 이름은 기존 두 이름 유지.
- 두 실제 씬을 AssetDatabase.MoveAsset으로 각각 임시 폴더 이동·경로 일치 확인 후 finally로 원위치/BuildSettings 복원 성공. 에디터 종료 후 fetch 성공, ff-only는 갈라진 브랜치로 불가(기존 변경 보존); PID10736 재개방, 동일Task doctor 반복 없음.
- Edit 탑뷰 마네킹 더블클릭은 순찰 지점보다 우선 선택하고 드래그 중단·Hierarchy 선택/Ping·Scene 뷰 근접 표시. Live 마커도 동일 포커스. 실제 모델 bounds만 사용하여 먼 순찰 경로로 줌 아웃하지 않음. 주변8방향 물리 장애물을 비교하고 Scene 뷰 확인 조명 사용, 씬 조명 데이터/배치 수정 없음.
- 실제 B1 ChangingRoom 마커를 마우스로 더블클릭하여 Scene 뷰 모델·주변 벤치·Hierarchy 선택 확인. 최초 단순 프레이밍은 벽 가림이 있어 크기/각도/조명을 개선하고 재확인. 선택=Mannequin_Male_01, 미리보기 존재, 씬 clean.
- 이동/복원 후 경로·라우팅·빌드 등록 회귀 Edit3/3 PASS(.05초, scene-guid-path-tests.json).

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-03 마네킹 이동과 순찰 동선 분리
- 원인: 순찰 지점은 Placement 자식이라 Scene Transform 기본 도구는 함께 움직이며, 기존 탑뷰 Move에만 월드 위치 보정이 있었음. Undo.postprocessModifications로 Scene/Inspector 이동·회전·크기 변경도 기존 월드 동선을 보존. 런타임/Play 중에는 동작하지 않으며 폴링 추가 없음.
- 사용자 확정: 가장 가까운 순찰 선분까지3D 거리10m 초과 시 해당 마네킹만 경로 해제. 배치 창 선택/순찰의 동선 자동 해제 거리로 개별 조정,0=해제 안 함. 공유 방 경로는 연결만 끊고 다른 마네킹/포인트 보존. 소유한 감시 지점은 Undo 가능한 제거, 창 알림3초.
- 첫 회귀에서 Undo 도중 포인트 위치·경로 연결 복원 누락 확인. Transform 후처리에서 자식 Destroy가 대기 중인 부모 Undo 기록을 flush하는 원인 분리, 삭제 전 부모 이전 자세 CompleteUndo 및 삭제할 포인트의 불필요한 자세 기록 제외로 해결.
- 최종 이동 회귀4/4 PASS(.55초): Scene 이동/회전/scale·Undo/Redo,10m 경계/0해제,먼 동선 삭제/복구,공용 경로 독립성. 기존 Authoring4/4 PASS(.61초). 별도 임시 씬에서 탑뷰 Move의 먼 경로 제거 Undo도 부모(0,0,0)/포인트(-2,0,0) 복구 확인. 총 고유8개 회귀.
- 실제 창에서 제거 알림 표시/자동 사라짐 확인. 사용자 씬 배치24체 보존, 씬 clean 확인. 정상 종료/fetch 성공, ff-only는 분기 이력으로 불가·변경 보존, PID3224 재개방. 같은Task doctor 반복 없음.

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-03 Start 씬의 미리보기 잔류
- 현장 확인: Start clean/Play 정지인데 __Preview_Pursuit_*21개와 __PursuitDesignerCamera1개가 scene=null/HideAndDontSave로 생존. 창 Cleanup 호출 후22개 모두0, Start dirty=false. 저장된 마네킹이 아니라 편집기 미리보기 수명 결함.
- 숨김 탭 Tick 정지 + 씬 변경 콜백 부재 + HideAndDontSave의 씬 언로드 생존이 원인. Scene closing/opened/active changed, assembly reload, quit에 정리. 창 Disable에서 구독 해제. Refresh는 편집 모드 활성 씬 Director만 선택, Director 소멸 시 잔여 자원도 정리.
- 실제 씬 Edit 회귀2/2 PASS(6.92초): Pursuit→Start 단일 전환, 창 숨긴 채 추가 로드/활성 전환, 모델·카메라·RT 파괴/Start에서 재생성 없음, Pursuit 재진입 재생성, 배치 데이터 불변. preview-lifetime-tests.json. 최종 Start만1씬·clean·미리보기0.
- 정상 종료/fetch 성공, ff-only는 분기 이력으로 불가·현재 변경 보존, PID32536 재개방. 동일Task doctor 반복 없음.

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-03 CLI 상태 확인의 Console 오류
- CLI1.0.0-beta.15 + Pipeline0.8.0-exp.1 명령 이름 호환 문제. Editor.log 동일 문구 횟수 측정: unity status 호출1회로1→2 증가, 명시적 editor_status 추가 호출은2→2 유지. 게임/추격 코드 예외가 아니며 check-work의 verify-unity 선행 unity status가 반복 발생원 중 하나였음.
- verify-unity는 --project-path 지정 editor_status를 직접 호출하고 응답 projectPath 필수/경로 일치 확인으로 대상 검증 유지. CLI 자체/doctor/worktree 등 외부 unity status 호출은 여전히 구형 패키지에서 같은 메시지를 낼 수 있음.

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 변경 후 verify-unity 직접 실행 및 check-work 전체 실행 모두PASS, 각각 Editor.log 해당 오류 횟수2→2로 증가 없음. 기존 Console 로그는 지우지 않았으며 다른 호출자의 unity status는 여전히 같은 호환 오류를 만들 수 있음.

## 2026-11-03 로비 곡면 천장 재투과
- 원인: 평면40개 충돌 면은 유지됐지만 로비/연습실 DomeCap·DrumWall4개는 Collider/카메라 면 모두 없음. 실제 로비 카메라 회귀에서 천장 최고12m에 카메라15m로 실패 재현(round-ceiling-before-tests.json). 씬 선택/폴더 이동 문제가 아님.
- CameraCollisionSurface.UseMeshGeometry + CameraMeshCollision 양면 구체 스윕(면·모서리·꼭짓점·시작 겹침), 월드 삼각형 캐시/경계 선별. 해당4개만 Unity로 연결·씬 저장, 기존 배치/설정·Base 씬/물리/NavMesh 보존. 테스트 씬 조회는 GUID로 이동 대응.
- Edit6/6 PASS(.05초): 앞/뒷면·빈 공간·모서리/끝점·겹침·음수/비균일scale·변환 캐시 갱신·기존OBB/편집pipeline. Play3/3 PASS(23.86초): 로비/연습실 실제 돔, 숙소 천장, 물리벽·수평천장·점진 복귀. round-ceiling-edit-tests.json / round-ceiling-play-tests.json.
- 실제벽5도 간격72방향 조회: 로비42차단/30개방, 연습실69차단/3개방; 출입구를 전체bounds로 막지 않음, Collider0. 수동LAN 호스트 frame166→803→1517 진행, 카메라Y11.68/실내 화면 정상(Temp/round-ceiling-camera-after.png). 예열 후1000회44면 조회 평균0.282ms, 측정 관리힙 할당0byte(에디터 단일 지점 측정).
- 에디터 비포커스 때 검사 완료 콜백/CLI가 지연돼 포커스로 재개했으며 최종 결과 파일 확인. Play 종료 후 Start 복원. origin/dev fetch 성공, ff-only 분기 이력으로 불가·기존 변경 유지. 같은Task doctor 반복 없음.

## 2026-11-03 Base/Pursuit 전체 카메라 충돌
- 사용자 요청으로 Base도 수정. 씬별MeshFilter23118개(활성14571개) 조사, 활성 경로/메시/중심/크기 차이0. 공통 구조물430개(OBB354/메시76): 모든 방 Shell·원통 벽/돔·통로·위층 바닥·보강판/보·엘리베이터/외벽·칸막이·출입문을 검토. 간판/조명/효과/가구는 분리. all-rooms-map-comparison.json.
- Base430개 추가, Pursuit386개 추가·기존6개 복합 통로를 OBB→실제면 검사로 변경. 기존 PassageFloorCeiling의 Mesh.bounds가 통로 내부를 채우던 결함 해소. Renderer.enabled=false이면 카메라 차단도 제외; forceRenderingOff는 무시. Tools/CurtainCall/Camera/현재 대기실 벽·천장 충돌 보완은 명시적 공통 대상 규칙으로 현재 씬만 멱등 보완·Undo 지원.
- Unity에서 양쪽 씬 저장. Transform Base29918/Pursuit30259개의 경로·위치/회전/크기/활성 상태와 Collider3764개씩의 직렬화 원문 전후 일치. 사용자 배치/동선·튜토리얼·물리/NavMesh 데이터 보존(all-rooms-apply.json, 상세 before는 Temp/camera-structure-before-*.json).
- 최종 Edit2/2 PASS(3.38초): 저장된 두 씬 각430구조물의 양면 샘플3000회 이상·복합 통로7개 내부·멱등 적용. Play 신규3/3 PASS(22.78초): Base/Pursuit 방9곳씩 실제 카메라 천장 검사·통로6곳씩 여유·패널 숨김/비활성/복원. 기존3/3 PASS(23.41초): 숙소 천장/돔2곳/벽·수평천장/점진 복귀. all-rooms-edit-tests.json, all-rooms-play-tests.json, all-rooms-existing-regression-tests.json.
- 첫 Edit 숨김 검사는 편집 모드에서 런타임 OnEnable이 호출되지 않아 실패해 Play로 옮겨 검증. 기존 천장 비교는 새 보강판이 계속 차단해 대비가 없어 이웃 면을 테스트 중에만 해제/복원; 연습실 pivot의 새 천막 겹침도 제거. 실제 씬 보호를 완화하지 않고 테스트 환경을 보정, 초기 실패 보고서 보존.
- Play frame1→1164 진행. 전체430면 5지점×500회 예열 후 평균.399~.567ms/조회, 관리힙 할당0byte(all-rooms-profile.json). 에디터 국소 측정이며 전체 게임 FPS 수치가 아님. CLI 5초 응답 제한이 걸린 긴 조사/저장은 완료 파일·후속 검증으로 확인했다.
- 작업 재개 시 clean/Play 정지 확인·정상 종료, origin/dev fetch 성공/ff-only 이력 분기로 불가·기존 변경 유지, PID2380 재개방. 동일Task doctor 반복 없음. 이전 완료 기록을 History005에 원문 보존. Play 종료·Start clean 복원, Pursuit 선택 유지.

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
