---
summary: 공연 HUD C2·C3·P1 정본, 표시 API·독립 확인 씬·원본 보존. HUD 배치나 게임 데이터 연결 전에 읽기
status: active
updated: 2026-10-20
source: Task-20261019-061, PM 공연 HUD 조립 계획 승인
---

# 공연 HUD

- 원본: `Assets/_ThirdParty/curtaincall_performance_hud_v1/`. 그림 신규20종은 제공 명세상 사람 피드백 대기이며 Unity 동작 검증과 구분한다.
- 정본: `Assets/Resources/Prefabs/UIs/PerformanceHud/PerformanceHud.prefab`. C2 남은 시간·C3 진행도·P1 균형만 포함한다.
- 독립 씬: `Assets/Scenes/Tests/PerformanceHud/PerformanceHud.unity`. 기존 C1 정본의 별도 인스턴스를 함께 표시하며 실제 Reward binding은 이 인스턴스에서만 꺼 둔다.
- Play 하단: 평소/경고·위험/4인 겹침/미수신/관전 전환/외줄 전환/공연 전환/초기화. 경고 버튼은 주황(0.4)→위험(0.8) 순환, 다른 샘플 진입·초기화로 순환 리셋. `ShowSample(1)`은 항상 주황부터다. 상태 설명의 P1 Safe/Warning/Danger로 구분하며 게임과 연결하지 않는다.
- HUD에는 EventSystem·GraphicRaycaster가 없고 모든 Graphic은 raycastOff다. 입력 도구는 씬 전용 Controls에만 둔다.
- private Runtime namespace: `CurtainCall.UI.PerformanceHudV1`, Editor: `.Editor`. 제공 Runtime을 공개 폴더로 복사하지 않는다.
- `PerformanceHUDView.SetContext(performanceActive, onTightrope, observing)`: C2/C3는 공연 중, P1은 외줄·유효 균형·비관전일 때 표시한다.
- `ResetForRuntime()`과 Awake는 미수신·숨김으로 초기화한다. Inspector 샘플은 편집 전용이며 실게임 초기값이 아니다.
- Timer `SetRemainingSeconds(double?)`: 초 올림/m:ss, 0~5999초. 미수신·음수·비유한 값·올림 후 상한 초과는 —. 30초 이하 경고/10초 이하 위험은 제공 표시 계약이다.
- Progress `SetProgress(courseLength, playerDistances, localPlayerIndex, fireDistance, maximumReachedDistance, fallDistances, fireIsNear)`: 2~4명/로컬-1 허용, 선택 마커 null 허용. 위치 clamp, 겹침은 실제 x를 바꾸지 않고 요약한다.
- `fireIsNear`는 외부 입력값 그대로 표시한다. 코스 길이·거리·불 임계값을 이 HUD에서 결정하거나 누적하지 않는다.
- Balance `SetBalance(balance, safetyMin, safetyMax, warningMin, warningMax, riskFraction, leftBindingLabel, rightBindingLabel, leftPressed, rightPressed)`: 유한 균형·경계[-1,1], `warningMin ≤ safetyMin < safetyMax ≤ warningMax`, 위험 비율[0,1]. 무효 입력은 숨긴다.
- 위험/주황/안전/주황/위험 5구간, 경계4개. 경고 폭0은 숨기며 빗금은 바깥 위험에만 표시한다. 기존8인자 API는 경고 폭0으로 위임한다.
- `riskFraction`은 경고·위험·착지 충격 등을 외부에서 합산한 추락 위험 비율이다. HUD가 빨강 체류만 세거나 누적을 초기화하지 않는다. 최신 기본 폭 ±30/±50은 확인 씬에서만 ±0.3/±0.5 합성값으로 재현한다.
- 키캡은 전달받은 라벨/눌림 표시만 한다. 실제 입력 polling·물리·체류 누적·시간 감소·네트워크 연결은 없다.
- 배치: C2 좌상160×56/(48,-40), C3 우상408×76/(-48,-40), P1 하단중앙544×80/(0,160). Overlay/1920×1080/Match0.5/referencePPU100/SpritePPU200.
- 표시 Sprite23 = 신규20 + clock/fire/flag3. 기존3은 CommonIcons 정본 읽기 전용, 중복 원본도 보존한다.
- 신규 PNG만 importer를 보정한다. 받침·트랙 Sliced/빗금 Tiled+RectMask2D/체류 Filled Horizontal. balance_fill 16×16은 전체 불투명 규격이다.
- 전용 Static TMP Medium/SemiBold/Bold: `Assets/Resources/Fonts/PerformanceHud/`. 기존 폰트는 글리프 부족으로 수정하지 않고 전용 자산을 생성했다.
- 남은 거리24px 글꼴은 줄 높이가28px 영역보다 약간 커 Ellipsis로 통째로 숨겨졌다. 지정 크기·배치를 유지하고 해당 텍스트만 Overflow로 보정했다.
- 조립: `Assets/Scripts/Systems/PerformanceHud/Editor/PerformanceHudProjectBuilder.cs`, 메뉴 `Tools/Curtain Call/공연 HUD/프리팹과 확인 씬 생성`. 기존 출력·dirty 씬·Play·Prefab Stage를 거부한다.
- 기존3구간 보정 메뉴 `Tools/Curtain Call/공연 HUD/P1 경고 구간 보정`: 소유 확인 씬 하나만 clean 상태로 열고 실행. 기존 객체를 보존하고 경고 Image/경계4개만 증분 추가한다. 반복 적용은 중복 생성하지 않는다. 새 문구용 ‘위·험’은 HUD 전용 SemiBold에만 추가, Static 유지·공유 폰트 불변.
- 원본74실파일(meta 제외)/PNG40 전체 보존: Source·Tools·Docs·시트·목업·폰트·라이선스는 재제작/확장용이다. 런타임 미참조라는 이유로 삭제하지 않는다.
- 폰트 OFL을 전체 그림 라이선스로 확대하지 않는다. 누적 manifest77행은 이번 신규20종 수가 아니다.
- 전수 경로·SHA256·GUID·실사용 정본·향후 후보: Task의 `performance-hud-inventory.csv`. 검사 상세: `performance-hud-verification.md`.
- 제공 문서의 진입120ms/퇴장100ms 애니메이션은 코드에 없으며 이번에 추가하지 않았다. 실게임 연결과 사람 최종 그림 승인은 별도다.
