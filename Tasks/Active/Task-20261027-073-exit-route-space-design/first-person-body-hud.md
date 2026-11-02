# A 시점·신체 실루엣·달리기 손

> 2026-11-02 최신 요청으로 1인칭 전환·손·조준점은 제거했다. 아래는 이전 구현 이력이며 현재 상태는 [3인칭 전용 정리](third-person-only.md)를 따른다. 상시 절단 실루엣만 유지한다.

- 담당: @RagSpirin / Task073 / feat/exit-route/design-map / 2026-11-01.
- 최신 사용자 요청: 실제1인칭 중앙 작은 조준점,절단 실루엣 좌우 반전. 상시 도식·달리기 손 교대·2키 혼합1인칭·글자/패널 없는 도식은 유지.

## 현재 동작
- 기본3인칭은 유지하고2키로 혼합1인칭 모드를 선택한다. 선택 후 일반 이동/비행은1인칭,외줄/매달림/구조·대포 탑승/운반은 원래 카메라를 쓰고 해제 후 자동1인칭 복귀한다. 별도2키 재입력은3인칭 선택으로 돌아간다.
- A 계단 끝을 지난 도착 카메라75점 요청은 해제해 원본 외줄0점 구도를 덮지 않는다. 생존/관전·로딩/검토 비행 경로는 보존한다.
- A에서만 인벤토리3/4키,1키 검토 비행. 다른 씬 인벤토리는 기존1/2키.
- HUD prefab: Assets/Resources/Prefabs/UIs/EscapeRoute/EscapeBodyStatusHud.prefab. 사람 Graphic1개만 좌하단(40,28)에84×156. 정상회색/절단붉은빗금. 제목/상태/숫자/키 안내·배경/테두리 전부 제거. raycast 없음.
- 기존 NetworkPlayer.LostParts만 읽으며 A의 로컬 참가자가 스폰되어 있으면1/3인칭·외줄·관전/검토 시점에서도 항상 표시한다. A 이탈/로컬 참가자 없음에서는 숨긴다. 피해/네트워크 판정 변경 없음.
- 절단4부위 매핑은 플레이어 왼쪽=화면 왼쪽,오른쪽=화면 오른쪽. 실제 CameraViewDirector.IsFirstPerson일 때 중앙5px 흰 원형/얇은 어두운 외곽 조준점을 표시한다. 선호 시점만1인칭인 외줄3인칭·로딩·관전에는 숨긴다. 조준점 Image1개 추가,사람 Graphic1개/패널0은 유지. 기준1920×1080·중앙 앵커0.5/오프셋0.
- EscapeFirstPersonArms는4원본 HumanoidModel(Rabbit/Bear/BluePierrot/PinkGirl)의 팔 메시/재질/피부색을 재사용한다. Transform만 복사한 임시 골격을 HumanPoseHandler로 굽혀 팔꿈치/손가락을 굽힌 자세를 굽고 즉시 정리한다. 실제 애니메이터/원본 메시/네트워크 객체를 수정하지 않는다.
- 전용 URP Overlay 카메라/31번 FirstPersonArms층/깊이 지움. 비활성 시 카메라 스택/메시 정리,main camera의 기존 layer bit 보존. 임시 캡슐/BlockDoll에는 가짜 팔을 합성하지 않는다.
- 손 간격0.60→0.84m,양손 반대 위상·앞뒤±7cm/상하±2.5cm. 한 번에 한 손만 보이며 내려간 뒤 반대 손이 올라온다. 돌아가는 손은 최대22cm 아래로 내려 숨기고 교대한다. 지상 이동+IsSprinting+입력허용일 때만 손을 표시하며 걷기/정지/공중에는 숨긴다. 절단 팔/봉 점유/운반/대포 중 이동용 손도 숨긴다. 상호작용 전용 손 애니메이션은 이번 범위가 아니다.

## 참고·검사
- 달리기 구도 참고: [Lethal Company 공식 Steam 트레일러](https://store.steampowered.com/app/1966720/Lethal_Company/)20초 부근. 외부 게임 에셋은 복제하지 않았다. 포즈 API는 [Unity HumanPoseHandler](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/HumanPoseHandler-ctor.html)를 사용한다.
- 실제 Play/MPPM은 계속 중지. 편집 렌더는 실제 플레이 결과가 아니다.
- 최신 조준점/좌우: .harness/local/reticle-mirror-20261101/tests.json의16/16 PASS. 프리팹 GUID/씬 기존 인스턴스 참조 보존·A dirty0·Play stopped. check-work 안전성/문서·규칙/컴파일 PASS,해당 실행 테스트 SKIPPED와 별도 HUD16개 검사를 구분한다.
- HUD EditMode16개: 정상/4부위/전체 절단·복원,화면 기준 좌우/메시,글자0·패널0·사람Graphic1/조준점Image1,1인칭→외줄3인칭→평상시1인칭 복귀 및 실제 시점별 조준점 표시,4원본 팔 생성/절단 숨김·복원/원본 골격 무변경,Overlay 깊이 지움·정리/타 카메라 mask 보존.
- 증거: .harness/local/human-only-hud-20261101-023823. silhouette-0/13.png, sprint-phase-0/1/2.png,tests.json,scene-audit.json,compile.txt,check-work.txt. 최종 검사 결과는 log에 기록한다.

직전 확인: 독립 Compile PASS·EditMode16/16 PASS,편집 렌더 확인,A dirty0/누락 스크립트0/16봉·5연결 보존. 실제 Play/MPPM 미실행. 전체 check-work의 기존 문서 차단 여부는 log 참조.

최신 변경 증거는 .harness/local/alternating-hands-current.txt가 가리키는 폴더에 보관한다. hud-tests.json은 상시 HUD/48위상마다 손 최대1개·절단 차례 검사를 포함한다. assembly-tests.json은 dev 통합 후 가변 참가 수 회귀 검사다. 실제 Play/MPPM은 계속 중지한다.
최신 결과: dev b723ca79 통합·보존48경로 누락0·Compile PASS·HUD16/16/집결26/26 PASS·12발판 원본 Transform 일치·A 저장/Play 중지. 손은48위상 전체에서 동시에 최대1개,절단 팔은 자기 차례에도 미표시. 진입 근접 카메라도 최신 dev 에셋을 실제 A 참조에 반영했다.
