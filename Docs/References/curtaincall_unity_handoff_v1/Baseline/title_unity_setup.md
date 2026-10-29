# T1/T2/T3 타이틀 메뉴 — Unity 조립 안내

버전: `1.0.0-review` · 기준 해상도: **1920×1080** · C안 **무대감독 계기판**

이 패키지는 PNG와 C# 조립기 소스입니다. 실제 Unity 씬·프리팹은 사용자의 에디터에서 조립 버튼을 눌러 생성합니다. 이 제작 환경에서는 Unity 임포트, C# 컴파일, Play Mode를 실행하지 않았습니다(`not_run`).

## 1. 프로젝트에 넣는 순서

1. ZIP을 풀고 패키지의 **`Assets/CurtainCallUI/` 전체를 프로젝트의 같은 경로에 병합**합니다. `TitleMenu` 외에 `CommonInputs`, `CommonButtons`, `BasicPanels`도 필요합니다. 폴더 전체를 다른 이름으로 한 겹 더 감싸지 마세요.
2. 같은 경로의 기존 공용 PNG와 `CommonButtons/Runtime/CurtainCallButton.cs`는 승인본과 바이트가 같습니다. 다른 폴더에 중복 복사하면 C# 클래스 중복 오류가 생길 수 있으므로 같은 경로를 사용합니다. 기존 `.meta`는 유지하세요.
3. 프로젝트의 uGUI와 TextMeshPro, **Pretendard Medium / SemiBold / Bold TMP Font Asset**을 준비합니다. 패키지가 다른 글꼴을 대신 고르지 않습니다. 전체본 `Source/Fonts/`에는 참고용 TTF와 라이선스가 있으며, Unity 적용용 ZIP에서는 프로젝트가 이미 사용하는 TMP 폰트를 연결합니다.
4. 일반 씬을 연 뒤 **`Tools > Curtain Call UI > 타이틀 메뉴 만들기`**를 누릅니다.
5. 세 TMP 폰트 필드를 연결하고 **`1. PNG 23개 규격 확인`**, **`2. 임포트 + 타이틀 레이어 조립`** 순으로 누릅니다.
6. 생성된 **`T1_TitleMenu`**를 선택합니다. Inspector의 **타이틀 / 방 코드 / LAN / 숨김**으로 각 레이어를 미리 봅니다. 편집 모드 미리보기는 네트워크 요청 이벤트를 보내지 않습니다.
7. 실제 타이틀 진입 코드의 `Start()` 등에서 `ShowMain()`을 호출합니다. **Play Mode 초기 상태는 항상 `Hidden`**입니다. 에디터에서 열린 미리보기 때문에 게임플레이 화면에 딤이 켜지지 않게 하기 위한 규칙입니다.

처음에는 부모 Canvas를 비워 새 검토 Canvas를 만들 수 있습니다. `새 Canvas 프리팹도 고유 이름으로 저장`을 선택하면 Unity가 참조가 연결된 프리팹을 `Assets/CurtainCallUI/TitleMenu/Prefabs/`에 저장합니다. 기존 프리팹을 덮어쓰지 않습니다. 씬은 자동 저장하지 않으며 생성한 씬 오브젝트는 Undo할 수 있습니다. PNG 임포트 설정 변경은 별도로 유지됩니다.

## 2. Canvas와 레이어 순서

| 항목 | 값 |
|---|---|
| Render Mode | Screen Space Overlay |
| Canvas Scaler | Scale With Screen Size |
| Reference Resolution | 1920×1080 |
| Match Width Or Height | 0.5 |
| Canvas Reference Pixels Per Unit | 100 |
| Sprite Pixels Per Unit | 200 |
| Image Pixels Per Unit Multiplier | 1 |
| 배치 | `Canvas/60_Panels/T1_TitleMenu` |
| 로고·페이지 기준 영역 | 중앙의 1920×1080 `10_LayoutStage` |
| 기준 영역 축척 | `min(1, viewportWidth/1920, viewportHeight/1080)` |
| 타이틀 딤 | 검정, alpha 0.6, 전체 뷰포트, 타이틀 루트 전용 |

기존 Canvas를 지정하면 조립 전에 해상도·PPU·그룹 순서·중복 타이틀·마스크·레이아웃·독립 정렬 Canvas 충돌을 검사합니다. 기존 그룹의 순서는 바꾸지 않고 `60_Panels`가 없으면 계약 순서에 맞춰 새 그룹만 삽입합니다. 기존 Canvas에는 활성 `GraphicRaycaster`가 필요합니다. 이 조립기는 **EventSystem과 InputModule을 자동 생성하지 않습니다.** 프로젝트에서 사용하는 입력 시스템을 연결해야 클릭, 키보드 이동과 문자 입력이 동작합니다.

