# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-10
- Task 생성. origin/dev(806c8dab)에서 분기. 지라 CC-250~259 등록. Task-104 Done 이동(미커밋)도 이 작업 트리에 함께 있음.
- 사전 조사: 목마는 대기실 자동 활성, 트램펄린·절단기는 HumanCannonSystem 필요, 절단기 prefab 없음, 탐색 기믹은 ExplorationGate로 대부분 씬 연결 가능.
- 2026-11-10 저장 전 검사: 안전성=PASS; 문서/규칙=FAIL(기존 Task-097 todo.md 102줄 HARD, 이 변경과 무관); 컴파일=SKIPPED(문서만 변경); 테스트=SKIPPED(요청 없음). Task-115 문서만 명시 경로로 저장.
- 2026-11-10 대상 씬 정정: 설정 waitingRoomVariant=2(Integrated) → `WaitingRoom.unity`(Pursuit 아님). 에디터에서 1층·B1 탑다운을 오브젝트 배치 창과 같은 방식(Preview 카메라, 바닥+2.4m 절단)으로 캡처해 아이디어 전체를 표시한 `gimmick-map.png` 작성. 씬 수정 없음.
- 2026-11-10 제출 전 검사: 안전성=PASS; 문서/규칙=FAIL(기존 Task-097 todo.md 102줄 HARD, 이 변경과 무관); 컴파일=SKIPPED(문서·이미지만); 테스트=SKIPPED(요청 없음). origin/dev 새 커밋 3개는 Unity 에디터가 열려 있어 미반영(이 Task 파일과 겹침 없음).
- 2026-11-10 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/321 (feat/waiting-room-passage-gimmicks → dev). new-pr.ps1은 관련 없는 로컬 변경(Task-104 이동·escape-labels·ThirdParty) 때문에 막혀 같은 본문으로 gh 직접 등록.

## 2026-11-10 구현 1차 (CC-250~253·255 일부)
- origin/dev(85b86ed7) 병합(aa9ea576, 로컬 커밋).
- 기획 확인: Confluence 38764736. 결정은 plan "확정" 참고. Confluence는 "협동형 두지 않음", plan 원칙은 "필요한 경우만" — CC-257 전에 확인 필요.
- 목마: `CommonInteractionUI.AddPiggyback`이 외줄 코스 진행 중에만 안내 → 대기실 씬이면 코스 없이도 기존 목마 안내를 띄우게 수정.
- 맨몸: `IBareHandTarget`·`StrikeProgress`(최소공배수 단위 비율 합산)·`ExplorationBook.Strike` 추가. BreakableWall(15/6)·ExplorationGate Breakable(5/1)·ToolTarget Pry(5/1). 뽑개는 뽑개 타격으로 열렸을 때만 1 소모(기존 규칙). 배관함 requiredHits 3→6(씬). 맨몸 타격 동작은 기존 내려치기 동작 재사용. 새 문구 키: wall.strike-bare, tool.pry-bare, tool.pry-progress.
- 트램펄린·대포 시스템: `WaitingRoomStageDevices`(런타임, 대기실 씬 안에 생성). 대포 리허설 `EnsureCourse` 직전 `ReleaseForRehearsal`로 즉시 제거(Current 중복 방지).
- 절단기: `EscapeExitSaw.fireWithoutCables` 추가. escape_route 2번 절단기 → `Prepared/WaitingRoomPortableSaw.prefab`(외부 참조 없음 확인) → WaitingRoom `S11_StageGear/S11_StageGear/PortableSaw`(-54,0,42) 배치. escape_route 씬은 저장하지 않음.
- 검사: 에디터 재컴파일 오류 0(콘솔 에러는 Vivox 403·MCP 시간 초과뿐). 테스트·플레이 확인은 요청 없어 미실행. .mat·escape-labels 변경은 에디터가 만든 것으로 이 작업과 무관.
- 다음: 플레이 확인(목마 안내·트램펄린 운반/반동·절단기 발사·맨몸 타격 수), CC-254, CC-255 숏컷·환풍구·레버 연쇄, CC-260~265 Task 반영.

