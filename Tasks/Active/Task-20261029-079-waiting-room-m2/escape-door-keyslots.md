# 탈출로 숨겨진 문·열쇠 구멍·계단 보 (CC-182·CC-183)

2026-10-30 사용자 요청: 버그 3건 수정 + 탈출로 기획 반영.

## 기획(사용자 결정)
키패드는 숨겨진 문 앞에 보인다 → 1234 입력 → 벽처럼 보이던 숨겨진 문이 옆으로 밀림 → 탈출로 문 양옆 열쇠 구멍 2개에서 두 명이 동시에 F(0.75초) → 탈출로 문 열림.
- 동시에 돌리기, 열쇠는 팀 공유 획득 상태(탈출 열쇠1·2 모두 필요), 숨은 문은 벽→옆으로 밀림.

## 수정
- CC-182: escapeDoor hiddenVisuals에서 Keypad/KeypadKey 제외. 생성기 `StructureAPlacement.Door()`도 같은 규칙.
- 열림 시 숨김: hideOnOpen을 문짝·자물쇠만(`OpensAway`)으로. 탈출 표지·경고선·경광등·녹·키패드, 계단 채광판·문턱은 열린 뒤에도 남음.
- CC-183 잔여: 계단 위층 비행 머리 위 0.67m를 지나던 ST_Stairwell_B1Level RoofGirder(x8.24) 3개를 비행 사이 가림벽 z104.5까지로 줄임. 생성기 `FactoryFinish` 재실행 시 다시 생기므로 이 메뉴를 다시 실행.
- 숨겨진 문: `HiddenPanel`(벽 재질, 문틀 자리 0.20×3.70×3.16) + `ExplorationSlide`(escape-pad 열림 시 +z 3.21m, 1.2초). 정답 전 문틀도 숨김.
- 열쇠 구멍: escapeDoor 아래 `KeySlot_L/R`(문틀 양 기둥 앞, 높이1.1m). `ExplorationGate.keySlots`가 있으면 거리·F 표시는 가까운 구멍 기준, 협력 판정은 서로 다른 구멍의 두 사람만 인정.
- 문짝 앞면 사슬 자물쇠 장식(SealedExit Hasp/Padlock/Chain/WallEye)은 열쇠 구멍으로 대체해 끔. 리벳·관찰창은 문짝과 함께 숨김/열림.
- 메뉴: CurtainCall/Waiting Room/Fix Door Visuals And Stair Girder (재실행 동일 결과 확인).

## 검사
- M2_ PlayMode 27/30. 이번 변경 검사(Access 2/2, Placement 4/4, ExitRoute 3/3, Flow 문 막음 포함) PASS.
- 실패 3건: ContainerRoute(미정 보상 재지급)·ToolsChain(UV 줍기)은 HEAD에서도 동일 실패=기존 문제. InputWalk는 HEAD 2/2, 코드만 1/1, 전체 변경 재실행 2/2 PASS. 앞선 2회 실패(4.01초)는 4초 정체 기준 경계의 불안정.
- check-work 안전성/규칙/컴파일 PASS, 씬 dirty0.
- 실제 2인 동시 열쇠 돌리기는 미검증(단일 클라이언트에서 구멍 판정·혼자 거절만 확인).