딤은 `00_Visuals/00_TitleDim` 하나입니다. `BasicPanels` 공용 스프라이트나 일반 게임플레이 메뉴 안에 포함되지 않습니다. 타이틀 진입 시 이 루트를 표시하고, 게임플레이로 전환할 때 `Hide()`를 호출하세요. 다른 Canvas의 정렬이나 C16의 씬 전환 완료 시점은 게임에서 관리합니다.

## 3. 실제로 쌓이는 레이어

`title_assembly.json`에 모든 오브젝트 이름, 부모, 형제 순서, RectTransform, 이미지·TMP 설정이 들어 있습니다. `resource_list.md`에는 PNG별 9-slice와 피벗이 있습니다.

| 조립 부분 | 뒤에서 앞으로 쌓이는 순서 | 설명 |
|---|---|---|
| 전체 | `00_TitleDim` → `10_LayoutStage` | 딤은 화면에 맞추고 기준 영역은 비율을 유지합니다. |
| 로고 `00_Brand` | `00_Shadow` → `10_Logo` | 두 PNG를 같은 사각형에 등록합니다. 그림자는 아래로 3 UI px. |
| 메인 메뉴 한 줄 | `00_HitTarget` → `10_Content` → `20_Focus` | 투명 히트 영역과 독립 키보드 포커스입니다. |
| 메뉴 Content | `00_LabelTMP` → `10_Underline` | 문구는 TMP, 밑줄은 흰색 마스크에 황동색을 입힙니다. |
| 공용 입력 패널 | `00_Shadow` → `10_Body` → `20_Frame` → `30_Corners` → `40_Content` | 기존 공용 패널 4개 PNG를 그대로 재사용합니다. |
| 입력 패널 Content | 제목 → 설명 → 구분선 → 필드 이름 → 입력 → 피드백 → 뒤로 → 확인 | 글자와 이미지가 각각 독립 오브젝트입니다. |
| 입력 필드 | `00_Body` → `10_TextViewport` → `20_Focus` | Body 4상태, TextViewport 안 TMP와 Placeholder, 독립 Focus. |
| 공용 버튼 | `00_Shadow` → `10_Body` → `20_Content/00_LabelTMP` → `30_Focus` | 승인된 확인·취소 버튼을 재사용합니다. |

그림자 색은 `#05090B`, alpha 0.32입니다. 로고를 제외한 패널·버튼 그림자는 아래로 1 UI px입니다. 몸체 이미지 alpha는 0.92이고, 비활성 글자는 `#AAB0AD`, alpha 1을 유지합니다. 전체 CanvasGroup을 흐리게 만들어 비활성을 표현하지 않습니다.

### 위치 표 — 왼쪽 위 기준 UI px

| 대상 | x | y | 너비 | 높이 |
|---|---:|---:|---:|---:|
| 메인 로고 | 576 | 48 | 768 | 384 |
| 메인 메뉴 첫 줄 | 780 | 488 | 360 | 56 |
| 이후 메뉴 줄 | 780 | `488 + 72 × 순번` | 360 | 56 |
| 메뉴 줄 안 글자 | 0 | 0 | 360 | 48 |
| 메뉴 줄 안 밑줄 | 52 | 48 | 256 | 8 |
| 하위 페이지 로고 | 768 | 88 | 384 | 192 |
| 하위 페이지 패널 | 624 | 328 | 672 | 432 |
| 패널 안 제목 | 48 | 36 | 576 | 40 |
| 패널 안 설명 | 48 | 88 | 576 | 32 |
| 패널 안 구분선 | 32 | 120 | 608 | 16 |
| 패널 안 필드 이름 | 48 | 160 | 576 | 24 |
| 패널 안 입력 필드 | 48 | 192 | 576 | 56 |
| 패널 안 피드백 | 48 | 260 | 576 | 48 |
| 패널 안 뒤로 | 48 | 328 | 264 | 56 |
| 패널 안 확인 | 360 | 328 | 264 | 56 |

메인 메뉴는 방 만들기 / 방 코드로 참가 / LAN 접속 / 설정 / 종료의 예시 문구를 사용합니다. 같은 입력 패널을 방 코드·LAN 모드에서 함께 사용하고 문구만 바꿉니다. 입력 안쪽은 좌우 16, 위아래 8 UI px만큼 비웁니다. 글자 크기는 도움말 18, 본문·입력 24, 메뉴·버튼 28, 패널 제목 32입니다.

## 4. PNG 임포트와 9-slice

