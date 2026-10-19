# 백스테이지·로비 HUD 검증 (2026-10-20)

## 결과물·경계
- 정본 폴더 `Assets/Resources/Prefabs/UIs/BackstageHud/`: BackstageHud/LobbyHud/InventorySlotTemplate 프리팹3종. W1/C4/C5/W6/L1 표시와 합성 확인용이다.
- 독립 씬 `Assets/Scenes/Tests/BackstageHud/BackstageHud.unity`, 공개 wrapper/확인 제어/테스트 `Assets/Scripts/Systems/BackstageHud/`, 전용 Static Medium/Bold `Assets/Resources/Fonts/BackstageHud/`.
- private Runtime/조립기 namespace `CurtainCall.UI.BackstageHudV1`/`.Editor`, 제공 코드는 원위치 보존. 실제 게임·클립보드·자동 타이머·경제·집결 계산은 연결하지 않는다.
- Task working, 사용자 최종 확인 대기. 삭제·Git 저장·PR·병합 없음. 기존 공연 HUD 상세 기록은 `performance-hud-verification.md`에 보존한다.

## 전수 목록·보존 (문서 담당 실측)
- [backstage-hud-inventory.csv](backstage-hud-inventory.csv): meta 제외88실파일, 파일 크기·최신 SHA256·동반 meta GUID·분류·정본 경로 갱신. 경로 집합88/88 일치, 전부 전체 보존.
- 표시용 PNG32 = 신규14 + 정본 재사용18. 재사용18개는 기존 정본과 PNG SHA256 일치18/18. C1 4종/공연 HUD smallplate5종/공용 아이콘9종이다.
- 키트 전체 PNG는45개다. 표시32개 외 미리보기·생성 원본·참고13개도 보존한다. 나머지56실파일 분류는 제작·확장 후보이며 Runtime/조립기도 이 목록에 포함한다.
- 원본 Source/Tools/Docs/시트/목업/폰트·라이선스는 삭제하지 않는다. 표시 자산 수를 전체 파일 수나 누적 manifest 행 수와 혼동하지 않는다.
- 읽기 전용 기준 `backstage-baseline.json` 95항목 중93항목 SHA256 동일. **공유 폰트2개 차이 발견·조사 pending**: `Assets/Resources/Fonts/CheerGauge/Pretendard-Bold SDF.asset`, `Assets/Resources/Fonts/CommonButtons/Pretendard-SemiBold SDF.asset`. 문서 담당은 자산을 수정하거나 복원하지 않았다. 전체 기존 자산 불변 PASS로 기록하지 않는다.
- 위 공유 폰트2개는 시작 gitStatus부터 dirty였고 현재 둘 다 `m_AtlasPopulationMode: 1`(동적 TMP)이다. baseline 이후 추가 SHA 차이는 Play 글리프/atlas 캐시 갱신 가능성이 있으나 원인은 미확정이다. baseline에는 해시만 있어 원래 dirty 내용을 되살릴 백업이 아니며 git 버전으로 임의 복구하지 않는다.
- baseline 파일 SHA256 `49f8637a22abd38dbea9ce224db630ad611e6ac9610999eedaae3ef1578346dd` 불변. 기존 `backstage-inventory.py`는 baseline을 다시 쓰므로 재실행하지 않고 문서용 별도 갱신 스크립트로 CSV만 갱신했다.
- private 무관 tracked diff는220파일/4886삽입/931삭제 유지. 해당 차이·미추적 원본을 저장·삭제·폐기하지 않았다.

## 문서 검사
- `Scripts/verify-knowledge.ps1`: Knowledge OK(9 folders). 소유 문서 `git diff --check` 공백 오류0, LF→CRLF 안내만 표시. Unity 컴파일·테스트 통과를 뜻하지 않는다.

## PlayMode (메인 실행 전달 결과)
- 신규 테스트 강화 후 최종7/7 PASS(3.06초). 최초 실행은 `LoadSceneInPlayMode` 직후 `isLoaded` 검사 때문에 SetUp7건 실패. 한 프레임 yield 및 `GetSceneByPath` 재확인으로 테스트만 보정했다.
- 확인 버튼11개 Mouse/Enter 각1회, 긴 값7→10→7, 로비 공개/복사 실제 InputSystem 경로 통과. 모든 샘플 글리프/텍스트 전체 숨김 회귀와 가림 상태 복사·Controls 재활성화 후 단일 구독 검사도 강화 후 통과했다. 이는 합성 입력 검사이며 사람 최종 그림 확인·실게임 플레이 결과가 아니다.

