---
summary: 타이틀 로고 정적 프리팹의 두 레이어와 1920×1080 배치·Sprite 계약
status: active
updated: 2026-10-21
source: Task-20261019-061
---

# 타이틀 로고 프리팹

## 언제 읽나
타이틀 로고를 Canvas에 배치하거나 원본/그림자·크기를 바꾸기 전에 읽는다.

## 정본과 의존성
- 프리팹: `Assets/Resources/Prefabs/UIs/TitleLogo/TitleLogo.prefab`.
- 제공 자료는 `Assets/_ThirdParty/curtaincall_title_logo_replacement_v2/`의 **PNG2개 교체 패치**다. 전체 타이틀 메뉴/TitleMenuView/Builder는 동봉되지 않았다.
- Sprite는 위 키트 `Assets/CurtainCallUI/TitleMenu/Sprites/`의 normal/shadow @2x 두 PNG를 직접 참조한다. private submodule 필요, PNG/GUID/Source/Tools/Docs/Previews는 보존한다.
- 두 PNG1536×768 RGBA. Sprite Single/FullRect/PPU200/pivot중앙/border0, alpha입력/투명처리/sRGB On, Bilinear/Clamp, mipmap Off/NPOT None/Uncompressed/Crunch Off/Max2048. 플랫폼 override 없음.

## 배치 계약
- 루트 RectTransform + `00_Shadow`, `10_Logo` Image2개. 자체 Canvas·GraphicRaycaster·EventSystem·TMP·게임 런타임 스크립트 없음.
- 외부 Canvas 권장: Overlay/ScaleWithScreenSize/1920×1080/Match.5/ReferencePPU100. **아래 위치는 1920×1080 LayoutStage의 좌상단 기준**이며 임의 크기 부모에 그대로 적용하지 않는다.
- 루트 anchorMin=anchorMax=pivot(0,1), 메인 anchoredPosition(576,-48)/size768×384. 방 코드/LAN 인스턴스는 (768,-88)/384×192로 조절 가능하며 모드 자동 전환은 없다.
- 두 Image FullStretch/Simple/preserveAspectfalse/raycastTargetfalse/PPUmultiplier1. 그림자 offsetMin=offsetMax(0,-3), #05090B/alpha.32. 본체 offset0/white/alpha1, 본체가 앞이다.
- 2:1 투명 캔버스 여백을 자르거나 하위 배치에서 SetNativeSize를 호출하지 않는다. 본체에 색이 들어 있으므로 tint는 흰색 유지.
- 원본1024×341을1440×480으로 리샘플해 (48,144)에 등록했다. 글자/별/전구는 합쳐진 이미지이며 독립 애니메이션 레이어가 아니다.

## 범위
정적 로고 프리팹 제작만. 기존 타이틀·메뉴 연결/게임 입력·런타임 모드·영구 테스트 씬은 추가하지 않는다. 검증 결과/실패/제약은 Task `title-logo-verification.md`를 따른다.
