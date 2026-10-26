# 패널 조립 검증 — 2026-10-20

## 결과물
- 프리팹: `Assets/Resources/Prefabs/UIs/BasicPanels/BasicPanels.prefab`.
- 씬: `Assets/Scenes/Tests/BasicPanels/BasicPanels.unity`. 단색 배경·연결 인스턴스1·EventSystem/InputSystemUIInputModule1.
- private 제공 Runtime/Builder 재사용, PanelsV1 및 BundledButtons namespace 격리. 기존 버튼 정본5개 중첩 참조, 전용 Static Medium/SemiBold 글리프 사용.
- 실제 설정·사망·방 이탈 기능은 제외. 제공 전환·입력과 검사용 콜백 카운터만 연결.

## 실행 결과
- 최종 recompile_status: completed/failed=false/errors=[]/compilationFailed=false. 조립 중 이전 CS1501은 버튼 폰트 인자 추가 작업 완료 후 해소했다.
- BasicPanels PlayMode 3/3 PASS(3.99초): 규격/레이어/폰트/콜백, timeScale0·Cancel·중복 신호·재활성화, InputSystemUIInputModule 경유 합성 마우스/Enter/Escape.
- CommonButtons PlayMode 2/2 PASS(1.48초), CheerGauge PlayMode 3/3 PASS(1.07초). EditMode와 표준 계단식 검사 결과는 handoff 참조.
- 저장 프리팹 Missing Script0, 버튼 중첩 원본 Confirm/Settings/Cancel/General/Cancel 유지. 프리팹 showMenuOnStart=false, 확인 씬만 true override.
- 실제 확인 씬 Play에서 메뉴·확인창 Overlay 캡처 확인. 1920×1080, 16:10(611×382 및1600×1000)에서 패널 경계/글리프/잘림 검사. 확인창 최초 선택 Button_ReturnToMenu 확인.
- 확인창 판을 실행 중640×304로 확장하여 9-slice 모서리·프레임 유지 확인. 콘텐츠는 제공 고정 배치이며 자동 재배치 검사는 아니다.
- Play 종료 후 BasicPanels 씬 dirty=false. 화면 프리셋1920×1080 복원, 생성했던 임시1600×1000 프리셋 제거. 판 크기 임시 변경 미저장.
- C1·버튼 프리팹/씬7파일 git hash-object가 작업 전 값과 동일. 기존 CommonButtons 폰트/importer 변경 없음. 패널 외 private tracked diff220파일 보존.

## 증거·한계
- 세션 scratchpad의 `panels-menu.png`, `panels-dialog.png`, `panels-dialog-1600x1000.png`, `panels-dialog-wide.png`는 ScreenCapture로 얻은 실제 Overlay다. Camera.Render 이미지가 아니다.
- capture_game_view는 프로젝트 밖 save_path 검증에서 거부되어 실행되지 않았다. ScreenCapture API로 세션 임시 폴더에 저장했다.
- 합성 입력과 API 검사이며 사람 직접 입력·네트워크·실제 게임 이탈 검사는 수행하지 않았다.
- 상단 개발 배지는 기존 전역 기능이며 변경하지 않았다. PM은 정상 동작을 직접 확인하고 정리·저장을 승인했다.

## 정리 후 재검증·저장
- 전체 백업·60개 실행 의존성/GUID 조사 후 Unity DeleteAsset으로 검토 PNG4개·중복 README_UNITY_ONLY.md 및 meta5개만 제거: 33,893,505 bytes(약32.32MiB). 58,237,786→24,344,281 bytes.
- 정리 직후160파일(루트 meta 포함) SHA256 불변. 저장 시 Git 자동 CRLF 변환으로27텍스트 해시만 바뀜; 백업 ZIP과 대조하여133파일 바이트 동일·27파일 줄바꿈만 차이·내용 변경0 확인. 최종 작업 폴더24,364,893 bytes. Source/Generated/Fonts·라이선스·Tools6·SVG4·layer sheet·동봉 버튼·Docs 보존, 재생성 방법은 Harness basic-panels.md로 이관.
- 정리 후 BasicPanels PlayMode3/3(4.05초), CommonButtons2/2(1.48초), CheerGaugePlay3/3(1.03초) PASS. 실제 씬 메뉴/확인창 재생·Overlay 캡처 정상, 최초 선택 Button_ReturnToMenu. 증거는 scratchpad panels-cleaned-menu.png/panels-cleaned-dialog.png.
- private main a971b415 저장·push 완료. 무관220파일·unity_only 키트는 제외. 공개 코드/프리팹/씬/폰트/문서/gitlink는 save-work로 함께 저장하며 PR·병합은 하지 않는다.
