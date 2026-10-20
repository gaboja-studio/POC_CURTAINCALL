---
summary: C7 조작 가이드·키캡·마우스 표시 API와 독립 확인 씬·원본 보존
status: active
updated: 2026-10-20
source: Task-20261019-061
---
# C7 조작 가이드
## 언제 읽나
가이드 표시/숨김·상위 HUD 차단·샘플 확인·재제작 전에 읽는다. 게임 입력 바인딩 규격은 아니다.
## 정본
- 프리팹: `Assets/Resources/Prefabs/UIs/Guides/Guides.prefab`.
- 씬: `Assets/Scenes/Tests/Guides/Guides.unity`; 공개 `GuidePreview`의17버튼으로 수동 합성 표시. 상태 문구는 top-left 고정·배경별 대비색이며 게임 HUD가 아니다.
- 공개 어댑터: `Assets/Scripts/Systems/Guides/Editor/GuideProjectBuilder.cs`; 기존 출력/dirty 씬/Play/PrefabStage/컴파일·임포트 충돌을 거부한다. 기존 정본 재생성·삭제 없음.
- private 키트: `Assets/_ThirdParty/curtaincall_guides_v1/`; Runtime/Builder 원위치 `CurtainCall.UI.GuidesV1` 격리, 공개 복제 금지.
## 표시 API
- `GuideView.ShowKey(keyLabel, actionLabel, GuideKeyWidth.Normal|Wide|ExtraWide)` / `ShowMouse(GuideMouseInput.LeftClick|RightClick|Move, actionLabel)`는 bool 반환.
- 편집/비활성/미설정/무효enum은 내용 보존·거부. 유효 빈 문구는 즉시 숨김. 실제 제어문자·줄 구분자는 공백, 태그·리터럴 역슬래시는 그대로.
- `SetContextBlocked(true)`는 즉시 문구 폐기/숨김·Show 거부. false는 자동 복원 없음. 상위 C11/P1/W6 차단 OR는 외부 호출자 책임이며 아직 게임에 연결하지 않았다.
- `Hide()`는 퇴장·비활성 자식 문구 잔류 가능; `HideImmediate()`는 내용 폐기·차단 유지. 새 세션 `ResetForRuntime()`은 차단도 해제한다. 비활성화는 자동 복원하지 않는다.
- `HasRuntimePresentation`은 유효 Show 첫 alpha0부터 Hide 전까지. `IsVisible`은 Motion 활성/자체 alpha 기준으로 부모 전체 가시성을 보증하지 않는다.
## 규격
- 544×80/하단중앙(0,176), 키40/96/144×40·마우스32×32/이동 간격8, Image8/TMP2.
- Medium24/NoWrap/Ellipsis/자동 축소·richText·parseCtrlCharacters off. Static 전용 폰트, 공유 Dynamic 참조/수정 없음. 새 문자열은 glyph 검증이 필요하다.
- 원본 키 글자 높이28은 실제 TMP 필요 높이28.63보다 작아 F가 사라졌다. 키캡/폰트/위치는 유지하고 글자 영역 높이만32로 보정했다.
- unscaled120/100ms/아래4UI/EaseOutCubic, 반복 비재시작·반전 현재값 유지. stable root 유지·Motion만 숨김.
- Canvas1920×1080/Match.5/PPU100/Sprite200/sorting1, 입력 비차단·Mask 밖. Controls10만 raycaster와 입력 시스템 사용.
## 보존·한계
- 원본54실파일/PNG19 전체 보존. 생산14=신규8+PerformanceHud 정본6, 정본6 SHA/규격 읽기 전용 확인. 변경은 제공 코드3·신규8 importer meta뿐이다.
- 입력 감지·자동 노출/우선순위/수명·게임/네트워크 연결 없음. Preview 시간 정지는 합성 검사용이고 종료/비활성/예외 시 원래 timeScale 복구.
- reload 설정 변경·both OFF 지원 주장은 없다. 사람 외형 승인·공동 플레이와 자동 검증은 별개.
- 상세 최초 실패/검증/전수 목록: Task의 `guides-verification.md`, `guides-inventory.csv`. 2026-10-20 PM 직접 재승인 후 private8b49cc29/main38001b8e 및 공개9216489 저장·push 완료, 공개 원격 SHA 일치 확인. C9는 이후 별도 미커밋 작업이다.