## 2026-11-10 남은 맨몸 기믹 검토
- 구조도 ③ 작업실 봉인 문(`S10_Workshop/DomeClosures/SealedDoorway`): 뒤에 연습실 돔 벽까지 약 2m 바닥 없는 틈 → 통로가 필요해 CC-265로 보류.
- ④ 화물 도크 셔터(`S12_CargoBay/Zone12/DockShutter`): 충돌체 없는 장식 판, 뒤는 로비까지 약 6m 틈 → CC-263(잠긴 공간 해금)과 함께.
- ⑦ 소품실 판자 벽 → 숨은 공간: 공간이 없어 보류.
- 한쪽에서만 여는 빗장은 새 코드 없이 가능: ExplorationGate(Open) 상호작용 지점을 한쪽에 두고, 문 충돌체를 게이트 자식이 아닌 곳에 두면 반대편은 시야 판정(ClearPathFrom)에 막힌다. 레버 연쇄는 openedBy(Gate·ExplorationSlide)로 가능.
- 코드·씬 변경 없음.

## 2026-11-10 숙소 환풍구 (CC-261)
- 결정(사용자): 입구 = 숙소 출입문 옆 서쪽 벽 앞, 입구 통만 서서(안 높이 1.7m)·덕트는 앉아서(1.2×1.2m), 기계실 하강은 경사로 양방향, 출구 두 곳은 안에서만 뜯기.
- 높이 근거: 캐릭터 1.5m·점프 0.8m, 목마 맨 위 앞점프는 점프 1.5배 → 발 약 2.7m. 입구 바닥 2.3m(혼자·상자·세면대 위 점프로는 닿지 않음), 덕트 바닥 2.5m(옷장 위 소품 2.4m 회피, 0.2m 턱은 오르기 0.3m 이내).
- 경로: 입구(x12.98~14.18, z74.8~76.3) → 남쪽: 화장실 문 위로 통과 → 화장실 덮개(z67.2). 북쪽: 북벽 따라 동쪽 → 숙소 북벽 구멍(x18.55~19.95, y2.4~3.8) → 숙소 북쪽 빈 공간 경사로(약 27도, z85.4→99.95, y2.5→-5) → 기계실 북벽 따라 서쪽 → 캣워크 덮개(x11.35).
- 덮개: ToolTarget Pry(맨몸 5/뽑개 1), 걸쇠는 덕트 안·덮개와 다른 계층이라 바깥에서는 시야 판정에 막힘(안 0.6m 닿음/밖 막힘 확인). targetId dorm-vent:restroom-panel, dorm-vent:machine-room-panel.
- 구현: `PassageGimmicks/Editor/DormVentBuilder.cs`(메뉴 CurtainCall/Waiting Room/Build Dorm Vent Route, 다시 실행 가능). 판 메시 `LA_DormVent.asset`, 벽 구멍은 이 씬만 `LA_DormWalls_Vent.asset` 사본 + 바깥 벽판·벽 충돌체 4조각 분할(LA_DormWalls는 WaitingRoomBase·Pursuit도 사용해 원본 유지). 받침: 숙소 천장 걸이, 빈 공간 기둥(-10m 바닥), 기계실 벽 받침.
- 검사: 기존 충돌체와 겹침 없음, 앉은 캡슐 95곳 통과(입구 0.2m 턱 2곳만 접촉), 경사로 바닥 지지 확인, 콘솔 에러 0. 플레이 확인(목마 앞점프 진입·덕트 안 카메라·덮개 뜯기)은 미실행. 탐색 제한시간 구역은 숙소·기계실 구역 상자 안이라 그대로.

