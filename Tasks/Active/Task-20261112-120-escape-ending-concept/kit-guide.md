# 노말 엔딩 결과창 키트 v1

승인된 원화의 정보 구조를 기존 UI 레이어로 조립했다. 실제 게임 흐름/배경은 아직 연결하지 않았다.

## 산출물

작업 worktree의 `Assets/_ThirdParty/curtaincall_escape_results_v1/`에 전체 키트와 배포 ZIP을 둔다. `curtaincall-escape-results-v1.unitypackage`가 우선 설치 경로다. `Unity~/`는 Unity가 자동 스캔하지 않는 원본 페이로드다. 해당 폴더를 Assets 안에서 이름만 바꾸지 않는다.

- 재사용 PNG 28장: 기존 결과창/패널/버튼 14장, 탭 6장, 현재 게임 기물 슬롯 아이콘 8장. 원본 바이트 SHA-256 동일, 원본 importer/GUID 불변.
- 신규 PNG 5장: 탈출·미탈출·원형 마스크·초상 테두리 4장과 캐릭터 초상 아틀라스 1장. 아틀라스는 Unity에서 토끼/피에로/소녀/곰 4개 Sprite로 분할한다. 전체 36 Sprite.
- Canvas·플레이어 행·통계 행 프리팹 3개, 독립 미리보기 씬, 조립기, 표시 전용 코드, TMP 폰트 3종과 OFL 라이선스, 규격 manifest, 생성 원본/검사 기록 포함.
- 초상은 기존 캐릭터 자료 기반 생성 원화다. 기물 8종은 게임의 `Resources/ItemArt/Icons/*-slot.png`를 그대로 재사용한다.

## 설치와 확인

1. Unity 6000.3.25f1/uGUI 2.0.0에서 패키지를 Import한다. 기존 TMP Essentials가 있는 프로젝트를 기준으로 하며 패키지는 필요한 shader 의존성을 포함한다.
2. `Assets/Scenes/Tests/EscapeResultsKit/EscapeResultsPreview.unity`를 열어 Play한다. 두 탭을 클릭하고 버튼을 누르면 예시 콜백만 발생한다. 실제 로비로 이동하지 않는다.
3. 본 프리팹은 `Assets/Resources/Prefabs/UIs/EscapeResultsKit/EscapeResultsCanvas.prefab`. 시작은 숨김이다. 프로젝트의 EventSystem/InputModule을 사용하며 본 프리팹에 중복 EventSystem을 넣지 않았다.
4. 조립기는 `Tools > Curtain Call UI > Escape Results > Build kit prefabs`. 이미 출력된 프리팹·씬을 덮어쓰지 않는다. 패키지는 조립 완료 상태이므로 보통 재실행할 필요가 없다.

원본 페이로드를 수동 설치할 때는 `Unity~/Assets`의 내용을 프로젝트 Assets로 복사한다. package Import와 수동 복사를 동시에 하지 않는다. 폴더별 `.meta`도 함께 보존한다.

## 레이어 구조

`EscapeResultsCanvas` → `EscapeResultsPanel` → Shadow / Body / Frame / Corners / Content. Content 안에 TitleRibbon·TitleTMP·SummaryTMP, 두 탭의 Body·LabelTMP·Focus·SelectedUnderline, Players, Statistics, ReturnToLobby의 Shadow·Body·LabelTMP·Focus, HelpTMP가 있다.

플레이어 행은 PortraitMask·Portrait·PortraitFrame, SlotColor, NameTMP, Outcome·OutcomeTMP, ItemSlot 2개(Icon·EmptyTMP), Divider로 분리했다. 미탈출자의 기물을 자동 삭제하지 않는다. 통계는 최대 6개 공연/난이도 행과 최고 기록 카드, 토큰 구역이다.

