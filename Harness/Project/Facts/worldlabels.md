---
summary: 월드 라벨 v2의 소품 표면3종·근접 정보2종·전용 정본/독립 씬·명시 관찰자/카메라 API
status: active
updated: 2026-10-20
source: Task-20261019-061
---

# 월드 라벨 v2

## 언제 읽나
월드 라벨 정본을 배치하거나 표시 API·독립 검증을 확인할 때. 실제 게임 연결은 별도 Task다.

## 제작 범위
- private 키트 `Assets/_ThirdParty/curtaincall_worldlabels_v2/`의 제공 Runtime/Builder를 원위치 재사용한다. PNG·제공 코드의 공개 복사 금지.
- Wood/Cloth/Paper는 소품 표면, Item/Shelf는 근접 정보 UI다. L2/L3/H1/W4/W5를 개별 소재와 임의 대응하지 않는다.
- 출력 전용 폴더는 `Assets/Resources/Prefabs/UIs/WorldLabels/`, `Assets/Resources/Fonts/WorldLabels/`, `Assets/Scenes/Tests/WorldLabels/`이며 5프리팹·전용 Medium/Bold·독립 확인 씬을 제작했다.
- 신규 물리 소재12개만 importer 보정, smallplate4·flag/person/tools3은 기존 performance_hud/icons 정본 읽기 전용 참조. 최초 바이트 대조7/7 일치.
- 종류별 루트+직접 자식7, Image5/TMP2. World Space Canvas, 입력 비차단, 소유 전용 Static Medium/Bold를 사용한다.
- 제안 기본 크기 Wood/Cloth320×96·Paper240×200·Item224×64·Shelf96×64 UI. scale0.0025·거리2.0/2.4는 게임 확정값이 아니다.
- `TrySetContent`/`TrySetSize`는 TMP 실측 실패 시 승인 내용/크기 보존. Paper만 본문 최대4줄, Shelf는 실제 Bold32. Shelf 최소96×56만 세로여백8로 글자영역40을 확보한다(기본64는 여백12 유지).
- `TrySetSurfaceVisible`은 소품3종의 표면4층만 숨긴다. Item/Shelf는 명시 Observer 거리 gate와 호출자 표시 gate를 함께 적용한다.
- Billboard는 명시 Camera 회전만 복사하며 기본 비활성이다. 카메라 자동 탐색·로컬 소유권/LOS·상호작용·네트워크·실게임 연결 없음.

## 검증·보존
- 원본161파일/PNG29·기존 소유 출력/코드838개 SHA baseline을 제작 전에 보존했다.
- 제공 번들의 정적 검사513건·목업 그림은 Unity 컴파일/테스트/게임 캡처 결과가 아니다.
- settings 최소 namespace/alias 보정 승인 후 충돌 해소, check-work 구역/문서/컴파일·EditMode798/798 PASS. Preview 폭 보정 후 두 화면비 Mouse44+Enter24=68/68·버튼 경계/글자 overflow 검사 통과. 신규Play 최초18/18 및 최종 선택 회귀 상세/첫 실패는 Task의 `worldlabels-verification.md`. 사람 외형 확인 전이다.
