# Log

- 최종 정리: A/B/C 씬과 B/C PNG·SVG 구조도, `variants.md`를 로컬 저장했다. 묶음 check-work 안전성·문서·컴파일 PASS, 자동 테스트 SKIPPED. A 자유 비행 화면으로 돌아간다. Unity/TMP가 기본 `LiberationSans SDF.asset`도 자동 갱신했으므로 해당 부수 변경과 기존 ProjectSettings·private 포인터 차이는 향후 작업 저장 대상과 구분한다. 임의 reset/discard·commit/push 없음.

- 2026-10-28 취침 전 요청 완료 묶음: A의 회색 콘크리트·빛을 유지하며 반복 드럼통을 작업 구역 두 곳으로 줄이고 배수 홈/그레이팅·습윤 가장자리·케이블 릴·작업대·팔레트·점검함·작은 표식·굽이의 반사광을 추가했다. `Label_` 제작 라벨은 Play에서만 숨기고 `Wayfinding_` 현장 표식은 유지한다. 배경 원본/공용 플레이어/private 에셋은 수정하지 않았다.
- B `escape_route_b.unity`: 트램폴린·엇갈린 그네 선행, 남하 굽이 뒤 대포·Y+4 고립 발판·대각 외줄, 12m 하강과 합류. C `escape_route_c.unity`: 가로 진입에서 북쪽 대포 횡단, Y+2 편심 발판·대각 외줄, 동쪽 하강 굽이와 남쪽 그네 횡단, 북상 질주·출구. 네 묘기 자리·큰 층고/깊이·재합류·밝은 외부와 자유 비행을 모두 포함한다. 구조의 상세 선택은 제작 제안이며 실제 조작·사거리·난도는 미검증이다.
- B/C를 각기 별도 내부 구조로 생성하고 A의 렌더 설정·외부 배경·미리보기 연결을 보존했다. `Variants` 관련 Editor 파일과 A/B/C 열기 메뉴, `Layout/{b,c}-plan.{svg,png}`, `variants.md`를 추가했다. C 재생성의 외부 위치가 누적되지 않게 고정했으며 C04→C05의 입구 방향 불일치를 최종 X119 중심으로 수정했다.
- B/C 저장 후 정적 검사: 각각 구역 연결 9/9(반경 0.3m SphereCast), 바닥 높이 11/11, 단절부 3/3, 누락 스크립트/오류·누락 재질 0/0. 결과는 `.harness/local/exit-route-{b,c}-structure.json`. 실제 게임플레이 테스트와 구분한다. A/B/C Play 기동에서 자유 비행 활성·기존 걷기 정지, 큰 제작 라벨 표시 0개를 확인했다. 입력 주입·자동 이동·묘기 통과 시험은 하지 않았다.
- Unity의 일시적 Play 리로드 중 C 조회 연결이 한 번 끊겼으나, 별도 재조회에서 참조·라벨 상태를 확인했다. 최종 콘솔 groundTruth는 compilationFailed=false·consoleErrors=0이다. 기존 `_ThirdParty` 포인터 차이와 ProjectSettings 변경은 보존했으며 commit/push는 하지 않았다. 런타임 A/B/C 선택·대기실 연결·묘기·협동·추격·탈출 판정은 이번 배경 범위 밖이다.

- 2026-10-28 비행 연결 확인: check-work 안전성·문서·컴파일 PASS, 자동 테스트 SKIPPED. 저장된 씬에서 Play Mode 진입 후 비행 컴포넌트 활성, 기존 걷기 카메라 정지, 씬 Player 비활성, 시작 카메라 (15, 1.64, 10), 속도 12m/s·가속 4배를 읽기 전용으로 확인했다. Play Mode를 켜 두었으며 AI 이동 입력은 주입하지 않았다. 실제 비행 조작은 사용자가 확인한다. commit/push 없음.

- 2026-10-28 사용자 요청으로 Play Mode에서 여러 구역을 날아다니는 미리보기를 추가했다. `EscapeRouteFlyCamera`를 탈출로 Main Camera에 연결했다. WASD·마우스, Space 상승·Ctrl 하강, Shift 가속(12→48m/s), Esc 커서 해제·화면 클릭 재잠금. 중력·충돌 없이 카메라만 이동하며 HUD·순간이동·자동 복귀는 없다. 기존 걷기 카메라·씬 Player는 비행 중에만 정지하고 비활성화 시 이전 활성 상태·조작 규칙을 복원한다. 공용 플레이어 프리팹과 시스템 코드는 수정하지 않았다.

