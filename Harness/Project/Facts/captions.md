---
summary: C9 자막·화자·방송 정본과 수용 검사·독립 확인 씬·원본 보존
status: active
updated: 2026-10-20
source: Task-20261019-061
---
# C9 자막·화자·방송
## 언제 읽나
자막 표시·폭 조절·긴 발화 처리·확인 씬 실행 전에 읽는다. 오디오/게임 발화 정책이 아니다.
## 정본·구현
- 프리팹 `Assets/Resources/Prefabs/UIs/Captions/Captions.prefab`, 씬 `Assets/Scenes/Tests/Captions/Captions.unity`, StaticMedium `Assets/Resources/Fonts/Captions/`.
- 공개 `Assets/Scripts/Systems/Captions/CaptionPreview.cs`의19버튼은 수동 합성 예시. 배경·시간 정지/복구 포함, 게임 입력/오디오 연결 없음.
- 안전 어댑터 `Assets/Scripts/Systems/Captions/Editor/CaptionProjectBuilder.cs`는 기존 출력·dirty 씬·Play·PrefabStage·컴파일/import 충돌을 거부한다. 기존 정본 재생성/삭제 없음.
- private 제공 `Assets/_ThirdParty/curtaincall_captions_v1/`의 Runtime/Builder를 원위치 `CurtainCall.UI.CaptionsV1`로 격리. 공개 복제 금지.
## 표시 API
- `CaptionView.TryShowDialogue(speaker, text)` / `TryShowBroadcast(speaker, text)`는 bool 반환. 방송 빈 화자는 `방송`, 대사 빈 화자는 화자 그룹 숨김/본문 확장.
- `CanFit(CaptionKind, speaker, text)`는 현재 TMP로 측정하고 기존 문구/메시·크기·표시 상태를 복원한다. `TrySetWidths(strip, speaker)`는 strip960~1536/speaker128~320 UI, 저장된 본문이 새 폭에 맞을 때만 적용.
- 빈 본문·두 줄/높이64UI 초과·무효값은 실패/현재 발화 보존, `LastIssue` 확인. 자동 축소·자동 분할은 없다. 본문 개행 유지·화자 개행/제어문자는 공백, 태그·리터럴 역슬래시 그대로.
- `Hide()`는 퇴장 후 비활성 자식에 문구가 남을 수 있다. 즉시 폐기는 `HideImmediate()`/`ResetForRuntime()`. 편집·비활성/persistent 자산의 런타임 API는 거부한다.
- 최초 런타임 초기화에서 TMP 비직렬화 `maxVisibleLines`를 다시 적용한다. 선행 Awake 호출은 enabled+activeInHierarchy로 검사하고 이후 Awake가 새 표시를 지우지 않는다.
## 규격·보존
- 기본1120×88/하단중앙 pivot(.5,0)·(0,32). 화자176×48/(24,20), 본문872×64/(224,12), 화자 없음1072×64/(24,12). 이름Medium18 한 줄/본문24 두 줄·NoAutosize/Ellipsis/NoRichText.
- Image9/TMP2, 재사용 Notifications4+PerformanceHud4+CommonIcons1 기존 정본 읽기 전용. 원본49실파일/PNG13·전체 Source/Tools/Docs/Previews/TTF/라이선스 유지.
- unscaled120/100ms/EaseOutCubic/아래4UI·반복 비재시작/반전 현재값 유지. 안정 루트 유지·Motion 자식만 숨김.
- Canvas1920×1080/Match.5/PPU100/Sprite200/sorting50, 모든 자막 Graphic 입력 비차단. 별도 Controls100만 입력 처리. C7과 다른 슬롯으로 동시 표시 가능.
- 큐/노출 타이머/우선순위/오디오 동기·게임/네트워크 연결 없음. reload 설정 변경·both OFF 지원 주장은 없다. 사람 외형·공동 검증은 자동 검사와 별개.
- Static 폰트는 조립 시 포함한 문자 집합만 표시한다. 다른 UI와 동시 확인할 때도 각 UI의 기존 예시 문구를 쓰며 임의 글자의 지원을 가정하지 않는다.
- 전수·최초 실패·실행 결과는 Task의 `captions-inventory.csv`, `captions-verification.md`. C9 제작 때 미커밋이었으며 후속 PM commit/push 요청으로 저장 승인, private `bab2b062`/main `2b67e36d` push 완료. 공유 Bold 기존 dirty(최초 C9 baseline 대비 변화0)/기존 Tooltip 회귀 실패는 별도 미결이며 C9 통과로 해소하지 않는다.
