# BUG-024 대기실 소품 끼임

- 요청: 2026-10-22 사용자 직접 요청. 통 위 점프 후 WASD로 안에 들어가 빠져나오지 못함. 다른 소품의 충돌 범위도 점검.
- 현재 브랜치: `codex/demo-day-four-ending`. 기존 데모 작업·사용자 변경 보존. 후속 저장 요청으로 관련 변경만 커밋·push, PR 보류.
- 관련 기존 담당 구역: Task-060의 WaitingRoomBase 씬·WaitingRoom 코드. 신규 번호는 임의 발급하지 않음. 정규 Task 부재에 따른 자동 구역 검사 제한은 그대로 남음.

## 원인과 수정

- 대상: `WaitingRoomGreybox/Mood/Tunnel/WallDressing/SM_Plasticbarrel`, 월드(46.599,0.005,73.298).
- 비볼록 MeshCollider의 윗면이 열려 있었다. 위쪽 중앙 ray가 높이 -0.001m의 통 바닥에 도달하여 진입 후 측면에 갇히는 구조 확인.
- 같은 메시를 닫힌 convex 충돌체로 변경. ray가 이제 1.272m 윗면에 닿음. 전역 플레이어/물리 설정·외부 원본 에셋은 변경하지 않음.
- 통로 골판지 상자와 전문가 방 의자에도 열린 비볼록 충돌체 확인, 같은 방법으로 닫음(상단 ray:0.747m·0.854m).
- 가구 생성 시 월드 AABB를 다시 로컬로 변환해 가로 범위가 부풀어 있는 소품13개 발견. 각 렌더러의 로컬 경계를 대상 좌표로 옮겨 가로 중심·폭·깊이만 정렬. 높이·배치·외형 유지.
- 보정13개: PackedTrunk, WaitingChair, ReadyPropsCrate, CircusStorageCrate_0/1, StorageTrunk_4, FootTrunk_1/3, StoredCostume_0/1, RetiredChair, StoredBarrel_1, EastCrates_2.
- 범위 밖 유지: 셔터 두께, 높은 천장 배관, 상단 보관물까지 막는 선반3개, 복합 기물 그룹은 단순 경계 일치만으로 결함을 판단하지 않고 유지.
- 재생성 도구는 변경/재실행하지 않음. 기존 수동 배치를 보존하는 최소 씬 보정이며, 전체 아트 재생성 시 충돌 보정 재점검 필요.

## 검증

- 에디터에서 활성 solid 충돌체333개(Box271·Mesh60·Capsule2) 조사. 메시60개 중 지상 소품의 열린 비볼록3곳 수정, 나머지 비볼록은 높은 천장 배관.
- 씬 수정 전후 비대상 컴포넌트15,931개 직렬화 동일. 대상 충돌체16개 외 변형 없음. WaitingRoomBase만 저장, isDirty=false.
- 컴파일: PASS(Unity6000.3.25f1, completed/failed=false/errors0). 새 `CurtainCall.WaitingRoom.Collision.PlayTests.dll` 생성 확인. 처음 검사는 테스트 임포트 후 씬 dirty로 거부됐고, 작업 대상 씬만 다시 저장해 재실행한 결과.
- 에디터 충돌 검사: PASS(통 위 ray1.272m, 상자0.747m, 의자0.854m; 변경 전 각 내부 바닥까지 통과). 가구13개 가로 경계는 각 렌더러 로컬 기준으로 보정.
- PlayMode: ENV_BLOCKED(컴파일 후 CLI의 STATUS_NO_INSTANCES). 두 차례 시작 요청 모두 테스트 실행 전에 거부됨. 에디터 PID16504·로컬7800리스너는 생존, 최신 재컴파일 상태 completed/failed=false/errors0 확인. 15초 until-ready도 연결 불가. 로컬 상태 API는 인증 부재로 거부되어 이후 별도 인증 우회/재시작/설정 변경은 하지 않음.
- 작성한 회귀 검사5개는 미실행: 실제 게임 PlayerMover의 캡슐로 통 착지·이탈3회, 닫힌 충돌체3곳, 가구13곳 경계. 이후 CLI beta.14 업데이트로 연결을 복구했지만 사용자가 추가 Unity 검증을 중단해 재실행하지 않음. 통 이동 검사는 주변 환경을 분리한 합성 변위 입력으로 작성했으며 실제 WASD 재현과 구분.
- 문서/규칙: PASS(verify-fast, 기존 WARN18/hard0), 변경 공백 검사 PASS. 신규 폴더·코드·asmdef의 .meta는 에디터에서 생성.
- 구역/통합 검사: 정규 Task 없는 codex 브랜치라 check-work/verify-scope 실행 제한(앞선 데모 작업 기록 참조). 하네스/기존 Task 배정을 임의 변경하지 않음.
- 실제 WASD·네트워크 다인 재현: 미실행. 자동 테스트의 변위 입력은 합성 입력으로 구분.

## 확인 방법

1. 대기실 무대 입구 쪽 파란 통 위로 점프하고 사방으로 이동: 통 내부에 빠지지 않고 옆으로 내려올 수 있어야 함.
2. 전문가 방 의자·통로 상자 접촉 후 후퇴, 보정된 가구 주변을 이동: 보이지 않는 큰 범위에 앞서 막히지 않아야 함.

## 지식 기록 제안

- 재생성 시 월드 AABB를 루트 로컬 AABB로 재변환하면 회전한 소품의 충돌 폭이 부푼다. 각 메시/렌더러의 로컬 경계 모서리를 루트 좌표로 직접 옮겨 계산해야 한다. PM이 Pitfalls 기록을 검토할 것.