- 2026-10-27 03 압도감 후속: 사용자가 “규모는 큰데 압도감이 부족함”을 확인하고 첫 진입 개선을 승인했다. 편집 모드에서 `14_CavernReveal`에 낮은 문틀·끝막이, 연속 하부 외피, 수직 집관과 상·하부 점검 구조, 14m 설비 점검구·사람 크기 점검문, 중성 조명 6개를 배치했다. 원본 에셋·다른 구역·전역 하늘/주광은 수정하지 않았다.
- 제작 도구 `EscapeCavernReveal.cs`는 같은 그룹을 교체하므로 재적용 시 중복하지 않는다. 전체 구조도 재배치의 마지막에도 이 패스를 적용하도록 연결했다. 외곽 규모와 기존 발판 Transform은 유지한다. 새 점검 발판은 충돌체 없는 시각 기준으로 플레이 우회 경로를 만들지 않았다.
- 정적 조회: 출발부 3점 Y0, 중간·도착부 Y+2, 단절부 2점은 아래 12m 내 바닥 없음. 입구 눈높이 통과선은 막힘 없음. 새 하부 외피의 상단은 Y0이며 초기 조정 중 생긴 진입부 높이 간섭을 최종본에서 바로잡았다. AI 플레이 입력·이동 시험은 하지 않았다.
- 씬 저장·재개방 후 `14_CavernReveal` 1개/직접 자식 48개/조명 6개, 점검문 실제 높이 2.2m, 누락 스크립트·오류/누락 재질 0, dirty=false 확인. 편집기 카메라로 진입부 전후와 공동 내부를 캡처했다. 실제 플레이 감각·다인 통과·성능은 미검증이다.
- 첫 `unity recompile --strict`는 기존 `_ThirdParty` TMP obsolete 경고 14개 때문에 종료 1이었다. 후속 recompile_status는 completed·failed=false·errors=[]·compilationFailed=false이며, 이번 묶음의 최종 판정은 아래 check-work 결과를 따른다. 기존 submodule 포인터 차이·ProjectSettings의 로컬 변경은 보존했다. commit/push 없음.

- 저장 직전 검사: check-work -SkipUnity의 안전성·문서/규칙 PASS, 기존 문서 길이 경고 12개·hard 0. Unity 재컴파일/자동 테스트는 생략했으며 읽기 전용 `recompile_status`에서 completed, failed=false, errors=[], compilationFailed=false를 확인했다. `git diff --check` 통과, 원격 작업 브랜치와 HEAD의 선행/후행 0/0, private submodule은 깨끗하고 origin/main에 커밋이 존재한다. 전체 변경을 자동 stage하는 save-work 스크립트 대신 스킬의 명시 경로 stage 절차로 관련 파일만 저장한다.

- 2026-10-27 다른 컴퓨터 작업 준비: 사용자 저장 요청으로 현재 `feat/exit-route/design-map`의 맵·전용 재질·Editor 제작 도구·Task/지식 기록을 저장·업로드한다. Unity의 자동 직렬화 부수 변경인 LiberationSans SDF와 MultiplayerManager 설정은 작업 커밋에 포함하지 않고 로컬에 보존한다. `_ThirdParty`는 변경 없이 기존 submodule 커밋 `86fc532c9ae6a99e830ad1aa9d68a97878cfa064`를 사용한다.
- 이어서 작업할 때: 현재 브랜치를 내려받고 기존 private `_ThirdParty` submodule/LFS를 준비한 뒤 `Assets/Scenes/Levels/escape_route.unity`를 연다. `space-baseline.md`가 공통 규모·감정·B/C 확장 경계의 현재 기준이다. Unity 편집 메뉴는 `Tools/CurtainCall/탈출로/`이며 재배치는 현재 생성 배경을 다시 만들므로 일반 열기/플레이에는 실행하지 않는다.

