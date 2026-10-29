---
summary: 공용 아이콘25종 catalog·비교 씬·정본 참조·제작 자료 보존. 아이콘 사용과 재제작 전에 읽기
status: active
updated: 2026-10-20
source: Task-20261019-061, PM 아이콘 조립·전수 점검 계획 승인
---

# 공용 아이콘

- 확인일: 2026-10-20, Task-061. 신규22종은 `1.0.0-review`이며 최종 그림 승인 전이다.
- 원본: `Assets/_ThirdParty/curtaincall_icons_v1/`. PNG·제공 코드는 private에 유지한다.
- 최종 UI25종 = 신규22 + 기존 settings/close/warning3. 누적 manifest57행을 신규 아이콘 수로 세지 않는다.
- 신규: currency/clock/fire/person/eye_open/eye_closed/copy/megaphone/delivery/lock/skull/flag/star/thumb_down/phone/schedule/medical/tools/bread/water/bandage/backpack.
- settings/close는 buttons_v1, warning은 panels_v1의 기존 Sprite 정본을 읽기 전용 참조한다. 새 키트 중복3종도 보존한다.
- `Assets/Resources/Prefabs/UIs/CommonIcons/CommonIconCatalog.asset`: 25개 key/label/Sprite 직접 참조. `GetSprite(key)`로 사용하며 미등록 키는 예외다.
- `Assets/Resources/Prefabs/UIs/CommonIcons/CommonIcons.prefab`: dark25 + light직접25 + 받침위25, 총75 Image 비교 보드. 실제 HUD 완성품이 아니다.
- `Assets/Scenes/Tests/CommonIcons/CommonIcons.unity`: 독립 확인 씬. Play 후 하단 다음 색/다음 크기/초기화, 배낭 아이콘의 샘플 버튼 입력 횟수를 확인한다.
- Controls/Preview·입력 도구는 씬 전용. Settings 정본은 수정하지 않고 별도 인스턴스의 아이콘만 override한다.
- 원본64×64 RGBA/@2x/흰 실루엣/사방8px 여백, 기본 표시32×32 UI px. Sprite Single/FullRect/PPU200/중앙/border0.
- 신규22 importer: sRGB/Bilinear/Clamp/Uncompressed/noMip/noReadWrite/NPOTNone/MaxSize64. 기존3 importer/GUID는 변경하지 않는다.
- hover/pressed/disabled 전용 그림 없음. 같은 Sprite의 `Image.color`를 변경하며 아이콘은 Simple/preserveAspect/raycastOff다.
- 기본#F0EEDC, 성공#73C7A1, 위험#E06C78, 경고#F2B45B, 비활성#AAB0AD, 단원#4C8DDB/#DE9253/#CDA6D8/#BAE5DA, 흰색. 검사 크기16/24/32/48.
- eye_open/eye_closed는 서로 다른 의미의 그림이다. 비활성 tint는 게임 기능의 비활성 처리와 무관하다.
- Canvas Overlay/1920×1080/Match0.5/referencePPU100. 밝은 배경에서 기본색 직접 표시는 저대비이므로 받침과 비교한다. 임의 아트 보정하지 않는다.
- 전용 Static Medium 폰트: `Assets/Resources/Fonts/CommonIcons/`. 기존 Static 폰트 글리프가 부족해 생성했으며 atlas3장이다.
- 조립: `Assets/Scripts/Systems/CommonIcons/Editor/CommonIconsProjectBuilder.cs`, 메뉴 `Tools/Curtain Call/공용 아이콘/프리팹과 확인 씬 생성`. 기존 출력·dirty 씬 덮어쓰기 거부.
- 제공 조립기 namespace는 `CurtainCall.UI.IconsV1.Editor`, private 실제 경로/정본 참조/안전 진입점만 보정했다.
- 전체81실파일(meta 제외)/PNG53을 보존한다. Source 생성22+초기이력3, Tools6·Docs·시트·검토목업·SVG·폰트·라이선스는 미래 제작/확장용이다.
- 원본·도구·검토자료는 런타임 미참조라는 이유로 삭제하지 않는다. 폰트 OFL은 아이콘 전체 라이선스의 근거가 아니다.
- 전수 경로·SHA256·GUID·실사용 정본·향후 사용 후보: `Tasks/Done/Task-20261019-061-cheer-gauge-ui/icons-inventory.csv`.
- 검증: 같은 Task의 `icons-verification.md`. 실게임·네트워크 연결 없음. 사람 최종 그림 승인과 합성 입력 검사를 구분한다.
