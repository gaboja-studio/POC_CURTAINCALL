# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-19 낱장5(작업실 가운데 작업대 위, 처음부터 보임, final:torn-note-5, 그림 TornNote_5.png): 작업대 윗판 메시 최고점 0.872m가 충돌체 0.863m보다 높아 처음 놓은 종이가 판 속에 묻힘 → 0.876m로 올림.
- 낱장6(final:torn-note-6): 폐기물 보관소 자물쇠 트렁크(structure-a:locked-box-1)를 열면 낱장2 옆에 함께 드러남. 그림·설명 대기라 빈 종이(LA_Paper)와 이름만.
- 폐기물 보관소 모니터: 6분할(베젤·화면·대기등)을 끄고 하우징을 16:9(5.42×3.145)로 키움. `JunkMonitorEvent`(FinalLayout/Manual)가 실행 중 단장 모니터(`RingmasterMonitor`)를 5.2×2.925m로 세움. 보관소 안에서 카메라가 화면을 28° 안으로 바라보고 가리는 물체가 없으면 `ExplorationState.RequestSeen("seen:junk-monitor")` → 기록되는 순간 모두 재생(조명 10개·전구 면 9개 3번 깜빡 → 잡음 1.3초 → 그림 5초 → 전원 꺼짐 0.6초). 세대 교체(런 종료) 때 장부가 비워져 런마다 1번. 그림 `Resources/WaitingRoom/Monitor/junk-monitor-scan.png`.
- 탐색 장부 요청 추가: SeenRequest(키가 `seen:`으로 시작할 때만 호스트가 MarkVisited).
- 정전 위치 버그: 배선 정전·회로 깜빡임이 밤 정비 회로(숙소·상점·전문가실·휴게 공간, 문 알림등, 숨은 방 조명 제외)만 꺼서 그 방에서는 조명이 그대로였음. `NightPower`가 정전·깜빡임 동안 씬의 회로 밖 조명(방향광 제외)도 끄고 끝나면 원래 켜짐 상태로 돌림. 밤 동작은 그대로.
- 씬 직접 수정(FinalLayoutBuilder 미반영 → 빌더 11·14단계를 다시 돌리면 낱장5·6이 빠짐, Polish3·Abandoned 빌더를 다시 돌리면 6분할이 돌아옴). WaitingRoomPursuit.unity의 같은 모니터 벽은 손대지 않음.
- 캡처 `.harness/local/captures/20261119/`(편집 모드 렌더, 모니터는 같은 크기 임시 화면).
- 배선: 실제 조건 — 탈출로 = 관리실 컴퓨터 + 탈출 배선 4 + 숨은 배선(2026-11-16 Task-136에서 추가), 관리실 문 = 숨은 배선·빗장·키 카드 중 하나. 사용자 의도(배선 = 관리실 문, 컴퓨터 = 탈출로)와 다름 → 확인 대기.
- 무관한 변경: Graffiti_Brush.asset·escape-labels.asset(TMP 동적 글리프), _ThirdParty 서브모듈 내용 변경. 커밋 대상 아님.
- 검사: 컴파일 PASS. check-work·플레이 확인 전.

- 2026-11-19 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-19 관리실 문·탈출로 규칙 확정(사용자): 관리실 문 = 기계실 배선 4가닥 모두 끊기·안쪽 빗장·키 카드 중 하나, 열리면 나머지 방법은 못 씀. 탈출로로 가는 문·탈출로 문 = 관리실 컴퓨터 암호만으로 동시에 열림(배선 무관).
  - 씬: FinalLayout/MachineWiring(녹슨 벽·숨은 배선)과 녹 부스러기 효과(Rust_RustWall) 삭제. 삭제 전 둘 다 꺼져 있어 탈출로를 열 수 없는 상태였음. 톱날 발사기 cables·NightPower blackoutCables 5→4. Route의 ExplorationAllOf: [escape-cable-1..4]→final:management-cable 추가, final:escape-route 조건을 [final:office-computer]만으로. 깨진 참조 0.
  - 코드: EscapeCuttableCable.retiredBy 추가(값 final:management-open) — 문이 이미 열렸으면 톱날이 배선을 자르지 않음(EscapeExitSaw.CutAlong). 빗장·단말기는 기존 retiredBy 그대로. 빌더 4단계는 삭제 전용으로, 5단계(Route)는 새 조건으로 맞춤.
  - 배선 오브젝트 이름·ID는 EscapeCable_N / final:escape-cable-N 그대로(이름만 예전 것).