- 2026-10-27 최신 수정: `Curve` 참고의 평탄한 회색 콘크리트로 변경. 기존 Concrete BC/N를 Escape 전용 재질에 연결하고 벽 normal 0.10/바닥 0.15, 금속·등기구·라벨의 주황빛을 줄였다. 원본 `_ThirdParty` 에셋은 변경하지 않았다.
- 내부는 낮은 중성 확산 조명으로 진행면을 읽게 하고, 사용자 후속 지시로 갈대밭은 강한 햇빛과 맑은 하늘로 대비시켰다. 실험한 먹구름 HDRI 외부 재질은 사용하지 않아 제거했다. 실제 Unity 카메라 캡처에서 내부 콘크리트와 밝은 외부를 확인했다.
- 별도 추격자·정비 통로 09를 제거했다. 배경 루트 X/Z 1.25배, Y 1배로 확장해 진입 15m·굽이/질주 20m·05 단절 25m로 만들었다. 회전된 벽·연결 난간·외줄은 부모 좌표계에서 함께 확장하고 소품·라벨·캐릭터는 크기를 보정했다. 같은 확대 메뉴 재적용 시 누적되지 않는다.
- 기존 1.8m 캡슐 기준물을 제거하고 실제 Player.prefab이 참조하는 Humanoid 모델을 1.73m로 맞춰 세 곳에 배치했다. 실제 씬 Player의 모델 참조와 기존 LevelPreviewCamera를 연결하고, 이 씬 인스턴스의 외줄 균형 시작만 껐다. 공용 이동·카메라 코드와 Player 원본 프리팹은 수정하지 않았다.
- 구조 확인: 확대 후 루트 (1.25,1,1.25), 09 통로 없음, 누락 스크립트 0·오류/누락 재질 0. 바닥 12개 위치 ray 중 11개는 예상 Y0/+2를 확인했고 시작점 ray는 Player 충돌체 상면을 먼저 맞았다. 재질 변경만 적용한 단계에서는 기존 렌더러 11095개의 위치·회전·스케일·bounds가 동일함을 확인했다.
- 사용자가 조작 안내·추가 미리보기 기능을 원하지 않아 HUD·순간이동·자동 복귀를 추가하지 않았다. 플레이 모드 진입 후 사용자가 직접 테스트하겠다고 지시했으므로 입력 주입·이동 시험 없이 그 상태를 유지했다. 이동·묘기 통과·재시작·멀티플레이 검증 완료로 보고하지 않는다.
- 검사 전 에디터 컴파일 실패 false·현재 콘솔 오류 0을 확인했다. 기존 `_ThirdParty` UI 생성기의 obsolete TMP API 경고 14개는 남아 있다. 사용자 플레이 상태를 유지하기 위해 이번 check-work는 -SkipUnity로 실행하며 컴파일 재실행과 자동 테스트는 생략한다.