## 화면·원본·회귀 (메인 실행 전달 결과)
- 실제1920×1080/1600×1000 각12상태, 총24캡처 및 비교 이미지를 메인이 Read로 목시 확인했다. 양쪽 visualjson의 glyph/emptyMeshes failures는 빈 배열 `[]`이다. 사용자 최종 외형 승인과는 구분한다.
- 표시 PNG32개는 시트 crop 바이트 일치, 크기·알파 정상. 그림자 최대 알파203/frame251은 의도된 반투명이며 불투명255가 아니라고 실패 처리하지 않는다.
- 백스테이지 추가 후 PerformanceHud9/9·CommonIcons3/3·BasicPanels3/3·CheerGauge3/3 회귀 PASS. CommonButtons 입력 단독1/1 PASS, 전체2건 실행의 글리프 검사 실패는 아래 미결로 남긴다.
- 정상 reload 설정은 enabled=False/options=None. both OFF 검사는 ProjectSettings 변경 금지로 미실행했다. 정상 설정 검사와 both OFF 검증을 혼동하지 않는다.

## 최종 검사·미결
- CommonIcons 최종3/3 PASS. 최초1600 해상도 실행은 Package Manager의 Unity Connect access-token 환경 오류1건과 입력 기대1건 실패; 1920×1080/PerformanceHud 호스트로 재실행해 통과했다. 원인을 해상도 하나로 단정하지 않는다.
- CommonButtons 전체 실행2/2 FAIL(빈 호스트에서도 재현). `FourPrefabsGeometryAndAllVisualStates`98행 공유 폰트 `HasCharacters("확인취소설정일반…")` false, 후속 입력은 Confirm 클릭0회. 입력 테스트만 별도 실행한 결과1/1 PASS(0.46초). 첫 실패 테스트의 정리 누락에 따른 후속 오염 가능성이 있으나 원인 확정은 아니다. 기존 폰트·테스트는 수정하지 않았다.
- BasicPanels 최초2/3은 SVGImporter 썸네일 생성 중 UIElements 정점65535개 한도 예외가 SetUp에 유입되었다. 재실행3/3 PASS(4.20초). CheerGauge 최초 비동기 상태 대기 시간 초과 후 단독 재실행3/3 PASS(1.44초). 최초 실패·시간 초과를 성공으로 집계하지 않는다.
- Sprite32종 rect/PPU200/border/비압축/noMip/Clamp/Bilinear/sRGB/알파·플랫폼160조합 검사 PASS, 활성 override0/불일치0. 재사용18 정본 PNG/meta는 baseline 검사에서 불변. 최종 Play audit: 의존성74/누락0/Missing Script0/누락 글리프0/활성 텍스트 전체 숨김0, EventSystem1, GraphicRaycaster는 LobbyHud/Controls에만 존재. 폰트는 전용 Static2종이다.
- 정상 reload 설정에서 Play 연속2회 진입 PASS. 첫 진입 기본 sample0/copyCount0/lastCode=null/가림을 검사하고, 로비 sample6·공개·복사1회로 변경한 뒤 종료했다. 두 번째 진입에서 기본 상태로 초기화됨을 재확인했다. ProjectSettings는 변경하지 않았다.
- 표준 `pwsh -NoProfile -File Scripts/check-work.ps1`: exit0, 구역/문서/컴파일 PASS, EditMode798/798 PASS(2026-10-20 최종 실행). 기존 P1 결과를 재사용한 것이 아니다. 이 통과와 별개로 CommonButtons PlayMode 전체 실행 실패는 남아 있다.
- 최종 전수 갱신에서도88파일/재사용18일치/private tracked220파일·4886삽입·931삭제/baseline 파일 SHA 불변을 확인했다. 기준95항목 중 공유 폰트2개 차이만 재현되어 대조 스크립트 exit1. 공유 폰트 원인·복구는 미결이며 기존 dirty 내용 보존을 위해 자동 복구하지 않는다. PM 확인 필요.
- 사용자 최종 확인: pending. 사람 공동/네트워크·전체 PlayMode·실게임 연결은 이번 수행 범위가 아니다.

## 종료 상태·기록
- 최종 문서 편집 뒤 verify-fast에서 handoff42줄 한도 초과를 발견해 현재 스냅샷으로 압축했다. 재실행 Fast OK/Context warn9·hard0/Knowledge9폴더 OK. 컴파일·테스트 결과와 구분한다.
- 종료 Play stopped/BackstageHud 씬 clean, GameView1920×1080, runInBackground=false. reload 설정은 원래 enabled=False/options=None을 유지했다.
- handoff/todo에 실제 결과를 동기화했다. 남은 것은 공유 폰트/Buttons 회귀 미결과 사용자 최종 확인이다. 기존 공연 HUD 과거 검사 이력은 별도 문서에 보존한다. 삭제·Git 저장·실게임 연결 없음.
