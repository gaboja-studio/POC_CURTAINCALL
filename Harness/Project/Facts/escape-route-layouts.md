---
summary: 탈출로 A/B/C 배경 검토 씬과 자유 비행·제작 라벨·구조 검사 범위
status: active
updated: 2026-10-28
source: Task-20261027-073, 저장 씬·정적 구조 검사·Play Mode 상태 조회
---

# 탈출로 A/B/C 검토 씬

## 언제 읽나
탈출로 배경을 둘러보거나 A/B/C 구조를 수정·재생성할 때.

- A: `Assets/Scenes/Levels/escape_route.unity`. 대포·외줄 선행, 트램폴린·그네 후행. 2026-10-28 후속으로 03은 `16_VerticalFacility`의 50×50m 수직 설비실(Y-90–44)로 대체했다. Y0→Y-6 횡단→계단→기존04 Y+2이며 03의 이전 Y+2 발판 기준은 더 이상 적용하지 않는다.
- B: `Assets/Scenes/Levels/escape_route_b.unity`. 트램폴린·그네 선행, 대포·Y+4 고립 발판·대각 외줄 후행, 긴 하강과 재합류.
- C: `Assets/Scenes/Levels/escape_route_c.unity`. 횡단 축이 꺾이는 대포·Y+2 편심 발판·대각 외줄, 남쪽 그네 횡단, 북상 질주와 출구.
- 기존 중성 회색 콘크리트·큰 층고/깊이·밝은 갈대밭을 공유한다. 배치 수치와 상세 비교는 Task의 `variants.md`·`Layout/`을 따른다.
- 각 Main Camera의 `EscapeRouteFlyCamera`: WASD/마우스, Space 상승·Ctrl 하강·Shift 가속·Esc 해제. 비행 중 기존 걷기 카메라와 씬 Player만 정지한다.
- `EscapeRouteReviewLabels`는 `Label_` 제작 라벨을 Play에서 숨긴다. `Wayfinding_` 현장 표식은 유지하며 편집 화면의 라벨은 보존한다.
- 씬 열기: `Tools/CurtainCall/탈출로/A·B·C 구조 씬 열기`. 재생성 메뉴는 제작 배경을 교체하므로 일반 검토에는 쓰지 않는다.
- B/C의 `ValidateCurrentVariant()`는 구역 연결 9곳·바닥 높이 11곳·단절부 3곳 및 누락 참조를 정적으로 검사한다. 실제 묘기·인원별 통과·성능 검증이 아니다.
- 현재는 배경 구조안이다. 묘기·문/협동 장치·추격 AI·탈출 판정·런타임 세트 선택·대기실 세트 연결은 구현하지 않았다.
- A 수직 설비실: `EscapeVerticalCavern.cs` 제작 / `EscapeVerticalValidation.cs` 정적 확인. 기존03·14와 일부 보조 구조는 비활성 보존. 새 그룹은 실제 미터 단위이며 부모의1.25 배율을 상쇄한다. 구형03 조정 메뉴 대신 새 수직 설비실 메뉴를 사용한다.
- A 나머지 최신 구조: `17_BrutalistRoute` / `EscapeBrutalistRoute.cs`·`EscapeBrutalistEnd.cs`. 04 깊은 수로, 05 하부100m·도착Y-8, 06 상행40단, 07 반출 트렌치와 두꺼운 외벽. Task의 `a-brutalist-route.md`가 이전 A 기준보다 우선한다. `EscapeRouteMeshes/`의 실제 미터 UV 메시와 전용 무광 재질을 참조한다.