각 PNG의 크기/PPU/9-slice(L,B,R,T)/출처/해시는 `Unity~/Assets/_ThirdParty/EscapeResultsKit/manifest.json`에 기록했다. Canvas 1920×1080/Match 0.5/referencePPU100, Sprite PPU200, 패널 top-left (40,60), 800×960. 일반 이미지·텍스트는 raycast를 받지 않고 패널 Body와 버튼만 받는다. 오른쪽 배경 영역은 차단하지 않는다.

탭 selected 자산은 버튼 전체 바탕이 아닌 밑줄 전용이다. 원본 PNG의 용도를 그대로 따른다. 테두리와 구분선도 Sprite로 남아 개별 교체할 수 있다. 가변 문구·수치·이름은 TMP이며 이미지에 굽지 않았다.

## 표시 코드 연결

namespace `CurtainCall.UI.EscapeResultsKit`. 표시 데이터는 게임이 제공한다.

- `SetPlayers(EscapePlayerData[])`: 1~4명. 이름, 초상 Sprite, 슬롯 Color, 최종 탈출 bool, 마지막 기물 Sprite 최대 2개. 결과 시점의 복사된 데이터에서 공급한다.
- `SetStatistics(...)`: 최대 6행. 공연/난이도/클리어율/평균 호응 및 최고 기록·개인 초상·획득/소모 토큰 문자열. 값 계산/보상 지급은 하지 않는다.
- `SetReturnAllowed(authority, help)`: 버튼 권한과 대기 안내. 결과를 새로 열거나 재시도할 때 호출한다.
- `SetVisible(true)` / `ShowPlayers()` / `ShowStatistics()`: 표시와 탭 선택. 전역 커서/입력 차단은 게임에서 소유한다.
- `ReturnRequested`: 실제 복귀 어댑터에 연결할 요청 이벤트. 자동 숨김·씬 전환 없음. 숨김/권한 없음/연속 클릭은 요청을 막는다. 실패 후에는 게임이 `SetReturnAllowed(true)`로 재시도를 열 수 있다.

기본 프리팹의 수치·기물 배정은 예시다. 런 통계의 분모·미시도 표시·최고 개인의 동률 규칙은 게임 데이터 계약에서 처리한다. 캐릭터 ID/슬롯 색을 특정 캐릭터로 고정하지 않았다. 실제 프로젝트 연결 시 기존 ItemVisualAssets.Icon으로 같은 기물 Sprite를 공급할 수 있다.

## 배경 연출과 실게임 연결

실제 플레이어의 일반 입력을 잠시 연출 제어로 바꾸고, 기존 이동/달리기 애니메이션으로 달리게 한 뒤 Cinemachine이 정면보다 낮은 위치에서 같은 속도로 후진 추적하는 방법을 먼저 시도한다. Cinemachine 자체가 캐릭터를 움직이는 것은 아니다. 호스트 권한·NGO 동기화·부상 상태·복귀 시 입력 해제 검증이 필요하다.

후속 구현에서 실제 플레이어 방식으로 연결했다. ZIP/UnityPackage는 독립 UI 키트이며 실게임 데이터 어댑터·갈대밭·로비 수정은 작업 브랜치의 별도 코드/씬에 있다. 설치 상태·검사 근거는 [integration-review.md](integration-review.md)를 따른다.

## 검증 범위

독립 프로젝트에서 Unity 컴파일/조립, PNG/폰트 참조와 텍스트 잘림 검사, 1920×1080 두 탭 렌더 확인. Play Mode의 UI raycast와 합성 pointer click으로 19개 항목(탭/입력 모듈/1~4인/빈 슬롯/미탈출 기물 유지/권한/숨김/반복 요청/씬 유지 등)을 검사했다. 사람의 실제 마우스·키보드 입력 및 NGO 다인 연결 검증과 구분한다.

검증 에디터의 SearchDatabase 초기 인덱싱에서 엔진 내부 ArgumentOutOfRangeException 1건이 있었으나 키트 스택은 아니며 19개 기능 검사는 완료됐다. 전체 Console 오류 0으로 기록하지 않는다. 저장소 전체 문서 검사는 log의 결과를 참조한다.
