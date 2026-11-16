---
summary: 시작 화면 배경 연출 연결과 실제 씬 경로, 클릭 차단 조건
status: active
updated: 2026-11-16
source: Task-20261116-138
---

# 시작 화면 배경 연출

- 실제 씬은 `Assets/Scenes/InGame/Start.unity`. 예전 `StartLobbySceneBuilder`의 경로를 따라 씬을 통째로 재생성하지 않는다.
- `StartMenu.Start()` → `StartMenuBackdrop.Attach()`가 같은 씬의 `Start Stage`를 찾는다. `Back Curtain`, `Drape Left/Right`, 자식 `Fold`, `SM_Prop_Chair_Folding_01`, `Stage Floor`, `Pin Spot` 이름을 바꾸면 연결도 갱신한다.
- 원본 커튼은 높이 방향 중간 정점이 없는 원기둥이다. 클릭 높이에서 휘게 하려고 실행 중에만 세분화 메시를 사용한다. 상단을 고정하고 메시/물리 자원을 씬 종료 시 정리한다.
- 배경 입력은 새 Input System과 GraphicRaycaster를 사용한다. 단순 IsPointerOverGameObject만 쓰면 3D 레이캐스터가 장식 입력 자체를 차단할 수 있다. 주 메뉴/포커스/접속/공통 설정/전환 상태도 확인한다.
- 의자는 클릭 지점의 힘과 토크로 움직인다. 연속 클릭하거나 길게 누르면 힘이 커진다. 최종 힘·넘어짐·커튼 질감은 실제 Play Mode로 확인한다.
- 의자 경계는 Back Curtain의 앞쪽 최대 변형(.65m 여유)과 양옆 Drape의 안쪽에서 계산한다. Rigidbody pivot만 제한하지 않고 회전한 Collider 전체 bounds를 보정한다.
- `StartMenuHangingTitle`은 기존 TitleLogo의 PNG 여백을 유지한다. 6회 입력 시 먼저 누른 쪽 줄만 한 차례 늘어나며, 이후 반대쪽 입력도 추가 낙하를 만들지 않는다. 실제 Main Rect 위 24px를 유지한다.
- `TitleRopeGraphic` 프리팹에는 CanvasRenderer가 필요하다. MaskableGraphic 상속만으로 자동 생성된다고 가정하지 않는다.
- 개발/라이선스 데이터: `Resources/Settings/StartMenu/StartMenuCredits.asset`. Project Settings > Curtain Call > Development and Licenses 또는 Tools > CurtainCall > Start Menu > Credits Settings에서 편집한다.
- 추가 한글은 전용 런타임 TMP 폰트에만 생성한다. 에셋 생성기는 SaveAssets 전체 저장 대신 해당 설정만 저장해 다른 동적 폰트의 자동 변경을 저장하지 않는다.
