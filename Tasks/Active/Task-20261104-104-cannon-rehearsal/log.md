# Log

## 2026-11-04
- feat/rehearsal-flow(c2f689bb+문서) 위에서 feat/cannon-rehearsal 분기. Unity 에디터가 열려 있어 origin/dev(c04997b2, PR 287 병합 포함) 반영은 에디터 종료 후로 미룸. 로컬 전용 변경(폰트 2·escape-labels·BaseGameSettings·ThirdParty) 보존.
- **사용자 지시로 구현 보류(2026-11-04).** 코드·씬·프리팹 변경 없음. 아래는 재개 시 쓸 조사 결과.
- 사용자 정정: 착지 지점은 연습실 링 바닥이다. 링 아래 지하 맵을 파지 않는다. 시작대·1단계 도착 장소·2단계 도착 장소는 링 위에 높인 발판, 트램은 1단계 도착 발판에서 링 바닥으로 내려간다.
- 차용 가능: HumanCannonSystem/Trampoline/RouteSystem/Transport/LandingZone은 NetworkBehaviour 없는 이름 붙은 메시지 구조로 탈출로 씬에서도 재사용 중. 씬당 하나(Current) 제약. Prepared 프리팹: 대포 발사 전방 7.2·상방 12m/s, 원형 트램펄린 반동 12m/s·requireCarrierForBounce=false, 운송 발판 6×8m·overloadTrap=true(공연 출발 운송 0번과 같음. RouteSystem 1번 칸은 쉼터 대기 조건이 있어 0번 칸 사용).
- 범위 관리: HumanCannonSystem을 켜면 점프 높이 덮어쓰기·부상 정책이 등록되므로 리허설 중에만 켠다. 직렬화 필드 setter가 없어 런타임 생성 대신 에디터 빌더로 리허설 코스 프리팹을 만드는 방식이 공연 코드 무수정에 맞다.
- 무부상 즉사: 공연은 비착지 착지 시 2곳 절단(HumanCannonSystem.Landing), 억제는 EscapeDevelopmentSession.FallInjuriesSuppressed뿐. 공연 코드 무수정안: 리허설 중 링 바닥에 착지 구역을 깔아 부상을 막고, LandingConfirmed 위치로 리허설이 Fallen(래그돌=사망 모션) → 1.5초 뒤 ServerRestoreAll → 체크포인트 이동(외줄 리허설 추락 복구와 같은 방식).
- 시간 정지: 연습실(Area_S09)은 탐색 타이머 정지 구역이고 밤 정비 시간은 무제한이라 새 코드 불필요.
- 진입: ScheduleBoard.Set에서 인간대포·미이수면 확인 패널(RehearsalVotePanel 재사용). 스케줄표 창은 순간이동하면 자동으로 닫힌다. 재진입은 연습실 PracticeLanes/HumanCannon(해금 시 보이는 기물)에 RehearsalPracticeStation 같은 상호작용을 붙인다.
- 충돌 주의: 밤에는 PracticeGate(-17.2,0,86.5)를 포함한 모든 NightDoorLock 철창이 잠긴다. 종료 후 "연습실 입구"는 철창 바깥(로비 쪽)에 두어야 갇히지 않는다. 연습실 대포 재진입은 밤에는 연습실이 잠겨 불가(쉬는 시간만 가능).
- 링 실측: 링 바닥 DeckCollider 중심(-48.5, 0.6 윗면, 112) 40×40m, 천장 충돌체 없음, 연습실 둥근 벽 높이 12m.
- origin/dev 미반영(Unity 에디터 열림).

- 2026-11-09 검사: 안전성=FAIL; 문서/규칙=SKIPPED(선행 실패); 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-09 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-09 대기실 오브젝트 배치 창 — 장식물/상호작용 분리·일괄 끄기 (사용자 요청, 이 브랜치에서 진행)
- origin/dev(2d591802) 병합 후 진행. 배치 창 맵 패널에 "장식물 끄기"/"상호작용 오브젝트 끄기" 체크와 분류별 선택 버튼 추가(`WaitingRoomObjectWindow.Visibility.cs`).
- 분류(사용자 지시로 수정): 상호작용=기존 IsInteractionObject 루트. 장식물=분위기 키워드(조명·깃발·녹·배관·잡동사니·소품)에 걸린 것만. 필수 시설(연습실 링·기구, 침대·가구, 상점, 로비 천막 본체, 승강기, 무대 입구 통로, 관리실·화장실 설비, 게이트·셔터·CCTV)과 구조물, 판정 불가 물체는 항상 표시.
- WaitingRoom 활성 렌더러 기준: 상호작용 779 / 장식 7937 / 필수·기타 3577 / 구조물 5550. 승강기·무대 입구 장식 0, 연습실은 깃발·전구·밧줄·조명만, 천막은 전구 줄·장난감 소품만 장식.
- 씬 뷰는 SceneVisibilityManager(에디터 전용), 창 내부 맵은 렌더 중 임시 마스킹. 씬 파일·활성 상태 무변경.
- 검사: check-work 문서/규칙 FAIL(Task-097 todo.md 102줄 hard 등 다른 Task 문서 길이, 이 작업과 무관) → 컴파일은 Unity 배치 모드로 별도 실행, error CS 0건(Assembly-CSharp-Editor 빌드 확인). 테스트는 요청 없어 미실행. 실제 분류 결과는 사용자가 에디터에서 확인 필요.
- 추가 요청: 배치 창 표시 층에 "전체 층 (모아 보기)" 추가(`WaitingRoomObjectWindow.AllFloors.cs`, 맵 렌더 공통 함수 분리). 한 텍스처에 층별 뷰포트로 렌더, 보기·선택 전용. 열린 에디터 재컴파일 오류 0(콘솔 에러는 Vivox 403만, 무관). 창에서 직접 조작 확인은 사용자 몫.
- 추가 요청: 맵 크기 조절(TwoPaneSplitView 경계선·"맵 크게 보기"·보기 설정 접기, 최소 창 크기 640×480), 전체 층을 나란히 칸 대신 한 화면 겹침으로 변경(층별 투명 배경 RT, 위층 비침 슬라이더 기본 0.45, 층별 색). 재컴파일 오류 0. 투명 배경이 URP 미리보기 카메라에서 유지되는지는 창에서 직접 확인 필요.
- 추가 요청: 표시 층·보기 설정·배치 도구를 오른쪽 패널로, 맵은 가운데 전체 높이로. 도킹 창 855×610 기준 맵 높이 227→589px(창 레이아웃 실측). "큰 창으로 분리" 버튼(에디터 90% 독립 창) 추가. 재컴파일 오류 0.
- 추가 요청: 전체 층 보기를 겹침 대신 "지상 왼쪽 크게 + 오른쪽 열에 지하 층"으로 변경. 이전 칸 방식은 모든 칸이 같은 중심을 써서 지하가 칸 밖에 있었음 → 칸마다 그 층 방 범위로 따로 맞춤(실측: 0m 칸 중심 z83/크기150, -5m z104/18, -10m z86/46). 재컴파일 오류 0.
- 2026-11-10 Alt 배치 안 됨: 카드 Button이 Alt 클릭을 무시하고 카드→맵 끌어 놓기가 없었음. Alt 클릭 activator·카드 끌어 놓기 배치 추가, 더블클릭 시 씬 뷰도 기물로 이동. 컴파일 PASS, WaitingRoomObjectWindow EditMode 9/9 PASS. check-work는 기존 rehearsal-flow todo.md 길이(HARD)로 문서/규칙 FAIL. 실제 끌어 놓기는 사용자 확인 필요.
