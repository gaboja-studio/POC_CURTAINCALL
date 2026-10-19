---
summary: 공용 버튼4종의 정본·private 의존성·조립기와 확인 방법. 버튼 수정이나 키트 정리 전에 읽기
status: active
updated: 2026-10-20
source: Task-20261019-061, PM UI 리소스 작업 확장·동작 확인 후 정리 승인
---

# 공용 버튼

## 언제 읽나

확인·취소·설정·일반 버튼을 배치하거나 제공 키트를 정리할 때.

## 사실

- 정본: `Assets/Resources/Prefabs/UIs/CommonButtons/Button_{Confirm,Cancel,Settings,General}.prefab`. 확인 씬 `Assets/Scenes/Tests/CommonButtons/CommonButtons.unity`은 이 프리팹 인스턴스4개를 사용한다. 게임 메뉴에는 아직 연결하지 않았다.
- 기본192×56/최소128×56, Body만 Raycast Target. Shadow/Body/Content(Icon·LabelTMP)/Focus 분리, 취소X·설정톱니. Body alpha0.92, Shadow0.32/Y=-1, foreground alpha1.
- private `Assets/_ThirdParty/curtaincall_buttons_v1/`의 PNG20장과 Runtime/CurtainCallButton.cs, SemiBold TTF가 실행 의존성이다. 원본 그림·코드를 공개 폴더에 복사하지 않는다. .meta/GUID와 OFL LICENSE를 유지한다.
- SemiBold TMP는 `Assets/Resources/Fonts/CommonButtons/Pretendard-SemiBold SDF.asset`. Dynamic 원본 TTF를 지우면 안 된다. 제공 OFL은 폰트 고지이며 그림/코드 공개 허가가 아니다.
- 같은 클래스가 `curtaincall_buttons_unity_only_v1`에도 있어 이번 키트만 `CurtainCall.UI.ButtonsV1` namespace로 분리했다. 다른 키트를 삭제하거나 동시에 같은 namespace로 되돌리지 않는다.
- 제공 builder는 같은 키트 Editor/CommonButtonBuilder.cs. 실제 private PNG 경로·공개 프리팹 출력·연결 저장·BuildProject 진입점을 보완했다. 프로젝트 `Assets/Scripts/Systems/CommonButtons/Editor/CommonButtonsProjectBuilder.cs`가 폰트·단독 씬을 준비한다. 기존 결과 덮어쓰기는 거부한다.
- Sprite Single/FullRect/PPU200, body border L,B,R,T=(32,24,32,24), icon0. Canvas Overlay1920×1080/Match0.5/referencePPU100. 서로 다른 기존 Canvas에 넣으면 크기를 재확인한다.
- 버튼은 uGUI Button 상속으로 onClick/interactable 계약을 유지한다. 4상태 sprite와 독립 Focus, 국소 연출만 제공한다. ESC/Cancel·메뉴 전환·커서·사운드·게임 콜백은 별도 연결 범위다.
- 확인 씬 전용 CommonButtonsPreview는 클릭 횟수와 전체 활성/비활성 조작만 제공한다. 프리팹에는 게임 콜백/확인용 카운터를 저장하지 않는다.
- 2026-10-20 PM 승인 후 미참조 Docs/Previews/Source/Generated·README·Bold/Medium TTF·SemiBold WOFF를 정리했다. 원본34개·파일 meta34개·빈 폴더 meta3개, 20,059,891 bytes(87.65%) 감소. 남은56파일은 2,825,546 bytes(루트 meta 제외). Git 이력·Library·빌드 크기 절감량은 아니다.
- 생성 원본 기반 아트 재가공 자료는 정리했으나 보존 PNG20장으로 재조립할 수 있다. 조립기는 JSON/CSV/WOFF를 읽지 않는다.
- 보존 목록: Sprites20장과 .meta, Runtime/CurtainCallButton.cs·Editor/CommonButtonBuilder.cs와 .meta, Source/Fonts의 SemiBold TTF·LICENSE.txt 및 모든 상위 폴더 .meta. 설정/일반 PNG는 그림이 같아도 GUID/동적 파일명이 달라 합치거나 지우면 안 된다. 2026-10-20 private main c15d9a11에 필수 키트/.meta 저장·push 완료. 사용 전 공개 저장소의 submodule 포인터와 일치하는지 확인한다.

## 확인 방법

- `CurtainCall.CommonButtons` PlayMode 필터: 4종의 상태·Focus·click/Submit/Disabled·재활성, 합성 InputSystem 마우스/Enter 경로 검사. 사람 직접 입력 확인과 구분한다.
- 씬 Play에서 실제 Overlay와 글리프/9-slice를 확인한다. 상태 비교는 Inspector 기능을 사용하되 Normal로 복원하고 저장한다.