| 항목 | 설정 |
|---|---|
| Texture Type / Mode | Sprite (2D and UI) / Single |
| Mesh Type / Pivot | Full Rect / (0.5, 0.5) |
| Alpha | From Input, Alpha Is Transparency 켜기 |
| sRGB | 켜기 |
| Mipmap | 끄기 |
| Filter / Wrap | Bilinear / Clamp |
| Non-Power of Two | None |
| Compression | None, Crunch 끄기 |
| 크기 | 원본보다 작게 제한하지 않기 |

수동 설정 시 `resource_list.md`의 **left / right / top / bottom**은 **@2x 원본 픽셀**입니다. Unity C#의 `spriteBorder` 벡터 순서는 **left / bottom / right / top**이므로 조립기가 이를 변환합니다.

| 유형 | 원본 px | 9-slice L / R / T / B | Image Type |
|---|---|---|---|
| 로고·로고 그림자 | 1536×768 | 0 / 0 / 0 / 0 | Simple, 비율 유지 |
| 메뉴 밑줄 | 512×16 | 16 / 16 / 0 / 0 | Sliced, 가로만 늘림 |
| 입력 4상태·Focus | 512×96 | 32 / 32 / 20 / 20 | Sliced |
| 공용 패널 4겹 | 256×256 | 64 / 64 / 64 / 64 | Sliced |
| 패널 구분선 | 256×32 | 16 / 16 / 12 / 12 | Sliced |
| 확인·취소·그림자·Focus | 384×112 | 32 / 32 / 24 / 24 | Sliced |

조립기는 기존 플랫폼별 오버라이드를 지우지 않습니다. 활성 플랫폼에서 원본 크기·RGBA 비압축·PPU·피벗·9-slice가 다르게 적용되면 조립을 중단하고 원인을 표시합니다. 입력은 최소 160×48, 버튼은 최소 128×56 UI px를 지켜주세요. 본 패널은 672×432입니다.

## 5. 상태와 표시 연출

| 상태 | 동작 |
|---|---|
| Normal | 기본 글자·입력 바탕 |
| Hover | 입력 바탕 교체, 메뉴 글자 흰색과 밑줄 표시 |
| Pressed | 입력 바탕 교체, 메뉴·버튼 Content만 아래로 1 UI px |
| Disabled | 비활성 바탕과 불투명 회색 글자, hover·press 중지 |
| Selected / Focus | 위 4상태와 별도로 테두리 표시; 메뉴는 밑줄도 유지 |
| Busy | 입력·확인 비활성, 뒤로는 활성 |

메뉴 글자·밑줄·포커스의 hover 전환은 80ms, 눌림은 50ms입니다. 입력 필드 안의 글자와 캐럿은 눌러도 이동하지 않습니다. 타이틀 전체 진입과 페이지 진입은 160ms, 숨김은 120ms이며 `unscaledDeltaTime`을 사용합니다. 숨김 요청 즉시 입력과 Raycast를 해제하고 남은 시각 요소만 페이드합니다.

Busy 진입 전에 입력 또는 확인 버튼이 선택되어 있었다면 선택을 **뒤로**로 옮깁니다. 비활성 전환으로 EventSystem 선택이 사라져 키보드로 빠져나올 수 없는 상황을 막습니다. Busy 해제 후에는 현재 선택을 유지하며, 뒤로에서 위 방향으로 입력 필드에 돌아갑니다. 확인 버튼이 비활성일 때 뒤로의 오른쪽 이동 대상에서는 제외합니다. 실제 문자 편집 중 키 처리와 Cancel 입력은 프로젝트 InputModule 및 TMP의 기본 동작을 따릅니다.

## 6. 게임 코드 연결

### 공개 메서드와 상태

| API | 역할 |
|---|---|
| `ShowMain()` | 타이틀 메뉴 열기 |
| `ShowRoomCode()` | 방 코드 입력 페이지 열기 |
| `ShowLan()` | LAN 주소 입력 페이지 열기 |
| `Hide()` | 타이틀 전체 숨김, 입력 소유 선택 해제 |
| `Back()` | 입력 페이지에서 메인으로 돌아가기 |
| `SetFormBusy(bool)` | 연결 요청 중 입력·확인 잠금/해제 |
| `SetFeedback(string, bool isError)` | 호출자가 제공한 일반/오류 문구 표시 |
| `CurrentMode` | Hidden / Main / RoomCode / Lan |
| `IsFormBusy` | 현재 입력 페이지 요청 대기 여부 |
| `FormVersion` | 페이지가 바뀌거나 닫힐 때 달라지는 번호. 늦은 비동기 응답의 적용 여부 판단에 사용 |

