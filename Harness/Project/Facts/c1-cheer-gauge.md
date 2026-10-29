---
summary: C1 호응 게이지의 디자인 정본·필수 private 자산·재생성 한계. 프리팹 수정이나 키트 정리 전에 읽기
status: active
updated: 2026-10-20
source: Task-20261019-061
---

# C1 호응 게이지

## 언제 읽나

호응 HUD를 배치·수정하거나 제공 키트의 파일을 정리할 때.

## 사실

- 디자인 정본: `Assets/Resources/Prefabs/UIs/CheerGauge/C1CheerGauge.prefab`. 확인 씬은 `Assets/Scenes/Tests/CheerGauge/C1CheerGauge.unity`이며 기존 공연 씬에는 아직 배치하지 않았다.
- 사용자 수정: RingRoot 190×190, Roof (0,38), Flag (10,76), Roof 좌우 분할 계층. 초기 제공 문서의 좌표보다 현재 프리팹이 우선한다. 프리팹은 ‘—’ 대기 상태, 합계155는 확인 씬의 샘플이다.
- 키트 루트는 private `Assets/_ThirdParty/curtaincall_c1_v1/`. 필수 파일과 상위 폴더의 `.meta`/GUID를 유지한다. 공개 Assets로 PNG·원본을 옮기지 않는다.
  - `Assets/CurtainCallUI/C1/Sprites/`: `ui_hud_cheergauge_{shadow,center,track,fill,highlight,bezel,roof,flag}_normal@2x.png` 8장.
  - `Source/Fonts/Pretendard-Bold.ttf`: Dynamic Bold TMP의 원본이므로 제거 금지.
  - `Source/Fonts/Pretendard-Medium.ttf`: 확인 씬 Preview 조작용 글꼴. 공용 Static Medium TMP와 별개다.
  - `Source/Fonts/LICENSE.txt`: Pretendard 저작권·SIL OFL 1.1 고지. 그림 전체의 공개 재배포 허가를 뜻하지 않는다.
- PNG는 Sprite/Single, FullRect/중앙 피벗, sRGB, 알파 입력·투명 처리, Bilinear/Clamp, Mipmap 끔, Uncompressed/NPOT None. 파일명 대신 실제 픽셀 크기로 PPU를 계산하며 기존 importer `.meta`를 재사용한다.
- 링은 같은 중심의 Image를 겹치고 Radial360/Top/Clockwise로 연결한다. 0점 구간은 숨기며 장식·텍스트는 회전시키지 않는다. 비상호작용 Graphic의 Raycast Target은 끈다.
- 프로젝트 구현은 `Assets/Scripts/Systems/CheerGauge/`. 보상 상태를 읽기 전용으로 구독하며 1~4명·상한100/150/200·공연 내 명부/색 고정을 지원한다. 실제 추락 신호는 미연결이며 강조 API/샘플만 있다.
- 기존 Canvas로 배치할 때 기준은 Overlay, Scale With Screen Size, 1920×1080, Match 0.5다. 다른 Canvas 규칙이면 크기·앵커를 별도로 확인한다. 기존 개발 배지와의 상단 겹침은 아직 미해결이다.
- 프로젝트 `Editor/C1CheerGaugeBuilder.cs`는 초기 생성용이며 기존 결과물 덮어쓰기를 거부한다. **사용자 수정 디자인을 재생성하지 못한다.** 정리·수정 중 실행하거나 프리팹을 삭제하고 다시 만들지 않는다.
- 제공 중복 코드·문서·Preview·제작 자료 33파일은 2026-10-19 실행 의존성 검증 후 전용 복사본에서 삭제했다. 키트에는 위 필수 11파일과 .meta만 남는다. 메인 폴더의 원본 키트는 보존하지만 원본/내보내기 도구와 이 문서만으로 최신 프리팹 디자인을 복원할 수는 없다.

## 확인 방법

- Unity AssetDatabase 의존성 및 프로젝트 전체 GUID/문자열 경로 참조를 대조한다. 불명확한 동적 참조는 삭제하지 않는다.
- `CurtainCall.CheerGauge` 필터의 EditMode/PlayMode 테스트와 확인 씬의 155·실제 오프라인 환호/야유/사망·서비스 교체를 검사한다. 폰트에 `0123456789호응—`가 필요하다.
- 사람 공동 확인·호스트/참가자 네트워크 검증은 별도다. 로컬 자동 검사로 통과 처리하지 않는다. 필수 키트/.meta는 2026-10-20 private main c15d9a11에 저장·push했다. 사용 전 공개 저장소 submodule 포인터를 확인한다.
