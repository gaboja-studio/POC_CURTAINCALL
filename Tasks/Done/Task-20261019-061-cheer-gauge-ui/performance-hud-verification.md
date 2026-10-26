# 공연 HUD 검증 (2026-10-20)

## 결과물·경계
- `Assets/Resources/Prefabs/UIs/PerformanceHud/PerformanceHud.prefab`: C2 남은 시간/C3 진행도/P1 균형, Sprite23종·Image53개(P1 보정 전49개). C1은 포함하지 않는다.
- `Assets/Scenes/Tests/PerformanceHud/PerformanceHud.unity`: 기존 C1 별도 인스턴스+독립 Controls/단일 EventSystem/InputSystemUIInputModule. C1 실제 Reward binding은 이 씬 인스턴스에서만 비활성화했다.
- 공개 코드 `Assets/Scripts/Systems/PerformanceHud/`: 안전 조립 wrapper, 씬 전용 합성 값 제어, PlayMode 테스트. 외부 제공 Runtime/조립기 코드는 private 원위치 유지.
- 이번 삭제·commit/push·PR·병합·실게임 연결 없음. 신규20종 그림 피드백 대기와 Unity 동작 검증을 구분한다.

## 전수 점검·보존
- [performance-hud-inventory.csv](performance-hud-inventory.csv): meta 제외74실파일/PNG40, 분류·크기·SHA256·GUID·표시 정본 참조·보존·사용 후보 기록.
- 실제 표시23종 = 신규20 + clock/fire/flag3 정본. 원본 RGBA/크기·투명 시트셀 일치23/23, 중복3 정본과 바이트 일치.
- 기존3 meta SHA256 작업 전후 동일: clock ec0b3335…1127/fire f40a76f6…dda1/flag 5f362146…a768. GUID/importer 수정 없음.
- 신규20만 Single/FullRect/PPU200/중앙/지정 border/sRGB/Bilinear/Clamp/Uncompressed/noMip/noReadWrite/NPOTNone. 실측 Sprite23/플랫폼115조합 중 활성 override0/실패0.
- balance_fill은16×16 전체 불투명 RGBA 타일이다. 런타임 미참조 폴더·중복 PNG·Source/Tools/Docs/시트/목업/폰트/OFL도 제작·확장 후보로 전부 보존한다.
- 누적 resource_manifest77행은 기존 키트까지 포함하며 신규20개 수가 아니다. 폰트 OFL을 전체 그림 라이선스로 해석하지 않는다.

## Unity 검사
- 새 namespace `CurtainCall.UI.PerformanceHudV1`/`.Editor`, 기존 UI 코드 충돌 없음. importer는 공유 정본 변경을 차단한다.
- 정본 HUD에 GraphicRaycaster0/Graphic raycastTarget0. Controls에만 raycaster/입력 모듈, HUD가 마우스 입력을 가로채지 않는다.
- prefab/scene 의존성76개 누락0/Missing Script0/TMP 글리프 누락0. 전용 Static Medium/SemiBold/Bold 생성, 기존 Static 폰트 수정 없음.
- 전용 PlayMode5/5 PASS: 초기 미수신/숨김·참조·글리프·입력 구조, 시간 경계·색상·자동 감소 없음, 진행2~4인/겹침/옵션/local-1/clamp/invalid/fall pool, 균형 경계/무효/키표시/관전·외줄·공연/Reset, 버튼8종 Mouse·Enter 총16입력.
- 시간 null/음수/NaN/±Infinity/상한초과, 0/10/30/31/소수 올림/5999초를 실제 공개 API로 검사했다. 게임 임계값·입력·물리 규칙을 새로 만들지 않았다.
- 실제 Play Overlay1920×1080 평소/경고/4인겹침/미수신, 1600×1000 밝은 배경 경고를 캡처·시각 확인했다. 받침 위 HUD는 명암 배경에서 정상이며 밝은 배경의 씬 전용 상태 설명은 저대비다.
- 확정 캡처는 scratch의 hud-final-{0,1,2}-1920.png/hud-unavailable-final.png/hud-bright-1600.png. 초기 비동기 CaptureScreenshot는 다음 상태가 섞일 수 있어 확정 비교에 사용하지 않고 CaptureScreenshotAsTexture 동기 캡처로 다시 확인했다.
- 합성 입력 검사이며 사람 최종 그림·실제 게임 연결·공동/네트워크 검증은 미실행이다. 제공 문서120/100ms 진입·퇴장 애니메이션은 미구현 그대로다.