### 이벤트

| 이벤트 | 게임에서 연결할 행동 |
|---|---|
| `CreateRoomRequested` | 방 생성 흐름 |
| `JoinRoomRequested(string)` | 전달된 코드로 방 참가 요청 |
| `LanConnectRequested(string)` | 전달된 주소로 LAN 접속 요청 |
| `SettingsRequested` | 이후 제작할 설정 페이지 열기 |
| `QuitRequested` | 프로젝트의 종료 흐름 |
| `FormClosed` | 입력 폼이 닫히거나 다른 모드로 전환될 때 해당 요청 취소/무효화 |

참가·접속 클릭 또는 TMP Submit 이벤트는 **busy를 먼저 설정한 후 한 번 호출**합니다. 같은 프레임에 Enter와 확인 요청이 겹쳐도 중복 전송을 차단합니다. 콜백 뒤에는 이 컴포넌트가 UI 상태를 덮어쓰지 않습니다.

접속이 완료되면 게임이 `Hide()`를 호출하고 이후 흐름을 진행합니다. 실패하면 `SetFeedback("연결하지 못했습니다.", true)`와 `SetFormBusy(false)`로 재시도를 허용하세요. 비동기 결과를 반영하기 전에 요청 때 저장한 `FormVersion`과 현재 번호가 같은지, 현재 모드가 요청 모드인지 확인합니다. `FormClosed`에서 프로젝트의 취소 토큰이나 네트워크 요청도 정리하세요.

**요청 이벤트에 아무 동작도 연결하지 않은 상태에서 참가·접속을 누르면 Busy가 유지됩니다.** 조립기가 가짜 접속 성공이나 실패를 생성하지 않습니다. 이때도 뒤로 버튼은 사용할 수 있습니다. 방 생성·설정·종료도 연결 전에는 요청 이벤트만 발생합니다.

방 코드의 자리수·대문자 규칙·LAN 주소 형식은 게임 규칙이 확정되지 않아 강제하지 않습니다. 빈 문자열·공백만 있는 값은 제출할 수 없고, 제출할 때 바깥 공백만 제거합니다. 예시 `ABCD12`, `192.168.0.10`은 입력 검증 규칙이 아닙니다.

`TitleCancelRelay`는 입력·뒤로·확인 오브젝트에만 붙어 프로젝트가 보낸 uGUI Cancel을 `Back()`으로 전달합니다. 전역 ESC나 다른 키를 폴링하지 않습니다. 게임 자체에 전역 뒤로 기능이 있으면 같은 입력을 이 경로와 중복 연결하지 않도록 입력 소유 관계를 정하세요.

## 7. 에디터에서 확인할 항목

- 조립 후 Console의 C# 컴파일 결과와 Sprite 원본 크기·알파·9-slice 설정을 확인합니다.
- 편집 모드에서 세 화면을 열고 로고·그림자·모서리·입력·TMP가 독립 레이어인지 확인합니다.
- Play 시작에는 숨겨져 있는지, `ShowMain()`으로 표시되는지 확인합니다.
- 입력이 비어 있으면 확인 비활성, 문자를 입력하면 활성인지 확인합니다.
- 콜백 요청이 한 번만 발생하는지, Busy 중 뒤로/Cancel을 사용할 수 있는지 확인합니다.
- Busy 실패 해제 후 재시도, 입력 페이지를 닫은 뒤 도착한 늦은 응답을 확인합니다.
- 사용하는 창 비율에서 중앙 기준 영역이 잘리지 않는지, 다른 Canvas/C16과 정렬이 맞는지 확인합니다.
- 한글 입력·IME·키보드/게임패드 이동은 실제 프로젝트 InputModule과 플랫폼에서 확인합니다.

위 항목은 **사용자 Unity 프로젝트에서 수행할 확인 목록**입니다. 이 패키지의 소스 정적 검토 결과를 실제 엔진 테스트 통과로 해석하지 마세요.

## 공식 API 참고

- [Unity TextMeshPro — TMP_InputField](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/api/TMPro.TMP_InputField.html): 네이티브 입력·textViewport·onSubmit·SetTextWithoutNotify 계약 확인.
- [Unity — TextureImporter.spriteBorder](https://docs.unity3d.com/ScriptReference/TextureImporter-spriteBorder.html): 9-slice 원본 경계 설정.
- [Unity uGUI — Navigation](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-SelectableNavigation.html): Selectable의 명시적 이동 연결.

소스·API 검토일: 2026-10-20. 실제 사용 Unity 및 uGUI/TMP 버전에서의 컴파일·기기 검증은 `not_run`입니다.