- 2026-10-27: 사용자 요청으로 현재 `feat/exit-route/design-map` 브랜치에 @RagSpirin의 탈출로 공간 디자인 Task를 세팅했다. 기존 에셋을 활용한 맵 배치가 주 작업이며, 씬·에셋은 이번 세팅에서 수정하지 않았다.
- 초기 doctor(-SkipEditor): FAIL — 현재 브랜치에 대응하는 Active Task가 없었음. 이번 Task 생성으로 연결을 추가했다. 경고: unity-cli 설치 버전과 Facts 차이, gh 미로그인. 문서 세팅에는 영향 없으며 커밋 요청 시 gh 연결 필요. 같은 작업의 doctor는 반복하지 않았다.
- new-task -UseCurrentBranch가 중첩 브랜치명을 거부하여 기존 브랜치 연결 시 중첩 경로를 허용하도록 보완했다. 새 브랜치 이름 규칙은 유지했다. WhatIf와 실제 생성 성공, Task 번호 073 배정.
- 다음: 담당자가 plan/todo를 읽고 기존 탈출로 씬에서 에셋·동선 조사와 배치를 시작한다. 추가 인계 승인 대기는 없다.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-27 배경 제작: 사용자 최신 요청으로 현재 브랜치에서 작업. doctor WARN(fail 0, warn 1: CLI beta.13/문서 beta.12 차이), Unity 6000.3.25f1/Pipeline 연결 확인. start-work는 중첩 브랜치명을 거부했으나 현재 브랜치와 Task의 일치를 확인했고 이름을 바꾸지 않았다.
- 처음 만든 단순 공동 초안은 사용자의 규모·구조도 피드백에 따라 교체했다. 최신 평면도 192×76m, 01–08 구역, 03 고립 발판과 +2m 착지, 05의 20m 단절을 반영했다. 28m 층고·60m 하부 깊이는 컨셉아트에 대응하는 이번 제작값이며 원본에 명시된 치수와 구분했다.
- 공연장 배경 원본을 읽기 전용으로 열어 비교했다. 런타임 배율 1/3의 전체 배경 bounds는 약 666.67×563.23×666.67m이며 원경까지 포함하므로 탈출로 주행 영역의 치수와 동일시하지 않는다. 원본 씬은 저장하지 않았다.
- 기존 공장 콘크리트·배관·크레인·난간·풀과 기존 URP 재질을 참조했다. 필요한 재질 보정은 EscapeRouteMaterials의 복제본에만 적용했고 `_ThirdParty` 변경은 없다. 미보유 장치·갈대 형태는 그레이박스와 한국어 이름으로 표시했다. AI·문 조작·묘기·탈출 판정은 구현하지 않았다.
- 12개 배경 그룹(01–08, 정비 통로, 천장, 깊이 설비), 약 1.5만 Transform. 원본 LOD 임계값 문제를 인스턴스 LOD0 사용으로 보정했다. 현재는 배경 검토용이며 렌더링 최적화·프레임 성능은 미검증이다.
- 저장 후 씬 재개방: 누락 스크립트 0, 활성 렌더러의 누락/오류 재질 0, 한국어 누락 글리프 라벨 0. 바닥 12지점 ray 확인(장치 자리 2곳은 그레이박스 상면), 03의 두 단절·05 단절의 지면 없음 확인. 실제 플레이 입력·다인·묘기 통과 테스트는 하지 않았다.
- 실제 Unity 카메라로 평면·천장/전면벽을 가린 검토용 조감·03/05·외부를 캡처했다. 캡처를 위해 숨긴 구조는 복원했다. 임시 TempFloor는 새 바닥 생성 후 비활성화했다.
- 에디터 자동 직렬화 부수 변경: 기본 LiberationSans SDF의 Unity/TMP 포맷 갱신, MultiplayerManager의 비활성 배포 필드 2개 추가. 사용자 변경과 혼동해 reset/discard하지 않고 남겼다. 이번 맵은 별도 escape-labels 폰트를 사용한다.
- 지식: imported-factory-lod.md 기록, verify-knowledge OK. 다음은 사람 눈높이로 규모·압도감·도착점 가독성을 확인한 뒤 B/C 내부 변형 설계. 기준은 space-baseline.md.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-28 A 첫 공동 수직 설비실
- 사용자 요청: 더 깊고 거대한 시설 안에 갇힌 느낌. 큰 방에 소품을 붙이는 방식에서 단면·설비 중심으로 수정.
- 실제 작업 체크아웃 C:/Unity/POC_CURTAINCALL, feat/exit-route/design-map. 기존 C 씬은 편집 모드·저장 상태를 확인한 뒤 A를 열었다. 기존 doctor는 이 연속 작업에서 실행한 기록이 있어 반복하지 않았다.
- `EscapeVerticalCavern.cs` 추가. 새16 그룹은 월드미터, 50×50m 외피·Y-90–44, 지름12m/높이120m 주 설비·교각·공급관·상부 크레인·4개 점검층. 실제 경로 Y0→Y-6→Y+2, 40단0.2m 계단.
- 기존03/14 및 대응 천장/깊이 장식/인체 기준은 비활성 보존. A 전체 빌더에도 새 패스 연결. B/C 재생성/저장 없음. 원본 외부 프리팹·공용 렌더 환경 유지.
- 에디터 상태 조회 후 A 씬을 `.harness/local/escape-vertical-before/escape_route.unity`에 백업했다. 사용자/기존 변경(_ThirdParty gitlink, ProjectSettings, TMP 폰트, _Recovery 포함)은 보존했다.
- `ValidateVerticalCavern`: 바닥6/6, 계단40/40, 단절2/2, 연결3/3, 누락 스크립트/재질0, 월드배율·이전 구조 비활성·비행 카메라 검사 통과. 이동 입력/자동 플레이 없음, 실제 묘기 기능과 성능은 미검증.
- 편집 카메라 진입·아래층·하부 깊이 캡처 확인. 하부 과한 조명과 떠 보이는 계단/발판 지지대를 보완 후 재확인했다. 캡처: 기존 시각자료 폴더의 a-vertical-entry.png, a-vertical-depth.png, a-vertical-low-angle.png.
- 첫 eval은 void 반환 컴파일 오류로 변경 없음. 컴파일 도중 발행된 생성 job은 도메인 리로드로 없어졌고, 새 그룹 부재 확인 후 다시 실행했다. 완료된 생성·재검사 모두 성공.
- 편집 위치와 대체 치수는 variants.md와 Facts/escape-route-layouts.md에 반영. commit/push 없음. 다음: 사용자의 공간 감정·묘기 치수 검토.

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work: 안전성 PASS, Structure/Knowledge OK, 문서 길이 기존 경고12·hard0. 작업 전부터 존재한 Assets/_Recovery.meta 및 _Recovery/0.unity(.meta)의 최상위 경로 규칙 위반으로 문서/규칙 FAIL, 컴파일은 선행 실패로 SKIPPED, 자동 테스트 SKIPPED. 사용자 복구 파일은 이동/삭제하지 않았으며 별도 verify-unity -Compile로 컴파일을 확인한다.
- 별도 verify-unity 결과: Editor ready, 열린 A 씬 dirty0, Compile PASS(up_to_date), Tests SKIPPED. Scene View를 첫 공동 입구 눈높이에 맞춰 남겼다.