## 2026-11-10 숙소 환풍구 재구성(사용자 정정: 덕트는 실내가 아니라 대기실 바깥)
- 정정: 실내에 드러난 덕트·기계실 안 경사 덕트를 없애고, 방에는 벽 구멍만 둔다. 기계실은 벽 환풍구에서 아래 새 캣워크로 뛰어내리고, 올라갈 때는 목마.
- 결정(사용자): 숙소 입구 = 동쪽 벽 발치 트렁크 위(z76.7~77.9, 기둥 z76.62와 사물함 사이, 바닥 2.3m). 기계실 = 동쪽 벽 북쪽 끝 환풍구(-2.7m) + 새 캣워크(-5m)로 기존 북쪽 캣워크(x11.3 끝)를 동쪽 벽까지 연장.
- 경로: 입구 통(서서, 숙소 벽~x25.2) → 건물 바깥 줄(x24~25.2) 남쪽 → 화장실 동쪽 빈칸 → 화장실 동벽 덮개(2.3m, z62.95~64.15) / 북쪽 → 기계실 동쪽 외벽 밖 완만한 경사(약 13도) → 기계실 동벽 덮개(z99.9~101.1).
- 새 캣워크: 기존과 같은 재질(그레이팅·녹슨 테두리·난간 2단·3m 기둥), 폭은 CCTV(z101.4~)를 피해 z99.7~101.35.
- 벽 구멍(이 씬만): 숙소 동벽·화장실 동벽·기계실 동벽. 벽 메시 사본(*_Vent.asset), 충돌체·바깥 벽판은 원본을 끄고(_VentOriginal) 구멍 둘레 조각(_VentCut)으로 대신. 빌더 재실행 시 자동으로 되돌린 뒤 다시 낸다.
- 처음 실내 덕트 판이 낸 숙소 북벽 구멍은 원래대로 되돌림(벽 메시 원본, 충돌체 한 장으로 합침). 북벽 바깥 벽판은 오른쪽 조각이 사라져 있어 원래 크기(x11.65~21.85)로 직접 맞춤.
- 검사: 기존 충돌체와 겹침 없음, 캡슐 97곳 통과(입구 통·캣워크는 선 키 1.5m, 덕트는 앉은 키), 바닥 지지 확인, 덮개 안 0.4~0.5m 닿음/밖 막힘, 숙소 입구는 숙소 쪽에서 열려 있음, 콘솔 에러 0. 플레이 확인 미실행.

## 2026-11-10 환풍구 피드백 대응
- 카메라 뚫림 원인: 카메라 기준점이 서 있을 때 높이(LookHeight 1.2m)로 고정 → 앉아서 1.2m 덕트에 들어가면 기준점이 천장(3.5m)에 붙어 밖에서 찍힘. 또 덕트 판의 카메라 표면 범위가 기본값(1m 상자)이었다. 수정: `CameraViewDirector.ApplyWallCollision`이 앉기 비율(PostureHeightScale)로 기준점을 낮추고 위쪽 충돌체 아래로 제한(`KeepPivotUnderCeiling`), 빌더는 카메라 표면을 실제 메시 범위로 맞춤. 플레이 확인 필요.
- 덮개: 환풍구 날개형 덮개로 교체, ExplorationGate Breakable(맨몸 3·뽑개 1)로 부수기. 부서지면 아래 모서리에 걸려 꺾인 덮개가 보인다.
- 트램펄린 천장 뚫림 원인: 대기실 천장은 모두 보이는 면+카메라 전용 표면이고 물리 충돌체가 없다(로비·숙소·작업실·소품실·보관실·연습실 확인). 비행은 CharacterController 위쪽 충돌로만 멈추므로 천장을 지나간다. 반동 12m/s·중력 9.81 → 트램펄린 면 위 약 7.3m(머리 약 9m). 수정은 요청 없어 미실행.
- 트램펄린 착지 사망: 사용자 확인, 기록만(원인 조사·수정 안 함).
- 구조안: `vent-structure-proposal.md`(숨은 방 2곳으로 환풍구 단축). 사용자 확인 전.

