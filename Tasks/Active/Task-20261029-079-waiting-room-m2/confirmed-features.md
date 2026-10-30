# 확정 기능 구현안 적용 (2026-10-30)

사용자 확정안: 타이머 구역 규칙, UV 장비형 손전등, 거울, 연습실 조명·모니터, 배관함 철창, 로비 UV 교체, 배치 오브젝트 외형. 추격자는 미구현 그대로.

## 결정(사용자 답변)
- UV 조작: 장비를 든 동안 우클릭 유지로 켜짐, 놓으면 꺼짐. 키맵 재설정은 키보드 키만 허용하는 기존 구조라 마우스 버튼 재설정은 미반영(후속).
- 외형 범위: 탐색 배치물(31개). 거울: 화장실 세면대 2·탈의 방 1·소품실 전신 3.
- 미지정 구역: 무대 통로·소품실·승강기 홀·서쪽 복도 = 멈춤, 탈의 방·관리실 = 재생.

## 구현
- 타이머: `ExplorationTimerZones`(ExplorationTimer/Frame8Areas 콜라이더를 이름으로 재생/멈춤 분류) → 호스트가 `CampaignRun.SetRestTimerPaused` → `RunState.SetRestTimerPaused`(Running↔Stopped). 겹치는 자리는 멈춤 우선(계단실 구역이 로비를 덮음). 0이면 기존 `EndRest→EnterPerformance`. 규칙은 `ExplorationTimerZoneRule`(EditMode 3/3).
- UV: `ItemCatalog` uv-lamp → Equipment, 내구도 없음(에셋·FillFromDraft 모두). 입력 에셋 Common에 `UvLight`(<Mouse>/rightButton) 추가, `PlayerInputFrame.UvLightHeld`. `UvFlashlight`(StructureAExploration): 가슴 높이에서 카메라 시선 방향 보라 Spot(50°, 12m) + 닿는 곳 원형 표시, 원뿔·시야 안 `UvMark`를 매 프레임 `Illuminate`, 미발견이면 호스트에 RequestUse. `ExplorationState.Use`는 장비면 어느 슬롯이든 인정. `UvMark`는 F 없음, 비추는 동안만 marks 활성(노란 자체발광 재질), 발견 시 0.5초 전기음 + `Discovered` → 손전등이 카메라를 낙서 쪽으로 한 번 돌림(마우스 시선 시점).
- 로비: 더미 UV(디버그 4·시험 1) 제거, `LobbyToolSpots/lobby-uv-lamp-1~4`(UID 41~44, 정식).
- 배관함: 판 뚜껑 → 철창(테두리·가로 3·세로 5) 조각으로 교체(무너질 때 함께 떨어짐).
- 거울: `PlanarMirror` + `CurtainCall/PlanarMirror` 셰이더(대칭 카메라·사선 투영, 보이고 14m 안일 때만 렌더).
- 연습실: 링·스테이션 스포트 빨강/호박/파랑/흰 순환 ×1.6, 채움광 ×2. 서쪽 둥근 벽 안쪽 7.2×4.05m 모니터(`WaitingRoomCctvMonitor`, 연습실 내부 카메라 1곳, 15fps).
- 외형: `StructureAPropShapes` — 상자(판자 틈·기둥·못), 캐비넷/서랍(손잡이·자물쇠판·슬릿·발), 강철상자(리벳·띠·걸쇠·자물쇠), 열쇠(고리·대·날), 문서(글줄·접힘/신문 머릿글/지도 격자·경로/노트 스프링), 벽 낙서(스프레이 획·흘림). 각 대상 아래 `PropShape`, 숨김/열림 목록에 등록.
- 메뉴: `Install Confirmed Features (UV·Timer·Mirror·Practice)`(DoorVisualFix 포함) → `Apply Exploration Prop Shapes`. 재실행 가능.

## 검사
- EditMode ZoneRule 3/3. PlayMode M2_Confirmed: 타이머 구역·배치 확인 PASS, UV 손전등 테스트는 enum 경로 수정 후 재실행(결과 log 참조).
- 사용자 지시로 기존 테스트 회귀 확인 생략. 영향 받은 기존 테스트는 새 규칙으로 수정(Review 디버그 8개, Tools UV 장비, Placement 로비 UV 이름·내구도).
- 편집 모드 렌더로 외형·철창·모니터 확인. 거울 반사·형광 색감·경고음·2인은 사람 확인 필요.

## 남은 것
- 키맵에서 UV 버튼(마우스) 재설정 허용. 추격자. 정식 효과음. 타이머 HUD의 멈춤 표시 색 확인. 연습실 모니터 라벨.
