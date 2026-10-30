# 3단계 — 열쇠·도구 획득 경로

## 구현
- StructureAKeyRoute: 실제 대상 참조에 확정 ID와 선행 조건을 일괄 연결. 위치/모델/아이템/텍스트를 만들지 않는다. 미지정 구역은 건너뛰므로 구역별 연결 가능. 같은 컴포넌트를 서로 다른 경로에 지정하면 전체 적용 전에 거절한다.
- Gate.ConfigureRoute: 기존 선행 조건(예: 패드)과 개방 방식·보상·내구도 보존. 열쇠는 상자 개방 후에만 표시/상호작용 가능하며 별도 F 획득 전에는 다음 문 조건이 충족되지 않는다.
- ToolTarget.ConfigureRoute: 경로 ID만 지정. 기존 머리핀 성공 확률·3번째 보장과 빠루 개방·내구도 규칙 보존.
- FoundItemSpot.availableAfter/UnlockAfter: 지정 대상이 열린 뒤에만 기존 바닥 기물 생성. 값이 없으면 이전처럼 즉시 생성. UID·발견품 내구도·슬롯·F 줍기·세대당1회 생성 유지.

## 확정 연결
- key1 → steelBox → managementKey
- lockedBox1 → stairsKey → stairsDoor
- cabinet3 → escapeKey1
- managementDrawer → escapeKey2
- escapeKey1 + escapeKey2 → escapeDoor (기존 패드/협력 조건 보존)

## 검사
M2_StructureAKeyRoute_UnlockPickupAndToolChain: 조기 개방 거절, 상자 개방과 열쇠 획득 분리, 실제 머리핀/빠루 사용·소진, 계단 문과 탈출 열쇠 연결, 패드 조건 보존, 혼자 협력 문 개방 거절, 개방 전 도구 생성 없음·개방 후1회 생성, 중복 참조 적용 거절을 검사한다. 검사 대상의 도구 생성 위치/UID/사용 횟수는 테스트 입력이며 정식 기획을 확정하지 않는다. 결과는 log 참조.

## 제외·남음
정식 맵 배치/참조 저장·에셋·상세 텍스트는 제외. 관리실 열쇠 사용처, 열쇠2/도구 획득처 등 미정 규칙을 추가하지 않는다. 실제2인 검증은 후순위. 최신 dev 전체 병합은 별도이며 현재 브랜치 기준 검사다.
