# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-16
- Jira CC-295에 버그 목록 체크리스트 작성. 완료 항목은 검증 후 체크한다.
- 관리실 컴퓨터 키보드 입력: ExplorationPanel에 실제 키보드 입력(숫자·영문·Backspace·Delete·Enter) 추가, 단말기 입력 중 F가 창을 닫지 않게 ExplorationGate 수정. Unity 플레이 확인 전.
- 기계실 배선: 휴대 절단기(씬 PrefabInstance)에 배선 5가닥(관리실 배선 1 + 탈출로 배선 4). 탈출로 길은 관리실 컴퓨터 + 탈출로 배선 4가닥 모두 필요(ExplorationAllOf). 관리실 문은 관리실 배선·빗장·키 카드 중 하나로 열림(any). 어느 문이 기대와 다르게 열렸는지 사용자 확인 대기.
- 기계실 배선(탈출로 문): 사용자 결정 — 배선 끊기는 탈출로 씬 기믹과 같아야 하므로 톱날 한 발 다중 절단 동작을 그대로 유지. 코드 변경 없음.
- 폐기물 보관소 단원 모델: 원인 — 전시 인형 옷 메시·재질(`Assets/_ThirdParty/CharacterArt/OldCostumes`, FinalLayoutBuilder 13단계 산출물)이 비공개 저장소 어느 브랜치에도 없어 씬 참조가 비어 보이지 않음(살펴보기 대상은 남아 상호작용 가능). 에디터에서 `ReplaceDisplayCostumes()` 재실행(eval 응답은 5초 제한으로 실패 표시였으나 작업은 완료) → 4벌 메시·재질 연결 확인, WaitingRoom 씬 저장. 남은 일: `_ThirdParty`의 OldCostumes 폴더를 비공개 저장소에 커밋·push하고 서브모듈 포인터 갱신해야 다른 팀원에게 보임.

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 침대: NightBed에 다른 플레이어 RestingBed 점유 검사 추가(침대 4 = 최대 인원 4). 점유 시 "다른 단원이 사용 중" 안내.
- 붉은 커튼(ProximityCurtain): 내 위치만 보던 판정을 동기화된 전체 플레이어 위치로 변경.
- 탈출 준비 UI(EscapeEntrance): 전용 HUD 판을 끄고 시스템 알림 카드(RoomNotice.Notice)로 표시, 인원 변하면 문구 갱신.
- TV 목마 해제: F 꾹 누르기를 테이프 꺼내기와 목마 해제가 공유 — 공통 조작 규칙 범위라 보류(사용자 지시: 공통 상호작용·조작 규칙 변경 제외).
- 환풍구: PlayerInteraction.CanInteract가 목마 위 플레이어도 허용 → 현재 부술 수 있음. 의도 확인 필요.
- 아침 조명: NightPower(밤 푸른 조명)·PursuitLighting(새벽 추격, 실행 순서 1100) 정적 분석으로는 복원 경로가 맞아 원인 미확정. 재현 조건 필요.
- 잠긴 문: 위로 열리는 문 후보(NightDoorLock 셔터, 튜토리얼 보호 시 셔터 높임, EscapeExitSaw doorLift) 중 대상 미확정.
- 검사: `unity recompile --strict` 오류 없음. check-work Fast FAIL 1건 = 다른 Task-107 `guides` 폴더 이름(이번 작업 무관), ThirdParty 미추적 5건(OldCostumes). Unity 테스트·플레이 확인 미실행.
- 잠긴 문 지형 뚫음(사용자: 위로 열리는 잠긴 문 전반): 원인 — 튜토리얼 보호 중 NightDoorLock이 셔터를 LockdownGateHeight(5.5m)까지 높이고, RehearsalLockdownGate도 5.5m로 세움. 셔터 위는 문틀·지붕(4.1~4.3m)이라 1.4m가 위로 솟음. 수정 — 위쪽 천장까지 광선으로 재서 그 아래까지만 높임(RehearsalLockdownGate.CeilingAbove 공용). 에디터 계산: Workshop·Basement·Practice 0m, Cargo 0.07m(이전 1.4m), 천장 없는 NightSchedule 2.5m 유지. 런타임 리허설 철창은 플레이 확인 전.
- 잠긴 문 보강(사용자: 위로 올라가 열리는 방식은 유지, 다른 지형을 뚫고 보이면 안 됨, 닫힐 때는 보이며 닫힘): RehearsalLockdownGate는 철창 묶음을 barHeight만큼 통째로 올려 열었음 → 천장 위로 솟음. 세로 창살은 천장(barHeight)까지만 남도록 잘라 그리고, 가로대는 천장을 넘으면 숨김(Lift). barHeight는 위 천장까지로 제한. 소품실 입구 철창 열림 55% 전·후 캡처로 확인(수정 전 문틀 위로 창살이 솟음 → 수정 후 문틀 아래까지만 보임). 런타임 실제 플레이 확인 전.
- 아침 조명(사용자: 대기실→대기실 이동 시 안 바뀌고 공연 씬 다녀오면 바뀜): 원인 — 밤에도 추격(Night + Pursuit 타이머)이 시작되면 PursuitLighting이 밤의 푸르고 어두운 값을 원래 값으로 기억해, 실행 순서 1100으로 NightPower의 아침 복원을 매 프레임 덮고 Release 때 밤 값으로 되돌림. 수정 — NightPower가 회로 조명의 지금 기준값(밤: 푸른 빛·낮춘 밝기, 낮: Awake 때 씬 원래 값)을 TryGetBase로 알려 주고 PursuitLighting이 그 값을 쓰도록 변경. SetDimmed(true)가 그 순간 색을 낮 색으로 다시 기억하던 것도 제거. 컴파일만 확인, 플레이 확인 전.
- 잠긴 문 재정정(사용자: 리허설 철창이 아니라 배터리·키 카드·배선으로 열리는 문, 관리실 연결 통로 문 사진): 대상은 ExplorationSlide 문(씬 8, B1_MachineRoom·S02_Dormitory 프리팹 각 1). 문짝을 openOffset만큼 통째로 밀어 위층 바닥·천장을 뚫고 보였음. 수정 — 밀려 움직이는 방식은 유지하고, 닫혔을 때 문짝이 차지하던 범위(문틀)를 벗어난 꼭짓점을 범위 끝면으로 눌러 잘라 그림(메시 사본만 변경, 원본 에셋 불변). 다 닫히면 원래 메시로 돌아감. 경고 줄무늬 문 모델 `Assets/_ThirdParty/Synty/PolygonApocalypse/Models/SM_Bld_Bunker_Wall_Caution_Door_01.fbx`는 Read/Write가 꺼져 있어 사용자 승인 후 켬(비공개 저장소 커밋 필요). 관리실 문 55% 열림 캡처: 수정 전 문틀 위에서 문짝이 위층 바닥 위로 튀어나옴 → 수정 후 안 보임, 방 안에서는 그대로 올라가며 문틀 속으로 들어감. 플레이 확인 전.
- 앞서 바꾼 리허설 철창(RehearsalLockdownGate) 잘라 그리기·셔터 높이 제한도 같은 증상이 있어 그대로 둠.
- 기계실 배선 재정정(사용자: 녹슨 벽 안 배선까지 모두 끊어야 문이 열려야 하는데 밖 배선만 끊어도 열림): 탈출로 길(final:escape-route) ExplorationAllOf 조건이 관리실 컴퓨터 + 밖 배선 4가닥뿐이었음. 녹슨 벽 안 배선(final:management-cable)을 조건에 추가 — WaitingRoom 씬(에디터에서 SerializedObject로 수정·저장, YAML 1줄)과 FinalLayoutBuilder.Route(재생성 시에도 유지). 톱날 다중 절단 동작은 그대로. 플레이 확인 전.
- 저장(2026-11-16): dev에 같은 번호 Task-20261116-134(escape-wall-backs)가 있어 이 Task를 136으로 바꿈. 비공개 저장소 main 4111377d(전시 인형 옷 OldCostumes, 경고 줄무늬 문 FBX Read/Write). 공개 저장소에는 이번 작업 파일과 서브모듈 포인터만 커밋 — Unity가 다시 저장한 폰트·머티리얼 13개, Task-107 log·guides, _ThirdParty 안 기존 셰이더·폰트 변경은 제외. origin/dev 새 커밋 8개는 이번 파일과 겹치지 않으며, Unity 에디터가 열려 있어 아직 반영하지 않음(PR 전 반영 필요).
- dev 반영(2026-11-16): origin/dev 28개 커밋 병합 6424dfe8, 충돌 없음(WaitingRoom.unity 자동 병합, 중복 fileID 0). 병합 후 컴파일·플레이 확인 미실행.

- 2026-11-16 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/397 (fix/waiting-room-bugs-20261116 → dev)
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/397 (dev 대상, 지정 팀원 검토 대기)