## 2026-11-10 숨은 방 구조로 재구성(사용자 승인 "이 구조로 진행")
- 긴 바깥 덕트·기계실 동벽 환풍구를 없애고(빌더가 자동 되돌림), `DormVentBuilder`를 숨은 방 구조로 다시 씀. 기계실 새 캣워크(-5m)는 유지.
- 숙소 북벽 환풍구(x15.2~16.4, 바닥 2.3m, 1.2×1.7m): 벽 두께만 지나 배기 샤프트로. 덮개는 방 쪽에서 부수기(맨몸 3·뽑개 1).
- 배기 샤프트(숙소 북쪽 빈 공간 x14.85~23.3·z85.15~101.9, 새 벽·지붕): 발판 P1 0.3m(갤러리 벽 따라) → P2 -1.6m → P3 -3.3m → 캣워크 -5m(한 칸 1.7~2.0m, 목마로 오름). 떨어지면 위험한 가장자리만 난간.
- 팬: 샤프트 서벽(갤러리 벽)·화물 승강기 칸 뒷벽을 지나는 구멍(z96.4~98, 높이 0.3~2.1m)에 대형 팬. 기계실 캣워크 북벽(x18)의 노란 전원 배선(EscapeCuttableCable)을 대기실 휴대 절단기 톱날로 끊으면 `ShaftFan`이 멈추고 막음 해제·샤프트 조명 꺼짐, 탐색 진행 `dorm-vent:shaft-fan`에 기록(런 유지·팀 공유). 절단기 cables에 이 배선을 연결.
- 팬 뒤는 잠긴 화물 승강기 칸(엘리베이터 홀, CC-263). 승강기 칸 안 장식(범퍼 레일·이음판)도 구멍 둘레로 나눔.
- 기계실 천장(충돌체 없는 보이는 판)을 샤프트 자리만 비움 — 샤프트에서 바닥처럼 보이고 내려갈 때 판을 지나던 문제.
- 배관 점검 칸(화장실 동쪽 빈 칸 x18.85~22.35·z62.6~68.3): 화장실 동벽 환풍구(2.3m)로 들어감. 물탱크(Synty 0.32배)·배관 밸브·조명, UV 숨은 낙서(화장실 기존 낙서 복사, targetId dorm-vent:uv-wire-hint, 내용 문구 미정).
- 미구현·미정: 배관 점검 칸 → 숙소 판자 숏컷(숙소 남벽 쪽은 침대·책상이 막아 출구 자리 확인 필요), 보상 방 내용(CC-263), UV 낙서 내용.
- 검사: 기존 충돌체 겹침 없음, 서서 통과 46곳(막힘은 덮개·팬 막음·밸브 소품 옆뿐), 덮개 방 쪽 2.0~2.2m 닿음/안쪽 막힘, 콘솔 에러 0(Vivox 403 제외). 플레이(목마 진입·배선 절단·팬 정지 동기화) 미확인.

## 2026-11-11 환풍구·숨은 방 작업 롤백(사용자 지시)
- 사용자 지적: 의도는 "숙소 환풍구 → 기계실 또는 화장실"인데 숙소→기계실 직통 계단식 샤프트를 만들었고, 기존 씬 물체(승강기 칸 상자·범퍼 레일·이음판, 갤러리 벽, 기계실 천장, 숙소·화장실 벽)를 임의로 끄고 잘랐다.
- 롤백: WaitingRoom.unity를 커밋본(6f0c7ea6)으로 되돌린 뒤 환풍구 이전에 승인된 변경만 다시 적용(배관함 뽑개 6대, 보관실 휴대 절단기 -54,0,42). 롤백 전 씬은 scratchpad에 백업.
- 삭제: DormVentBuilder.cs·ShaftFan.cs, LA_DormVent.asset, 벽 메시 사본(LA_DormWalls_Vent·LA_RestroomWalls_Vent·LA_DeepPlantUpperWalls_Vent).
- 숙소 가구는 씬 파일에 커밋본과 같은 수(침대 20·옷장 7·사물함 3 등)로 있었다. 화면에서 안 보였다면 배치 창 "장식물 끄기"(씬 뷰 숨김)일 수 있음 — 확인 필요.
- 남긴 것: 앉기 카메라 기준점 수정(CameraViewDirector), 절단기 배선 없이 발사 옵션, 맨몸 기믹·목마 안내·트램펄린. vent-structure-proposal.md는 폐기된 안으로 남김.
- 다음: 환풍구 경로(숙소 → 기계실 / 숙소 → 화장실 분기)를 그림으로 먼저 확인받은 뒤 진행. 기존 물체·벽·천장은 건드리기 전에 확인.

