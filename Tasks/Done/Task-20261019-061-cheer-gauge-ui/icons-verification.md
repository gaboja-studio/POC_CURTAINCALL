# 공용 아이콘 검증 (2026-10-20)

## 결과물·경계
- `Assets/Resources/Prefabs/UIs/CommonIcons/`: CommonIconCatalog.asset(25종), CommonIcons.prefab(75 Image 비교 보드).
- `Assets/Scenes/Tests/CommonIcons/CommonIcons.unity`: 씬 전용 Controls, 단일 EventSystem/InputSystemUIInputModule, tint/size/reset/Settings 별도 인스턴스 입력 검사.
- `Assets/Scripts/Systems/CommonIcons/`: catalog/preview/안전 조립 wrapper/PlayMode 테스트. 원본 코드는 private 유지.
- 조립·검증 당시 삭제·commit/push·PR·실게임 연결은 실행하지 않았다. 이후 PM이 동작 확인·저장을 요청하여 private main fe5b19fb 저장/push 완료, 공개는 이 문서 포함 save-work로 저장한다. 신규22종 그림은 사람 최종 승인 전이다.
- private는 아이콘179파일(meta 포함)만 저장, 무관 변경 제외. 원본 LICENSE.txt의 후행 공백1건은 라이선스 원문 보존을 위해 수정하지 않았다. 삭제·PR·실게임 연결 없음.

## 전파일·미래 사용 후보
- [icons-inventory.csv](icons-inventory.csv): meta 제외81개 전수, PNG53개. 상대경로/분류/키/크기/해시/GUID/실사용 정본/보존/사용 후보 기록.
- 최종25개 모두 검사·catalog 참조. 신규22종과 기존3종을 구분하고 기존3의 중복 PNG도 보존.
- 미래 후보는 납품 manifest의 쓰는 곳을 ‘사용 후보(게임 미연결)’로 기록. 화폐/시간/인원·가시성/통신·배송/진행·경고/시설·식량·물·치료·배낭 등이며 확정된 게임 규칙이 아니다.
- Source 전체(최종 생성22/초기 교체이력3), Tools6·Docs·투명 시트·PNG목업2·SVG목업2·폰트/라이선스 전부 보존. 누적 manifest57행은 이전 C1/버튼/패널까지 포함한다.
- 원본25종: 64×64 RGBA/투명/흰 실루엣/사방8px 여백 및 투명 시트의25셀 RGBA 일치 PASS. 납품238/238 기록 대신 이번 Python/Pillow 실측으로 확인.
- manifest에는 SHA256 필드가 없어 제공 해시 대조는 해당 없음. 전파일 실측 SHA256은 CSV에 별도 기록.
- settings/close/warning은 이전 정본과 바이트 동일. 정본 GUID와 meta SHA256도 작업 전후 동일. 신규22 importer만 보정.

## Unity 검증
- 재컴파일 PASS. catalog25/고유 Sprite25/비교 Image75·종별3회, Sprite64/PPU200/중앙/border0, Simple/preserveAspect/raycastOff 확인.
- importer·유효 텍스처 검사 PASS. Standalone/Android/iPhone/WebGL/Windows Store Apps override125조합 중 활성 override0. 기존3 importer 변경 없음.
- prefab/scene/catalog의 의존성59개 누락0, TMP 글리프 누락0. 새 전용 Static Medium352문자/atlas3장, 기존 폰트 미변경.
- catalog 중복키·길이 불일치·미등록키 예외 검사 PASS.
- CommonIconsPlayTests 최종3/3 PASS: 공개 prefab/catalog/scene, tint10개·순환·크기32→48→16→24→32/중심·Sprite 유지/reset, InputSystem 가상 Mouse/Keyboard 클릭·Enter.
- SampleButton의 배낭 아이콘 중앙 클릭과 Enter가 부모 onClick에 전달된다. 기존 Settings 정본의 설정 아이콘 유지. 합성 입력 검사이지 사람 조작 확인은 아니다.
- 실제 Play Overlay 1920×1080 기본32/전체 tint10/크기4, 1600×1000 성공색48 캡처 확인. 밝은 배경 기본색 직접 표시는 저대비(원본 의도), 받침 표시 정상. 16px는 세부 실루엣 식별 한계가 있다.
- 캡처는 scratch의 icons-default-1920.png, icons-success48-1600.png, icons-tint-0~9.png, icons-size-{16,24,32,48}.png. capture_game_view는 프로젝트 밖 저장 거부하여 ScreenCapture로 실제 화면을 scratch에 저장했다.
- Play 종료·기본1920×1080 복구·CommonIcons 확인 씬 재오픈, dirty=false/compileFailed=false. 테스트 후 임시 변경 미저장.

## 회귀·수정 이력
- 기존 UI7파일 git blob SHA1 모두 작업 전후 동일(C1 prefab/scene, 버튼4 prefab/scene). BasicPanels prefab/scene/font·기존 버튼 font diff0. private 기존3키트 status 변경0.
- CommonButtons2/2, BasicPanels3/3(재실행), CheerGauge3/3 PASS. 패널 첫 실행은 정확 Vector2 비교에서 (0,-64)와 (0,-64) 실패 후 변경 없이 재실행 통과: 좌표 정밀도 간헐 실패로 기록하며 기존 범위는 수정하지 않았다.
- 첫 폰트 조립은 atlas 배열의 빈 슬롯에서 NullReferenceException. 새 wrapper에서 null 슬롯을 제외하고 소유 폰트 저장을 마친 뒤 재실행 성공.
- 아이콘 테스트 초반2회는 테스트가 prefab에 씬 전용 Preview를 요구하고 모든 Controls 버튼에 커스텀 타입을 요구한 가정 오류였다. 승인된 prefab/scene 책임에 맞게 검사 수정 후3/3.
- doctor FAIL: 보존할 private 미저장 변경. 시작 시 보고했으며 무관 변경 저장/폐기하지 않았다. CLI 파라미터 전달 오류1회는 올바른 --code 사용으로 해소.
- 표준 check-work 최종 구역/문서/컴파일 PASS, EditMode798/798 PASS. 최초 문서 검사는 신규 사실 문서 frontmatter 누락으로 실패하여 형식 보정 후 통과했다. 전체 PlayMode·사람 공동·네트워크 검증은 미실행.
