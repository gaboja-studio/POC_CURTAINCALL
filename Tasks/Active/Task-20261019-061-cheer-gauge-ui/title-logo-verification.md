# 타이틀 로고 UI 프리팹 검증

## 후속 저장 승인 (2026-10-21)
- 사용자 "좋아 커밋 푸시 해줘." 요청으로 타이틀 private 키트/meta 전체와 공개 프리팹/검증·지식 기록/gitlink의 저장·push를 승인했다. 아래 commit/push 제외는 제작 당시 경계이며 이번 저장만 대체한다. 무관 변경·실게임·삭제·PR/통합 병합은 제외한다.
- 저장 사전 DryRun: 구역/문서Fast OK(WARN12/hard0)/Compile PASS·씬clean 확인. private 원격 변경은 README 자동 목록 갱신43c8e176뿐임을 확인해 FF 수신, resource/ui-title-logo에서 로고65파일만 선택 stage·diff --check PASS. PNG/원본/자료 전체 보존, source Tools 실행 없음.

- private 저장 완료: 로고65파일 커밋 `f32c85f2502361aa65ae965762fe24f783f7167f`, main push·원격 SHA 일치·작업 트리 clean 확인. 공개 프리팹/기록/gitlink는 이 기록과 함께 저장하며 최종 push 완료는 원격 SHA와 최종 보고로 확인한다.

## 범위·준비 (2026-10-21)
- 사용자 요청은 제공 title_logo로 로고 UI 프리팹 제작. 실제 폴더 replacement_v2는 로고2 PNG 교체 패치이며 전체 타이틀 Builder/View는 동봉되지 않았다. 기존 메뉴/씬·게임 입력 연결, 런타임/영구 테스트 씬 추가·삭제·commit/push는 하지 않았다.
- doctor FAIL1은 private 새 키트 미추적, WARN2는 CLI Facts버전/skill-links. 사용자에게 보고하고 원본 보존하며 진행했다. 자동 다운로드/삭제/저장 없음.
- 타깃은 경로 `D:/github/POC_CURTAINCALL-cheer-gauge-ui`/pid28976/Unity6000.3.25f1 ready. 이번 실제 status의 port는7801이었다(이전7800에서 바뀜). 포트에 의존하지 않고 모든 호출에 worktree project-path를 지정했다. 다른 프로젝트 수정 없음.
- 기존 ResultsUi 씬 clean/Play stopped·Prefab Stage 없음·동명 출력 없음 확인. 보호560파일(키트 전체·기존 프리팹/씬·ProjectSettings) SHA 기준은 scratch에만 보관한다.

## 제작 결과
- `Assets/Resources/Prefabs/UIs/TitleLogo/TitleLogo.prefab` 하나. Root RectTransform/본체·그림자 Image2, 사용자 게임 런타임 코드·TMP·자체 Canvas/EventSystem 없음. 외부1920×1080 LayoutStage/Canvas에 배치하는 조각 프리팹이다.
- 메인 anchor/pivot좌상단/위치576,-48/768×384. 그림자 FullStretch/offset0,-3/#05090B/alpha.32, 본체 FullStretch/offset0/white/alpha1. Image Simple/preserveAspectfalse/raycastTargetfalse/PPUmultiplier1.
- 신규 PNG2 importer만 SpriteSingle/FullRect/PPU200/중앙/border0/입력alpha/투명/sRGB/Bilinear/Clamp/mipmapOff/NPOTNone/Uncompressed/CrunchOff/Max2048로 설정. 원본1536×768 RGBA32/GUID 보존, PNG/Source/Docs/Tools/Previews 변경 없음. 원본 스크립트 실행 없음.
- 조립은 scratch run_script·PrefabUtility/AssetDatabase API, 임시 additive 씬만 사용해 원래 씬을 복구했다. YAML 수동 편집·기존 출력 덮어쓰기 없음.

## 실패·보정·정식 검사
- 최초 check-work: 구역 PASS/문서 FAIL(handoff41>40, todo82>80), Compile/테스트 SKIPPED. compact-docs에 따라 handoff 최신3칸 스냅샷·todo 완료 원문 History005로 보존/압축한 뒤 재실행했다.
- 재실행 check-work: 구역/문서/Compile PASS, 프로젝트 EditMode798/798 PASS. 신규 로고 전용 NUnit 테스트를 추가한 것은 아니며 아래 scratch 자동 검사/Play 렌더와 구분한다.
- 최종 지식 문서 추가 직후 Fast는 새 Fact의 status frontmatter 누락으로 FAIL1, `status: active`를 추가해 보정하고 다시 검사했다. 이 문서 보정은 제품 에셋/코드 변경이 아니다.
- 최초 scratch QA 컴파일은 CanvasRenderer.GetMesh 인자 API 차이 CS1501로 실패, Unity6의 무인자 GetMesh()로 QA만 보정 후 통과. 제품 C# 컴파일 실패가 아니다.

## 실제 렌더·종료 확인
- Play Mode에서 임시 Overlay Canvas1920×1080/Match.5/ReferencePPU100·고정1920×1080 LayoutStage를 만든 후 프리팹을 인스턴스화했다. 1920×1080/1920×1200 × 밝음/어두움 × Main/RoomCode/Lan 총12 ScreenCapture PNG를 확인했다. 하위 배치는 인스턴스만384×192/위치768,-88로 변경, 프리팹 원본은 메인 유지.
- 화면 범위/2:1 비율12건·실제 layer mesh24건·합성 GraphicRaycaster 입력 비차단24건 PASS. PNG12개 실제 해상도 일치·contact sheet 전체와 메인 dark/하위 light 원본 직접 확인. 전역 DEV MODE 배지는 기존 실행 시스템이며 로고 프리팹에는 없다.
- 프리팹 참조39/missing0/MissingScript0, Transform3/Image2/신규 importer2/플랫폼override18검사 PASS. 기존 출력 재조립 거절·프리팹 바이트 불변 확인.
- 보호560 누락0/승인 importer2만 변경/예상 밖0/PNG변경0. Git 변경은 로고 프리팹/meta·제공 키트 미추적·Task/Integration/사실 기록뿐, 기존 씬·ProjectSettings 변경 없음.
- 종료: QA객체0/Play stopped/모든 씬 clean/기존 ResultsUi 씬·1920×1080/timeScale1/runInBackgroundfalse. Console groundTruth 오류0/경고0, 과거 누적 버퍼와 구분한다.
- 사람 최종 외형 승인·물리 입력·실기기/공동·실게임 타이틀 연결·전체 프로젝트 PlayMode 회귀는 미실행. 이번 범위 밖이며 기존 공유 폰트/Buttons/Tooltip 미결을 해결 취급하지 않는다.
