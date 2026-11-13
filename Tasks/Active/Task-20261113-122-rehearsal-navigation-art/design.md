# 리허설 네비 UI — 황동 무대 표식

## 원화와 재질
기존 목재 안내판·버건디 천·크림색 기호를 기준으로 황동 캡슐형 바닥 표식과 천 원판 목적지를 제안했다. 긴 점선을 없애고 독립 표식을 반복한다. 원화는 imagegen 제작이며 실제 Unity 캡처와 구분한다.

원화: `Assets/_ThirdParty/curtaincall_navigation_v1/Source/navigation-concept.png`.
제작 프롬프트: 같은 Source의 `generation-prompts.md`.

| 재료 | PNG | 용도 |
|---|---|---|
| 낡은 황동 | brass-base.png | 경로 표식 바탕 |
| 크림색 에나멜 | ivory-chevron.png | 진행 방향 |
| 버건디 직물 | burgundy-cloth.png | 목적지 바탕 |
| 황동 링 | brass-ring.png | 목적지 테두리; 중앙 투명 |
| 크림색 에나멜 | ivory-star.png | 목적지 문양 |

5개 모두 1254×1254 RGBA 원본. Unity Sprite Single, PPU 1024, Trilinear, mipmap, Aniso 8, clamp, 무압축. 원본 패딩은 유지하고 조립할 때 실제 알파 경계 기준으로 크기를 정규화한다. 그림자 레이어는 바탕 스프라이트에 어두운 tint를 주어 재사용한다.

## 프리팹
`Assets/Resources/Prefabs/UIs/Navigation/`:
- `NavigationRouteMarker.prefab`: 그림자 / 황동 / 방향표 3레이어, 보이는 황동 폭 약 0.82m.
- `NavigationDestination.prefab`: 그림자 / 직물 / 황동 링 / 별 4레이어, 링 폭 약 1.45m.
- `RehearsalNavigation.prefab`: 표시 전용 컴포넌트, 목적지, 예시 경로의 표식 11개. 인스펙터에서 레이어와 간격을 편집할 수 있다.

LineRenderer 없음. SpriteRenderer + SortingGroup, 레이어별 4mm 높이 분리. 전용 unlit shader는 알파 블렌드, ZWrite Off, ZTest LEqual이다. 벽을 투시하지 않는다. 높이 0.045m는 데모 바닥 기준이다.

## 에디터에서 보기
`Assets/Scenes/Tests/Navigation/NavigationReview.unity`를 연다. 경로 전시와 x=25의 레이어 보드가 있다. 메뉴 `CurtainCall > UI > Navigation`에서 조립 및 검증/캡처를 재실행할 수 있다. 조립기는 미저장 씬이 있으면 중단하며, 검토 씬을 다시 만든다. 프리팹 수동 수정 후 재조립하면 이 제작값으로 덮어쓰므로 먼저 변경을 보존한다.

## 향후 게임 연결 계약
`CurtainCall.UI.Wayfinding.RehearsalNavigationView.SetPath(IReadOnlyList<Vector3>)`에 검증된 월드 바닥 경로를 전달하고 `Hide()`로 숨긴다. 현재 기존 `RehearsalPathGuide`와 연결하지 않았다. 목적지 기준 일정 간격으로 배치하며 코너에서는 회전만 완만하게 바꾼다. 위치는 입력 경로 위를 유지해 벽 모서리를 자르지 않는다. 접힌 경로에서 표식 간 근접 겹침을 억제한다.

현재 지면 법선/계단/기울어진 바닥 투영, 네트워크·VisibilityChanged 인계, 실제 리허설 종료 이벤트, 플레이 중 장시간 깜빡임은 미검증이다. 이번 결과는 프리팹 시각 프로토타입이며 기존 버그의 실게임 해결 완료를 뜻하지 않는다. GameSettings·게임 규칙 변경 없음.

## 증거
Captures의 3개 PNG는 Unity Camera.Render로 저장한 실제 독립 검토 씬 렌더다. 생성 원화나 게임 진행 캡처로 오인하지 않는다. `validation.txt`에 참조·셰이더·입력 경계 검증 결과를 저장했다.
