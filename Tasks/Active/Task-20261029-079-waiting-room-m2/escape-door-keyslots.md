# 기계실 숨겨진 문·배관함 키패드·탈출로 열쇠 투입구·계단 보 (CC-182·CC-183)

2026-10-30 사용자 요청: 버그 3건 수정 + 탈출로 기획 반영(3차 변경까지).

## 기획(사용자 결정, 최종)
기계실 숨겨진 문(machine-hidden-door, x-4.05 z90.1~92.2)은 기계실 쪽에서 주변 벽과 구분되지 않는다.
→ 문 오른쪽 벽 앞 배관함(뚜껑으로 키패드 가림)을 천막 말뚝 뽑개로 3번 F → 뚜껑·배관이 무너지고 키패드가 드러남
→ 키패드 F: 숫자 버튼 오버뷰 UI, 1234 확인 → 숨겨진 문이 옆으로 밀려 탈출 복도가 열림(문에 직접 F 없음)
→ 복도 끝 탈출로 문(처음부터 보임) 양옆 열쇠 투입구에서 두 명이 동시에 F(0.75초) → 탈출로 문 열림.
- 오답: 경고음 + 입력 비움, 패널 유지·재시도. 정답: 확인음 후 패널 닫힘.
- 열쇠2 조건은 기계실 문에서 제외(획득만 유지, 사용처는 후속). 탈출 열쇠1·2는 팀 공유 획득 상태.
- 1차 구현에서 숨은 문 판·배관함을 탈출로 문 자리에 둔 것은 잘못(열 문 너머에 키패드) → 기계실 문으로 옮김.

## 수정
- CC-182: 탈출로 hiddenVisuals에서 Keypad 제외, 생성기 `StructureAPlacement.Door()`도 같은 규칙.
- 열림 시 숨김: hideOnOpen을 문짝·자물쇠만(`OpensAway`). 탈출 표지·경고선·경광등·녹, 계단 채광판·문턱은 남음.
- CC-183 잔여: 계단 위층 비행 머리 위 0.67m를 지나던 RoofGirder(x8.24) 3개를 가림벽 z104.5까지로 줄임. 생성기 `FactoryFinish` 재실행 시 이 메뉴를 다시 실행.
- `ExplorationGate.openedBy`: 지정 대상이 열리면 함께 열리고 직접 F 불가. `StructureAExitRoute`가 기계실 문에 escape-pad를 지정(열쇠2·공개 조건 제거).
- 숨겨진 문 판 `HiddenPanel`: 기계실 DeepPlant/LowerShell 서쪽 면과 같은 두 재질·월드 UV의 1cm 판(z89.95~92.35, 바닥~-5.0). 정답 시 -z로 2.45m 밀림(`ExplorationSlide`).
- 문 테두리: DoorReveal 기둥·RevealLintel은 저장 상태부터 꺼 두고 showOnOpen으로 개방 시 켬. 끊겼던 벽 강철 띠는 `HiddenDoorLedger`로 이음(2.4m, 충돌 없음). 바닥 DoorSill·HiddenPassageThreshold는 충돌 바닥이라 남기고 기계실 쪽 돌출만 벽면까지 줄임. 위 캣워크 층 같은 이름 오브젝트는 건드리지 않음.
- 배관함 `PipeBox`(기계실, 문 오른쪽 z92.55~93.70): `BreakableWall` 재사용, id structure-a:machine-pipe-box, 말뚝 뽑개 3타, 조각=배관4+고정대2+앞 뚜껑. 몸체 위·양옆·턱 충돌체. 키패드·패드 대상을 함 뒤판으로 이동, 패드 revealTarget=배관함.
- 키패드 경고음: `ExplorationGate`가 RequestAnswered 오답(`ExplorationState.WrongCode`)에 임시 합성 경고음+입력 비움.
- 탈출로 문: revealTarget 해제로 처음부터 표시. 양 기둥 앞 `KeySlot_L/R`(높이1.15m, 판+황동 링+열쇠 구멍+램프). `keySlots` 협력 판정은 서로 다른 구멍의 두 사람만 인정. 문짝 앞 사슬 자물쇠 장식은 끔.
- 화장실 벽 시험은 BreakableWall이 둘이라 `RestroomBreach()`로 ID 지정. ExitRoute 시험은 새 기계실 문 규칙으로 갱신.
- 메뉴: CurtainCall/Waiting Room/Fix Door Visuals And Stair Girder (재실행 동일 결과).

## 검사
- 최종 M2_ 전체 실행 23/30: 기존 실패2(ContainerRoute·ToolsChain, HEAD에서도 실패) + 키보드 입력 시험5(AllRooms·InputWalk·FInput·Review2)가 4.0초 경계 정체/입력 누락으로 실패. 5건 모두 단독 재실행 PASS(AllRooms 숨은 문 개방 경로 포함). 이전 전체 실행(3차 변경 전)에서는 5건 PASS.
- Placement(배관함3타→패드→오답 유지/비움/경고음 소스→정답→기계실 문 개방·판 밀림·투입구 판정) PASS, ExitRoute3 PASS, Reach1/1, EditMode WaitingRoom 88/88.
- 화면(편집 모드 렌더): 닫힌 숨은 문 자리 벽·강철 띠 이어짐·테두리 없음, 뚜껑으로 키패드 가림, 개방 후 복도·탈출로 문·투입구 확인.
- check-work 안전성/규칙/컴파일 PASS, 씬 dirty0.
- 미검증: 실제 2인 동시 열쇠 돌리기, 경고음 청취, 사람 직접 플레이. 정식 효과음·배관함 아트·탈출로 너머 공간/엔딩은 별도.
- 수치 영향: 발견품 말뚝 뽑개는 1회용이라 화장실 벽(6타)과 배관함(3타)을 모두 하려면 말뚝 뽑개 2개 필요.