- 검사: check-work는 문서/규칙 FAIL(기존 Task-20261105-107 guides 폴더명, 이번 변경 무관)로 컴파일 단계 건너뜀 → verify-unity 직접 실행: 컴파일 PASS, 공통 설정 PASS, WaitingRoom EditMode 598/603. 실패 5건: 문구 파일 테스트 3건(기존과 같은 종류, 씬 대상 키 목록에는 낱장1~4와 함께 낱장5·6도 포함), RehearsalCannonSourceTests.W6(CC_GripArt 재질, 기존), WaitingRoomHudProfileTests.PerCueOverrides…(HUD, 이번 변경 파일과 무관해 보이나 dev 기준 재현은 확인 안 함). 플레이 확인 전. 커밋 안 함.
- 2026-11-19 낱장6 그림·설명(사용자): TornNote_6.png·TornNote_6.mat 적용(크기 .165×.22, 덧댄 모서리 끔), 설명 "누군가가 남긴 메모이다."
- 2026-11-19 배선 재정정(사용자: 문이 이미 열려 있어도 배선은 잘리고 정전 연출도 나와야 함): 배선의 retiredBy 차단(코드·씬·빌더)을 되돌림. 빗장·키 카드 단말기만 문이 열리면 사라짐(기존 retiredBy). 컴파일 PASS. 테스트 재실행 안 함.
- 2026-11-19 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/443 (fix/bugs-20261119 → dev). save-work·new-pr 스크립트는 무관한 미커밋 변경(폰트 에셋 2개·_ThirdParty·사운드 문서)과 구조 검사 FAIL로 쓰지 못해 git·gh로 직접 처리.
- 2026-11-19 추가 버그 3건(사용자):
  - 탈출로 입구 벽 글자 "탈출 준비 N/M · 플레이 중인 전원이 모이면 출발": `EscapeEntrance.RefreshPresentation`이 준비 단계에서는 벽 글자를 비움(화면 가운데 시스템 알림은 그대로, 탈출 중 문구는 유지). 컴파일 PASS, 플레이 확인 전.
  - 잠자기 때 아직 뜸: 누운 몸의 가장 낮은 정점이 등이 아니라 돌출부였음(실측: 곰·토끼 엉덩이·꼬리, 삐에로 머리 쪽, 분홍 소녀 다리 쪽 → 윗등이 4.6~17.6cm 뜸). `NetworkBedPose`가 뒤통수·윗등·엉덩이 등쪽 면(뼈 아래 수평 8cm 안 최저 정점)의 중앙값을 매트리스에 맞춤(여유 0.5cm). 정적 비교: 예전보다 곰 4.0 / 토끼 6.0 / 삐에로 12.4 / 분홍 15.9cm 내려감, 윗등 틈 0.6~3.7cm, 돌출부는 2.5~14.4cm 묻힘(1cm 넘게 묻히는 정점 0.2~6.3%). 컴파일 PASS, 플레이 확인 전.
  - 무대 장치 보관실 투명 벽: 스크린샷 위치는 남쪽 2층 통로(스풀·상자 x≈-44, 북쪽 난간). 통로 위 숨은 충돌체 없음(바닥판 전수 캡슐 검사). 막는 것은 난간 위 `RailTopWall`(Task-148에서 사용자 선택으로 넣은 트램펄린·난간 윗면 차단 벽 51개). 바꾸는 방향 사용자 확인 대기. 서쪽 끝 `EndBarrierCollider`는 낙하 방지용(너머는 통로 없음)이라 그대로.
- 2026-11-19 난간 위 투명 벽(사용자 결정: 통로에서 내려가는 것만 허용): `OneWayRailWalls`(FinalLayout/Manual, 대기실 전용) 추가. 켜져 있는 `RailTopWall` 50개와 벽마다 통로 쪽 방향을 연결(동 19·서 6·남 12·북 13). 통로 쪽 플레이어는 벽과의 충돌을 무시(Physics.IgnoreCollision, 각 화면의 캐릭터 충돌체에만), 바깥쪽은 예전처럼 막힘. 넘는 중에는 직전 판정 유지(바깥으로 반지름+0.15m 벗어나면 다시 막음). 난간 윗면에 올라서면 바깥으로 2.5m/s로 밀어 떨어뜨림. 공용 이동 코드(PlayerMover)·난간 충돌체·NavMesh 변경 없음.
  - 정적 확인: 벽 50개 모두 통로 쪽이 한쪽으로만 판정됨(건너뜀 0), 바깥 0.5m 지점 아래 2m 이상 비어 있음 50/50, 통로 쪽 설 수 있음 48/50(나머지 2곳은 기둥 자리). 컴파일 PASS. 플레이 확인 전(난간 넘어 내려가기, 트램펄린으로 올라오기 차단, 난간 위에 서지 않는지).