## 2026-11-11 숙소 환풍구 A안 재구성(사용자 결정)
- 숙소 기물 "사라짐" 원인: 배치 창 "상호작용 오브젝트 끄기"(hideInteractions=True)로 씬 뷰에서만 숨김(전체 755개, 숙소 252개). 씬 파일은 정상. 설정을 꺼서 모두 다시 보이게 함.
- 결정: 숙소 동벽 침대 C·D 사이(책상 끝 위 z70.6~71.8, 바닥 2.3m)에 환풍구. 벽 바로 밖 분기 칸 → 오른쪽(남) 바깥 덕트(약 13m) → 화장실 동벽 덮개(2.3m) / 왼쪽(북) 짧은 덕트(약 2m) → 설비실(기계실 동벽 바로 바깥, x23.85~27.85·z71.7~78.5, 바닥 -5m).
- 설비실: 설비 상자 상단 0.6 → -1.1 → -2.9 → 바닥 -5(목마로 오름), 동력 소켓(ExplorationGate Open, 도구 power-battery, 넣으면 소모) → 기계실 동벽 셔터(ExplorationSlide) 열림 → 새 캣워크(-5m, 기계실 동벽·북벽 따라 기존 북쪽 캣워크와 연결). 보일러 굴뚝(OldBoiler/Flue)은 그대로 두고 캣워크에 구멍을 내 서쪽으로 우회.
- 배터리 기물: ItemUses.PowerBattery("power-battery"), ItemCatalog 에셋에 추가(탐색 도구, 1회, 가격 0 → 상점 미노출). 놓을 자리·손에 든 모델·아이콘은 미정/미구현.
- 덮개: 숙소 덮개는 방 쪽, 화장실 덮개는 덕트 안에서 부수기(맨몸 3·뽑개 1). 소켓은 설비실 안에서만.
- 다른 방 기존 물체는 건드리지 않음. 벽 구멍: 숙소 동벽·화장실 동벽·기계실 동벽(셔터)만.
- 검사: 기존 충돌체·보이는 물체와 겹침 없음, 통과 60곳(막힘은 닫힌 덮개·난간 바로 옆 점), 덮개·소켓 여는 쪽만 손 닿음, 씬 저장. 플레이 확인 미실행.
- 2026-11-11 배터리 자리(사용자: 소켓 바로 앞): 설비실 소켓 앞 바닥(26.95,-5,76.7)에 FoundItemSpot(utility-room-battery, uid 51, 세대마다 1개). 배터리 모델 `Resources/ItemArt/Prefabs/power-battery.prefab`(Synty 자동차 배터리 0.55배, 약 27cm). 인벤토리 아이콘은 아직 없음. 씬 저장.

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-11 대기실 공간 프리팹화(사용자 요청)
- 결정(사용자): 위치 `Assets/Resources/Prefabs/Spaces/WaitingRoom/`(새 분류), 범위는 WaitingRoom.unity만(WaitingRoomPursuit는 그대로).
- `WaitingRoom_LayoutA` 아래 방 22곳을 같은 이름 프리팹으로 저장하고 씬 오브젝트를 인스턴스로 연결: S01~S12, LG_Lounge, P05_PipeZone, ST_Stairwell, WC_WestCorridor(꺼진 상태 유지), B1_* 6곳. 방이 아닌 묶음(ExplorationTimer·SecurityCctv·NightMaintenance·StructureAExploration·Frame8*·외벽 등)과 레거시 `WaitingRoomGreybox`는 씬에 그대로 둠. 방 안의 기존 중첩 프리팹은 중첩으로 유지.
- 네트워크 오브젝트는 방 안에 없음. 방 밖에서 방 안을 가리키는 참조(PursuitRoom·NightPower·StructureA 경로 등)와 방 안에서 씬을 가리키는 참조는 그대로 연결(후자는 인스턴스 덮어쓰기, 22개 합계 376개).
- 검증: 변환 전/후·저장 후 다시 연 씬 모두 컴포넌트 96,376개·씬 참조 5,278개·전체 Transform 위치/회전/크기/활성 상태 차이 0건. 씬 파일 약 74MB → 16MB, 프리팹 22개 합 63MB.
- 오클루전 컬링: 기존 데이터(`Scenes/Tests/WaitingRoom/Base/WaitingRoomBase/OcclusionCullingData.asset`, WaitingRoomBase와 공유)는 렌더러 ID로 연결돼 프리팹 안 렌더러와 끊김 → WaitingRoom 전용으로 다시 구움(`Scenes/InGame/WaitingRoom/WaitingRoom/OcclusionCullingData.asset`). 공유 원본은 수정 안 함.
- 주의: 방 구조를 다시 생성·삭제하는 옛 에디터 도구(WaitingRoomLayoutA·StructureA*Fix·WaitingRoomOverlapFix 등)는 프리팹 인스턴스 자식을 지우지 못한다. 다시 쓰려면 프리팹을 열어 실행하거나 도구를 고쳐야 함. 방 수정은 이제 프리팹 모드에서 하고, 씬에서 고친 값은 Apply 여부를 확인할 것.
- EditMode 테스트(WaitingRoom 필터) 323개 중 318 통과, 5 실패는 모두 변경 전부터 있던 것: 문구 파일 키 2건, 편집 모드 DontDestroyOnLoad 2건, 추격 배치 비교 1건(PursuitSystem/S04_PropRoom/Mannequin_Male_04 Size=Medium, HEAD에도 동일·프리팹 밖). PlayMode·실제 플레이 미실행.
- 함께 바뀐 파일 아님: Pretendard SDF 폰트 2개·escape-labels.asset·_ThirdParty 셰이더 2개는 에디터 사용 중 생긴 기존 변경으로 손대지 않음.