## 2026-10-28 A 전체 브루탈리즘 확장
- 사용자: A 첫 공동 방향을 나머지 구간에 적용하고, 세련된 인상을 줄이며 브루탈리즘 강화 요청.
- 시작 시 C:/Unity/POC_CURTAINCALL, feat/exit-route/design-map, A 편집 모드/dirty false 확인. doctor는 동일 연속 작업에서 반복하지 않음. 원본 씬 백업 `.harness/local/escape-brutalist-before/`.
- 새 `17_BrutalistRoute`와 EscapeBrutalistRoute/End/Validation 추가. 01 낮은 하중보, 02 압축 갤러리, 03 원통을 감싼 콘크리트 코어, 04 수로 선반, 05 깊이100m/착지Y-8, 06 상행40단, 07 반출 트렌치/6m 두께 방벽 두 겹을 구현. 기존08과15/16 유지, 대체 구형 구간은 비활성 보존.
- 기존 콘크리트 텍스처를 새 전용 무광 재질에 사용하고 실제 미터 UV 메시를 Unity AssetDatabase로 생성. 원본 외부 에셋과 B/C 씬 저장/재생성 없음. 첫 공동 기존 Renderer에만 전용 콘크리트 재질 적용, 공유 원본 재질 변경 없음.
- 화면 검토 후06 상단 과한 조명 완화·계단 난간 지지·05/07 인체 크기 기준 보완. 캡처 a-brutal-01/02-v1/03-v1/04-v1/05-depth/06/07/exit-v1.png는 기존 시각자료 폴더에 저장.
- 정적 검증92/92: 바닥19·계단50·연결16·단절3·누락 스크립트/재질·메시 영속성·배율. A03 기존검사도 Failures0. 첫 영속성 검사에서 TMP 자동 재생성 텍스트 메시를 에셋으로 요구하던 검사 조건을 수정했으며 실제 형상 메시 누락은 없었다.
- 씬 저장 후 재개방해 정적 검사 재통과, 보고서 `.harness/local/exit-route-a-brutalist-validation.txt`. 자동 이동/Play/묘기 통과 시험 없음. 성능은 미검증.
- Scene View가 없던 상태에서는 GetWindow<SceneView>로 열어 캡처했다. 정확한 눈높이 배치에는 camera.fieldOfView 대신 cameraSettings.fieldOfView로 LookAtDirect 거리를 계산해야 한다는 점을 확인해 마지막 깊이 캡처에 반영.
- 최신 구조는 a-brutalist-route.md, Facts/escape-route-layouts.md, Decisions/escape-background-direction.md에 기록. 기존 사용자 복구파일/ProjectSettings/TMP/서브모듈 차이 보존, commit/push 없음.

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work: 안전성 PASS, Structure/Knowledge OK, 기존 문서 경고12·hard0. 작업 전부터 존재한 _Recovery 경로 위반3개로 문서/규칙 FAIL 및 통합 컴파일 SKIPPED. 복구 파일 보존. 별도 verify-unity -Compile PASS(up_to_date), Tests SKIPPED; verify-knowledge OK. A 씬 재개방 후 dirty0이며 Unity Scene View는 둘째 공동 입구에 남겼다.