## 보정·실행 이력
- 첫 조립은 폰트 신규 생성에 따른 임포트 중 안전 가드로 중단. 출력 생성 전 중단했으며 임포트 완료 후 재실행했다.
- 전체 불투명 balance_fill을 Unity 알파 누락으로 오인하는 제공 검사1건 보정. 다른 Sprite 알파 검사는 유지했다.
- 실제 Play에서 남은 거리 텍스트가 사라짐을 발견: Pretendard24px 줄 높이28.63 > 지정 영역28px, Ellipsis로 문장 전체 숨김. 해당 텍스트만 Overflow로 보정해 크기/위치를 유지하고 mesh 문자 수 회귀 검사를 추가했다.
- 동기 HTTP PlayMode 실행은 domain reload로 불가 응답(0tests)이라 통과로 세지 않고 async 실행으로 전환했다.
- doctor FAIL은 시작 때 보고한 private 무관 미저장 변경 때문이다. 기존 tracked220파일/4886삽입/931삭제 및 unity_only 폴더/meta를 보존했다.
- 기존 UI 정본·공유 폰트·키트 변경0 확인, 회귀 CommonIcons3/3/CommonButtons2/2/BasicPanels3/3/CheerGauge3/3 PASS.
- 보정 전: 강화된 mesh 문자 회귀 포함 HUD5/5 PASS. 표준 check-work 구역/문서/컴파일·EditMode798/798 PASS. Play stopped/PerformanceHud 씬 clean/1920×1080 복구, 참조·글리프·스크립트0누락.

## P1 주황 경고 누락 보정 (2026-10-20 PM 승인)
- 최신 외줄은 안전 ±30/경고 ±50/전체 ±100. 기존3구간 표시를 위험/주황/안전/주황/위험5구간, 경계4개로 보정했다. 기존 외줄 씬·Player·Settings는 수정하지 않았다.
- 새10인자 API는 안전·경고 경계를 모두 수신하며 유한값·순서·범위를 검사한다. 구8인자는 경고 폭0으로 위임한다. 위험 비율은 외부 합산값 표시만 하며 HUD 자체 누적·게임 판정 없음.
- 확인 씬의 기존8버튼·ID·배치 유지. 경고/위험 버튼은0.4→0.8→0.4 순환, 다른 샘플·초기화 시 리셋. ShowSample(1)은 주황부터. 상태 설명에 P1 Safe/Warning/Danger 표시.
- 기존 prefab의203개 컴포넌트 식별자 전부 보존, 추가4객체/12컴포넌트. 전체 Image53/Sprite23/의존성76, 누락·Missing Script·글리프·HUD raycast0. 보정 메뉴 반복 적용 후 prefab/씬/폰트 해시 변화0.
- 새 문구의 ‘위·험’ 글리프가 없어 HUD 전용 SemiBold에만 추가했다. Static을 유지하며 공유 폰트·PNG importer는 불변이다. TMP sourceFontFile setter가 internal이어서 첫 컴파일 실패 후 TMP 편집기 원본 getter로 GUID 참조를 복원하도록 보정했다. 첫 실행은 prefab 저장 후 폰트 원본 참조 단계에서 중단됐으나 재실행으로 완료했다.
- HUD PlayMode 최종9/9 PASS: 5구간424px 연속 geometry/색/빗금·경계, ±0.3/±0.5 전후·±1·비대칭·폭0·full-safe, 비유한/역전/범위초과 및 정상 복구, 위험도0/1·자동 누적 없음, 구8인자 호환, Mouse/Enter 순환·샘플 리셋·기존8버튼/시간/진행/초기화 회귀.
- 첫 테스트7/9: 새 테스트2개가 P1 내부 구간명 텍스트를 잘못 기대했다. 실제 계약인 바늘 위치 및 씬 상태 설명 검사로 고쳐9/9 재실행. 합성 입력 검사이며 사람 플레이·실게임 연결은 아니다.
- 회귀 통합 정규식 필터는 지원되지 않거나0건이어서 PASS로 세지 않았다. 개별 재실행은 CommonIcons3/3·CommonButtons2/2·BasicPanels3/3·CheerGauge3/3 PASS(합계11/11).
- 실제 Play1920×1080 안전/주황/위험/미수신 및1600×1000 주황/위험, 양쪽 경계±0.3/±0.5를 동기 캡처·시각 확인했다. 경계 바늘·5구간·외곽 빗금·C1 동시 표시 정상. Play stopped/1920×1080/runInBackground=false로 복구했다.
- 확정 캡처: scratch의 p1-safe-1920.png, p1-warning-final-1920.png, p1-danger-final-1920.png, p1-unavailable-1920.png, p1-{warning,danger}-1600.png, p1-boundary-review.png. 초기 p1-warning-1920.png/p1-danger-1920.png는 파일명과 선택 상태가 반대여서 제외하고 상태·프레임을 함께 기록하여 재촬영했다.
- 최종 객체 비교203→215컴포넌트/기존ID 변경0/버튼8개 persistent 연결 불변. 소유 씬 재개방 후 확인했으며 임시 빈 씬에서 비교한 연결 불일치는 검증 증거로 쓰지 않는다. 원본74파일/PNG40/기존3 meta SHA256 및 private 무관220파일/4886삽입/931삭제 보존.
- P1 보정 후 표준 check-work 구역/문서/컴파일 PASS, EditMode798/798 PASS. 삭제·Git 저장·실게임 연결 없음.