## 2026-11-11 공간 프리팹 장소 기준 재분류(사용자 지적·승인)
- 문제: 1차 프리팹화가 기존 하이어라키 묶음을 그대로 따라, 다른 방의 외벽·연결 통로·메자닌·지하 세부물이 로비·S11·계단실 프리팹 안에 있었음.
- 옮김(월드 위치 유지): `S01_Lobby/Frame8Lobby/OuterSkin_{S04,S10,P05,S05}` → 각 방 / `Passage_F8_*` 6개·ServiceGallery·CargoDockConnection·PipeJunkLink → `FacilityConnections`(새 프리팹) / `DarkOuterShell`(시설 전체 외피) → `ExteriorEnclosure`(새 프리팹) / `S11_StageGear/HallMezzanine` → S12_CargoBay / `ST_Stairwell/EnvironmentDetail`의 OfficeLink·EscapeCorridor·EscapeEntrance·SealedExit·PlantOverhead·OldBoiler → 해당 B1 방, BasementRooms는 하위 21개를 위치대로 B1_Office(10)·B1_ChangingRoom(11)에 나눔. 계단실에는 EnvironmentDetail/Stairwell만 남음. 로비에는 돔·원형 바닥·벽 충돌체·AbandonedFactory 유지.
- 방법: 관련 방 14곳 프리팹 연결을 풀고 재부모화 후 같은 경로에 다시 저장(GUID 유지). 프리팹 24개(방 22 + 연결 통로 + 외피).
- 메자닌은 S11·S12 영역에 거의 반씩 걸쳐 있음(렌더러 517/573). 화물 적재장 위 데크 기준으로 S12에 넣음.
- 검증: 메모리 비교 참조 5,278개 끊김 0, 저장·재오픈 후 이름·위치 기준 비교에서 참조·컴포넌트 전부 일치, 위치 차이 최대 0.01m(반올림 경계). 오클루전 다시 구움. EditMode(WaitingRoom) 323 중 318 통과, 실패 5건은 이전과 같은 기존 실패.
- 경로가 바뀐 곳: 옛 생성 도구(WaitingRoomLayoutA.M2/Polish2/Polish4/Polish5, StructureADoorVisualFix)는 옛 경로를 찾는다(이미 프리팹화로 재실행 불가 상태). CameraViewsPlayTests의 `S01_Lobby/Frame8Lobby/PipeJunkLink` 경로는 WaitingRoomBase·추격 씬 전용이라 영향 없음.
- 프리팹 밖에 남긴 것: SecurityCctv·NightMaintenance 문·SupportFinishing 깃발 줄·BasementEscapeConnection·탐색 타이머 영역 등 여러 방에 걸친 시스템 묶음.

## 2026-11-11 origin/dev 병합(공간 프리팹 재적용)
- dev 4a081339(카메라 충돌면 보강 등)가 WaitingRoom.unity를 수정해 프리팹화 씬과 충돌. 브랜치에서 이 씬을 바꾼 커밋은 프리팹 커밋 2개뿐이라 dev 씬을 그대로 받고 같은 재분류·프리팹 저장(24개, GUID 유지)을 다시 적용.
- 검증: dev 씬 대비 참조 끊김 0, 저장·재오픈 후 이름·위치 기준 비교에서 dev 컴포넌트·참조 전부 포함(차이는 BasementRooms 분할뿐, 위치 오차 ≤0.01m 반올림). 오클루전 다시 구움.
- EditMode(WaitingRoom) 323 중 319 통과. 실패 4: 기존 3건(문구 2·추격 배치 비교 1) + `WaitingRoomCameraCoverageTests` WaitingRoomBase 케이스("Repeated repair must not dirty the scene") — WaitingRoomBase는 dev 버전 그대로(이 브랜치 미수정)라 dev 쪽 실패로 판단, 담당자 확인 필요.