## 2026-10-28 사용자 저장 요청
- save-work 절차에 따라 현재 작업 브랜치에 작업 파일만 커밋·push한다. 작성자 gh @RagSpirin 확인. A 씬은 편집모드/dirty0이며 최신 정적 검사·컴파일 결과를 재사용한다.
- 이전 전체 검사에서 걸렸던 사용자 Unity 복구 파일3개는 삭제/이동 없이 로컬 .git/info/exclude에 정확한 경로만 등록해 저장 대상에서 제외했다. 공유 .gitignore와 검사 규칙은 변경하지 않는다.
- _ThirdParty gitlink, Multiplayer/Vivox 설정, 공용 LiberationSans TMP 폰트는 기존 별도 변경으로 stage하지 않는다. 이번 Task의 A/B/C 씬, 제작/비행 코드, 전용 머티리얼/메시/라벨 글리프, 구조도와 관련 문서만 저장한다.

- 2026-10-28 작업 환경 준비: 원격 재조회 후 feat/exit-route/design-map과 origin이 380fc5ef에서 동일함을 확인했다. Unity 6000.3.25f1/Pipeline을 C:/Unity/POC_CURTAINCALL로 명시해 연결하고 저장된 A 씬을 다시 열었다. 편집 모드에서 17_BrutalistRoute를 선택해 Scene View에 맞췄다. Transform 15641개, 누락 스크립트/재질 0, 자유 비행 활성 및 기존 걷기 카메라/Player 참조 연결을 확인했다. 씬 미저장 변경 없음, 기존 로컬 에셋/설정 3개 변경 보존. 컴파일 실패 false. 콘솔 오류 2개는 기존 Vivox 자격 증명 조회 HTTP 403이며 배경 편집 준비와 구분한다. 코드/씬 수정·플레이 모드 진입·이동 입력·자동 테스트는 수행하지 않았다.

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-28 현재 피드백 반영: 사용자가 저장한 A 씬을 .harness/local/escape-industrial-feedback/escape-route-before.unity에 백업하고18_WorkingFacilityFinish를 추가했다. Flat 주변광 RGB(.21,.22,.23)→(.36,.37,.38), 근거리 작업등 spot112도/inner38도, 국소 확산 작업등으로 어두운 벽/설비 가독성을 보완했다. 로컬 Main Camera의 검은 단색 배경을 Skybox로 바꿔 맑은 출구 하늘을 복원했다. 배관/밸브/처진 케이블/정비 물품/국소 누수/보수 흔적429개 Transform, 충돌체0. 기존 구조와 B/C·공용 재질·private 원본은 보존했다. 5개 같은 카메라의 전후 PNG를 같은 로컬 폴더에 보관했다.
- 정적 확인: A 92개 검사 통과, 기존 수직 공동 검사 실패0, 추가 그룹 누락 스크립트/재질0·쉐이더 오류0. 저장 후 재개방에서18 그룹/주변광/Skybox/dirty=false 확인. 이동 입력·자동 플레이·성능 테스트 미실시. 현재 장치 화면만 확인했으며 이전 데스크톱과의 밝기 차이 원인은 확정하지 않았다.
- Vivox: 403은 과거 로그에만 남았지만 읽기 전용 인증/credentials 엔드포인트 재요청도403이었다. 자격 증명은 출력/저장하지 않았다. 사용자가 다른 계정일 수 있다고 답했으므로 프로젝트 권한이 있는 Unity 계정 로그인 후 재확인이 필요하다. 서비스를 끄거나 인증 오류를 숨기는 코드 수정은 하지 않았다.
- 제작 도중 편집 도구의 LoadAllAssetsAtPath(ScenePath)가 ReadObjectThreaded 오류를 유발해 즉시 제거했다. 호출 이후 적용은 완료됐고 기존/추가 구조 검사 및 재개방 확인을 통과했다. 해당 오류 원인을 수정한 후 콘솔을 비우고 최종 검사를 진행한다.

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-28 저장 요청: A 사용감/가독성 수정 씬, 전용 재질6종, 편집 도구2개, 표면 오염 셰이더와 관련 Task/지식 문서만 명시 경로로 저장한다. 최종 check-work의 안전성·문서·컴파일 PASS 및 정적 동선 결과를 재사용하며 이후 변경은 todo 완료 표시와 저장 기록뿐이다. escape-labels의 자동 생성 글리프/폰트 기능 캐시, 공용 LiberationSans 폰트, MultiplayerManager의 기존 로컬 변경은 커밋에서 제외하고 보존한다. Vivox403의 계정 재로그인/재확인과 실제 플레이·성능 확인은 남아 있다.
