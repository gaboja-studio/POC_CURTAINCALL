---
summary: 메뉴·나가기 확인 패널 정본·private 의존성·조립기. 패널 배치·수정 전에 읽기
status: active
updated: 2026-10-20
source: Task-20261019-061, PM 패널 동작 확인·저장·정리 승인(다른 패널 제작 자료 보존)
---

# 기본 패널

## 언제 읽나

메뉴·나가기 확인창을 배치하거나 패널 키트·폰트 의존성을 변경할 때.

## 사실

- 정본 `Assets/Resources/Prefabs/UIs/BasicPanels/BasicPanels.prefab`은 Canvas·컨트롤러·메뉴·확인창을 함께 보존한다. 확인 씬 `Assets/Scenes/Tests/BasicPanels/BasicPanels.unity`은 연결 인스턴스1개·EventSystem1개를 사용한다.
- 메뉴384×400/버튼320×56, 확인창512×304/중앙Y=-64/버튼216×56. `60_Panels/Menu_C13`, `70_ConfirmDialogs/LeaveConfirmation` 레이어를 유지한다. 임의 너비 변경은 판의 9-slice만 늘리며 내부 콘텐츠 재배치는 별도다.
- Canvas Overlay1920×1080/Match0.5/referencePPU100. 패널 PNG7장은 private `Assets/_ThirdParty/curtaincall_panels_v1/Assets/CurtainCallUI/BasicPanels/Sprites/`에 있다. PPU200, 판5종 border64, divider(16,12,16,12), icon0. Body alpha0.92.
- 제공 Runtime은 `CurtainCall.UI.PanelsV1`, 동봉 버튼은 `CurtainCall.UI.PanelsV1.BundledButtons`로 격리했다. 실제 버튼5개는 기존 CommonButtons 정본의 중첩 인스턴스이며 기존 정본을 변경하지 않는다.
- 패널 전용 Static TMP Medium/SemiBold는 `Assets/Resources/Fonts/BasicPanels/`. 기존 폰트의 한글 누락 때문에 분리했으며 버튼 Label.font만 인스턴스 override한다. 신규 문구는 글리프 포함 여부를 확인해야 한다.
- 제공 코드·PNG·Source/Fonts의 Medium/SemiBold TTF·LICENSE와 .meta/GUID를 private에 유지한다. 확인 씬 IMGUI 설명에는 Medium TTF가 필요하다. 기존 버튼 키트의 Runtime·PNG 의존성도 함께 필요하다.
- 제공 Builder의 `BuildProject(medium, semiBold, initiallyShowMenu)`를 공개 `Assets/Scripts/Systems/BasicPanels/Editor/BasicPanelsProjectBuilder.cs`가 호출한다. 기존 출력·미저장 씬·Play·PrefabStage·컴파일 실패 때 생성을 거부하며 전역 SaveAssets는 사용하지 않는다.
- 프리팹은 실행 시작 시 닫힘, 확인 씬만 showMenuOnStart=true override. 편집 미리보기에는 메뉴가 보인다. 실제 게임에서 표시하려면 컨트롤러 Show를 호출한다.
- 확인창은 돌아가기에 먼저 포커스하고 메뉴 버튼·푸터만 숨긴다. Cancel은 확인창→메뉴, 메뉴→닫기. 전역 ESC 감시·pause·커서 제어·전면 dim은 없다. 등장160ms/퇴장120ms/8px/unscaled.
- 설정·나가기 확정은 UnityEvent 신호만 제공한다. 실제 설정·사망·방 이탈·씬 전환은 연결하지 않았다. 씬 전용 BasicPanelsPreview가 재열기/닫기/카운터를 제공한다.
- 2026-10-20 PM 정상 동작 확인 후 저장·미사용 자료 정리 승인. 검토 PNG4개와 중복 README_UNITY_ONLY.md만 정리 대상이며, 현재 설치는 제공 README의 옛 경로/27PNG 직접 조립이 아니라 위 wrapper·7PNG·기존 버튼 정본 참조 방식이다.
- 다른 패널 제작용 Source 전체(Generated도 캐시가 아닌 생성 원본), Fonts/Bold·WOFF·LICENSE, Tools6개, SVG4개, 투명 layer sheet, 동봉 CommonButtons20PNG/코드, Docs/구조화 명세는 보존한다. 런타임 미참조라는 이유만으로 지우지 않는다.
- 제거한 검토 PNG는 Tools/render_previews.cjs로 보존 SVG에서 재렌더 가능(sharp 필요). 새 그림 제작은 export_panels.cjs→write_manifest.py→validate_panels.cjs, 검토는 build_panel_previews.py(fontTools)→render_previews.cjs. 재패키징 전 PNG 재렌더·검증을 선행한다(Python Pillow도 필요).
- Docs의 export_report.json·sheet_layout.json·resource_validation.json은 각각 제작/검증/패키징 입력이므로 보고서라는 이름만 보고 삭제하지 않는다. Source·SVG 원본을 Harness 요약으로 대체하지 않는다.
- 정리 완료: 검토 PNG4개·중복 README와 meta 총10파일/33,893,505 bytes 제거. 정리 직후160파일(루트 meta 포함) SHA256 불변; Git 저장 후133파일 바이트 동일·27텍스트는 자동 CRLF 변환뿐이며 내용 불변. private main `a971b415`에 보존 자료 저장·push 완료.
- 동봉 BundledButtons 조립기는 이식 참고용이며 RootPath가 옛 설치 경로다. 현재 프로젝트 버튼은 별도 CommonButtons 정본과 그 wrapper로 만든다. 다른 패널 제작 시 경로·글리프·콜백을 다시 검증한다.

## 확인 방법

- PlayMode `BasicPanels` 필터3건: 규격·폰트·콜백, timeScale0 전환·중복 신호, 합성 InputSystem 마우스/Enter/Escape. 사람 직접 입력 검사와 구분한다.
- 확인 씬 Play에서 메뉴/확인창 실제 Overlay를 확인한다. 1920×1080·1600×1000과 판 너비640 임시 확장 검사 완료. 임시 변경은 저장하지 않고 원래 화면 프리셋으로 복원했다.
- 상세 결과는 Task-061 `panels-verification.md`, 기존 UI는 `common-buttons.md`·`c1-cheer-gauge.md` 참고.