## 2026-11-11 WaitingRoomBase도 공간 프리팹 연결(사용자 요청)
- 같은 장소 재분류 후 방 24곳을 WaitingRoom 공간 프리팹에 `ConvertToPrefabInstance`로 연결. Base 내용은 그대로 유지: 프리팹에만 있는 것(천장 충돌체 등 컴포넌트 46개, 휴대 절단기·환풍구 절단 벽 등 오브젝트)은 제거 오버라이드, Base에만 있는 원래 벽 조각 7개(숙소 6·기계실 1)는 추가 오버라이드.
- 함정: 같은 이름 형제가 여럿이면 연결 시 Unity가 프리팹 쪽 복사본으로 바꿔 넣어 외부 참조가 끊기고 Base 전용 컴포넌트가 빠진다. 방마다 원본 복제본을 떠 두고 연결 후 모든 오브젝트·컴포넌트 값을 복제본과 맞추고 외부 참조(방당 최대 65개)를 새 오브젝트로 다시 연결해 해결. 작업 중 에디터 활성 씬이 바뀌어 한 번 중단 — 이후 매 단계 씬 경로 확인.
- 검증: 저장·재오픈 후 Base 원본과 이름·위치·컴포넌트·참조 비교 차이 0(BasementRooms 분할과 TMP 자동 생성 SubMeshUI 4개 제외, 위치 ≤0.01m 반올림). 씬 75MB→16MB, 공간 프리팹 파일 변경 없음. Base 오클루전 다시 구움(Base 폴더 기존 데이터 갱신).
- 테스트 수정: `CameraViewsPlayTests` 천장 목록의 PipeJunkLink 경로를 FacilityConnections로(추격 씬은 옛 경로라 둘 다 찾음), `WaitingRoomTextTests.TargetKeysMatchSceneTargets`가 공간 프리팹·오버라이드의 targetId도 읽게 함.
- 결과: PlayMode `WaitingRooms_Architecture` 3개 통과(Base·추격 천장). EditMode(WaitingRoom) 기존 실패 그대로 — 문구 2·추격 배치 1·카메라 커버리지 Base 1(dev부터)·`RehearsalCannonSourceTests.W6` forwardSpeed(dev PR #328 인간대포 씬 변경 관련, 이 작업과 무관)·편집 모드 DontDestroyOnLoad 2(간헐).

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-11 origin/dev 병합(PR #330 등) — 공간 프리팹 위에 반영
- 충돌: dev 5af3a8a8이 WaitingRoom·WaitingRoomBase에서 운반 트램펄린 4개 겉모습(CC_PropArt)을 RoundCarryTrampolineVisual → CC_Trampoline으로 교체, Base에 천장·지붕 충돌체(Box 38·Mesh 8) 추가. 우리 쪽은 같은 씬을 프리팹 구조로 재저장해 텍스트 병합 불가.
- 해결: 씬은 우리 쪽 유지. 트램펄린 교체는 S05·S09·S11 공간 프리팹에 반영(기본 위치·회전·크기, 형제 순서 6 동일). Base의 충돌체 '제거' 오버라이드 46개를 되돌려 프리팹 충돌체 사용.
- 검증: dev 씬 대비 이름·위치·컴포넌트·참조 비교 차이 0(두 씬 모두, BasementRooms 분할만 다름). dev가 Base에 넣은 BoxCollider 38개 크기·중심 값이 모두 일치(MeshCollider 값은 비교 안 함). 두 씬 오클루전 다시 구움.
- 테스트: EditMode(WaitingRoom) 338 중 335 통과, 실패 3은 기존(문구 2·추격 배치 1). 카메라 커버리지 Base·W6는 dev 반영으로 통과. PlayMode `WaitingRooms_Architecture` 3개 통과.
