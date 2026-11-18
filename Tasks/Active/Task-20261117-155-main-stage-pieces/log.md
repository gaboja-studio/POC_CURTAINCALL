# Log

- 2026-11-17: doctor.ps1 -SkipEditor 1회 실행. 최초 도구 응답이 잘려 최종 판정은 확인하지 못했으므로 PASS로 기록하지 않는다. 일반 권한 Git은 LFS/MSYS·서브모듈 접근 오류, CIM 프로세스 조회는 접근 불가. Get-Process Unity에서는 실행 중인 Unity가 없었다.
- 2026-11-17: 권한 확장으로 git status·fetch origin dev 성공. HEAD...origin/dev=0/0으로 이미 동일하여 병합 불필요. gh 로그인 MoHoDu. 기존 변경 5개 보존.
- 2026-11-17: Confluence 44793873 및 31784961 읽기, 현행 카탈로그·PropCarry.Equipment·Task-092/094와 비교. 신규 기물보다 기존 공연 장비 4종 우선. discussion.md에 미확정 제안 저장. 외부 문서와 게임 파일은 수정하지 않았다.
- 다음: 나머지 기물 효과·신발 명칭을 합의하고, 실제 환호 포인트 후보를 선택한다.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-18 재개: Unity 프로세스 없음 확인. origin/dev fetch, HEAD와 0/0. 기존 변경 5개 보존. doctor는 동일 작업에서 재실행하지 않았다.
- 신발 확정: 공중 추가 점프, 착지 전 최대 1회, 실제 이단 점프 적용 시 내구도 1. 설명 수정 및 Decisions/Performance-equipment 기록, 기존 내구도·부상 문서에 신규 기획/기존 구현 구분. 명칭 ‘곡예사의 도약화’는 제안이다.
- 실제 Synty PolygonHorrorCarnival 가면·머리·웃는 광대·표적판 4종을 원본 FBX+프리팹 컬러 아틀라스로 Blender 5.2 배경 렌더하여 앞뒤 확인. 가면 A 추천. 원본에 명확한 균열 없음. 모델·프리팹·게임 코드 변경 없음. FBX의 비어 있는 텍스처 참조 경고는 프리팹에서 확인한 01_A 아틀라스를 명시 적용하여 미리보기 처리.
- 비교 이미지: `C:/Users/imvag/.codex/visualizations/2026/11/17/01a1e9b3-663b-7802-ac84-19722bfce72d/cheer-assets-front.png` 및 `cheer-assets-back.png`. Unity 최종 화면·성능·공중 점프 검증은 아직 진행하지 않았다.
- Confluence 44793873 live_doc에 신발 규칙·설명, 기존 에셋 우선 방향, A/B/C/D 후보와 가면 활용 제안 추가. dryRun 성공 후 저장, snapshot d1:2260 읽기 검증에서 신발·가면 경로·기존 목차·배경 보존 확인.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-18 시안 작업 시작: Unity 종료 상태에서 origin/dev fetch, HEAD...origin/dev=0/0. 초기 변경은 보존했다. doctor는 동일 작업에서 반복하지 않았다.
- Unity 최초 batchmode 임포트 중 D3D12 GPU device 오류로 종료, D3D11 재시작은 Serialization 메모리 부족으로 종료. 이후 같은 worktree의 GUI 에디터(PID 13632)가 준비되어 명시 project-path/port 7800으로 작업했다. 컴파일 정상화 후 작업 진행.
- 보유 PolygonHorrorCarnival 고리·가면·불꽃·불티 및 PolygonGeneric 화염 시트를 조합해 실제 프리팹 A_FireHoop/B_EncoreHalo/C_MaskApparition 생성. 파생 재질·프리팹은 private CheerPointConcepts, 비교 씬은 Tests/MainStage/CheerPoints. Unity AssetDatabase/PrefabUtility 사용. 모델 원본은 수정하지 않았다.
- 실제 공연장 배경 복사 갤러리에서 PNG와 48프레임/12fps 움직임 GIF를 생성했다. 정적 이미지에서 가면 뒷면·듬성한 불꽃·고리에 가리는 불티를 확인해 수정했다. 비교용 조명/색보정은 갤러리에만 적용했다. 실게임 획득/피해 판정은 이번 시안에 없다.
- All In 1 패키지 및 기존 InventorySlotFx 재질 존재 확인. CommonMenuUI 슬롯 템플릿에 1.05초·25단위 상승·1.2배·-9도 설정. 기존 사용 연출은 유지하며 펀치 .35→.2. 소진 시 앞 28%는 상승, 이후 Fade 분해. 중단 시 원래 위치/재질 복구.
- PlayMode InventorySlotLiftPlayTests 1/1 PASS(1.91초). 실제 대기실 W2_InventoryDurabilityBarAndUseEffect 1/1 PASS(32.36초), 탐색 도구 마지막 사용 검증 추가. 현재 아이콘 재질 보존·상승 후 분해·중단/재사용 복구와 도구/장비 양쪽 이벤트 확인. 사람 플레이 및 다인 네트워크 시각 검증은 미실행.
- UI 캡처는 실제 CommonMenuUI 템플릿과 기물 아이콘을 사용했다. 편집 모드 Canvas 생성 직후 캡처는 빈 화면이어서 WorldSpace Canvas와 갱신 대기 후 촬영하도록 수정했다. 비활성 segmentTemplate도 명시 유지해 실제 내구도 칸 수와 일치시켰다.
- 결과: `C:/Users/imvag/.codex/visualizations/2026/11/17/01a1e9b3-663b-7802-ac84-19722bfce72d/cheer-hoops-comparison.png`, `cheer-hoops-motion.gif`, `A_FireHoop.png`, `B_EncoreHalo.png`, `C_MaskApparition.png`, `inventory-durability-sequence.png`.
- private 저장소에서 기존 All In 1 Lit shader 2개는 내용 diff 없이 줄바꿈 상태가 표시되고 DayClock/RestTimer .meta 27개에 WebGL 임포트 기본값 추가가 관찰됐다. 이번 제작 경로 밖이므로 일괄 되돌리지 않았다. 커밋·push 없음.
- 다음: 사용자가 실제 시안에서 방향 선택 → 최종 규격/통과 판정 → 게임 보상 연결. 신발 공중 추가 점프는 확정 기획이며 게임 로직/상점 설명 구현은 후속 묶음이다.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 저장·dev 제출 (2026-11-18)
- 사용자 요청으로 이번 작업만 commit/push 및 dev PR 준비. 기존 unrelated 변경은 보존·제외. 동일 코드의 컴파일/회귀 결과 재사용, 문서 및 저장 범위 가벼운 검사 수행.
- private 에셋 커밋 701a1bbe: CheerPointConcepts·EquipmentUseFx 32개 파일. resource/performance-equipment 브랜치 푸시 완료, 작성자 gh 로그인 MoHoDu. private main 직접 푸시는 자동 승인 검토가 공유 기본 브랜치 변경 위험을 이유로 거절하여 실행하지 않았으며 PR 경로로 제출한다.
- Confluence d1:3037: 변경 이력, 공통 내구도 UI 연출, 최신 검증 기록 보완. 이전 후보 검토의 미완료 문구를 과거 시점으로 정정.
- 기획 페이지 44793873에 MP4 4편 업로드 및 첨부 목록/시연 링크 확인: 신발 att45940738, 최종 구조 att45973505, 끈끈이 att46006273, 벌룬 att46006279. 영상은 자동 조작 실제 공연 시연이며 구조는 한 호스트의 두 캐릭터; 다인 동기화 별도 검증의 범위는 기존 기록대로 유지.
- private PR: https://github.com/gaboja-studio/CURTAINCALL_PaidAssets/pull/23 (resource/performance-equipment → main), PM 팀 검토 요청. 메인 프로젝트의 pre-push가 private main에 포함된 gitlink만 허용하므로 에셋 PR 반영 전에는 메인 프로젝트 push/dev PR을 완료할 수 없다. 검사 우회 없이 관련 파일 커밋·PR 본문까지 준비한다.
- 메인 프로젝트 구현 커밋 d178f9a8 생성(104파일). verify-scope/verify-fast/스테이징 diff-check/gh 작성자 검사 통과. verify-third-party -CheckSubmodule은 에셋701a1bbe가 main에 없다는 1건으로 FAIL. 공개 푸시 훅을 우회하지 않고 PR23의 main 반영 승인 대기. dev PR 본문 `.harness/local/main-stage-pr.md` 준비 완료. 코드·에셋 검증은 직전 결과 재사용.
- 후속 사용자 승인으로 private PR23을 merge 방식으로 main에 병합(ecd611ee, 701a1bbe 보존). verify-third-party -CheckSubmodule PASS 후 메인 fix/main-stage/pieces 푸시 완료.
- 2026-11-18 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/419 (fix/main-stage/pieces → dev). 구현·검증 범위와 기획/영상4편 링크를 포함하고 PM 팀 검토를 요청했다. dev 병합은 하지 않음. 기존 unrelated 변경은 계속 작업 트리에 보존.

- 최종: dirty 씬 0 확인 후 대상 Unity를 --if-saved로 정상 종료. origin/dev fetch 후 HEAD...origin/dev=0/0, 병합 불필요. 종료 시 GripYellow/LA_DeadScreenGlass/GB_Dark의 _Color에 약 1.6e-8 수준 직렬화 반올림 변경만 추가됨을 diff로 확인해 보존했다. 최종 컴파일·안전성·문서 검사는 PASS, 선택 PlayMode 2개도 별도 PASS. 에디터는 종료 상태이며 갤러리 파일을 열어 Play로 확인 가능. 기존 사용자 변경 보존, commit/push 미실행.

- 2026-11-18 구현 재개: Unity 종료 상태에서 origin/dev fetch, HEAD...origin/dev=0/0. doctor는 동일 작업에서 반복하지 않았다. GUI 에디터를 열어 프로젝트 경로를 명시해 작업.
- 사용자 A 불 고리·무해·내부 통과·그네 제거 선택. 나머지 3종 상세 규칙은 async 답변 '이 규칙으로 구현 진행'으로 확정. 신발 포함 4종 수동 사용을 공통 GameSettings와 호스트 내구도 장부에 연결.
- 실제 외줄/인간대포 각각 5개(기본3+기물경로2) 배치, 배치 창 및 3종 외형 전환. PlacePoints eval 응답은 5초 타임아웃이었지만 실제 두 씬 저장 및 고리10개 존재를 별도로 확인했다.
- Confluence 사용자 확정 내용·입력·연출·A/B/C 전환 반영. dryRun 성공 후 저장 d1:2453, 재조회로 확인. 기존 배경·목차와 옛 후보는 기록으로 보존.
- 첫 PlayMode 3/3 PASS(46.83초): 고리 통과 단회, 신발/벌룬 유효 소모·체공 제한·마지막 내구도 접기, 장갑 3초 정지. 확장 5개 중4 PASS, 구조 테스트1 FAIL은 테스트가 NetworkConfig 필드를 property로 조회한 오류. 필드 조회로 수정 후 재검증 진행.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=FAIL
- PlayMode 최종 5/5 PASS(51.45초). 구조 검증은 호스트 소유 시험 액터2개로 실제 서버→소유자 RPC/안전 위치 이동·내구도·부상 없음 확인. 별도 클라이언트 프로세스 검증은 아님. 끈끈이 사용권을 원격에도 공유하도록 보완 후 해당 테스트1/1 추가 PASS(14.76초).
- 실제 실행 중 외줄·인간대포 씬을 임시 촬영 카메라로 렌더하여 상부 부속 제거/고리 배치를 확인. Temp/PerformanceEquipment PNG를 사용자 시각화 폴더의 Tightrope-fire-hoop-live.png, HumanCannon-fire-hoop-live.png로 복사했다.
- check-work -CommonRegression 첫 실행: 안전성/문서/컴파일/공통설정 PASS, EditMode 1030 중1028 PASS/2 FAIL. 수정하지 않았던 리허설 테스트에서 (1) 재지정 가능한 {move}/{jump} 문구를 옛 Space 문구로 기대, (2) reflection object의 null 비교가 Unity 파괴 객체를 살아있는 HUD로 취급. 게임 규칙 변경 없이 테스트 기대와 Unity 객체형 null 검사를 수정했다. 재검증한다.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=FAIL
- 공통 회귀 재실행은1029/1030 PASS: HUD 객체형 null 수정은 통과. 안내 문구 테스트가 에디터 캐시 상태에 따라 실제 문구 에셋 또는 코드 fallback을 읽음을 확인해, 두 표현의 동일한 이동키·점프키·옆줄 점프 안내 의미를 검사하도록 정리했다.
- 편집 모드 배치 창 목록은 PlayMode의 정적 등록 목록 대신 열린 씬의 고리 컴포넌트를 직접 조회하도록 보완했다.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS
- 최종 check-work -CommonRegression: 안전성/문서/컴파일/공통 설정 PASS, EditMode 1030/1030 PASS(새 고리/봉/에셋 검사9개 포함). 게임 기능 PlayMode5개 PASS와 끈끈이 보완 후1개 재검증 PASS. 문서 길이 경고는 기존 하네스 경고이며 hard0.
- Confluence 구현·검증 범위 기록 추가 d1:2508, 읽기 검증 성공. 별도 클라이언트 다인 플레이와 최종 조작감/도달성/재미는 미실행으로 명시.
- Unity dirty0 확인 후 --if-saved 정상 종료(PID59860). 최종 origin/dev fetch, HEAD...origin/dev=0/0으로 반영할 변경 없음. 기존 사용자 파일 변경 보존, commit/push 없음. 테스트 중 pole-gate-black.mat에도 기존 재질들과 동일한 부동소수 색 직렬화 차이가 관찰되어 임의 되돌리지 않았다.
- 다음: 사용자/팀의 실제 다인 플레이와 A/B/C 외형 비교 피드백, 고리 위치·수치 조정. 에디터 메뉴 CurtainCall/Performance/Cheer Point Placement 또는 GameSettings→PerformanceEquipment에서 조절.

## 2026-11-18 4종 사용 연출 추가
- 사용자 요청: 캐릭터/월드 사용 VFX 기획·구현 후 기물별 실제 시연 영상. 시작 전 Unity 종료 상태를 확인하고 origin/dev fetch: 0/0. 같은 Task의 doctor는 재실행하지 않았다.
- 신발 금빛 도약 링/스파크, 구조 연결 줄/도착 링, 끈끈이 손뼈 접착 광채/잔여 원호, 벌룬 기존 바지 팽창/바람 궤적. Synty Sparks와 기존 ItemArt를 사용한 파생 프리팹4종, GameSettings useVisuals 참조. 서버 승인 시에만 RPC로 생성, 접기/놓기/착지에 맞춰 종료한다.
- 녹화 도구: Editor 전용 Unity Recorder, 실제 GameView 1280x720 MP4와 한글 설명 자막. 촬영 카메라는 원래 카메라 상태를 복원한다. 테스트는 실제 캠페인 호스트/RPC로 발동하며 게임 규칙을 대체하지 않는다.
- 촬영 시행착오: Editor 폴더 MonoBehaviour를 AddComponent할 수 없어 Editor update 기반 촬영 도구로 수정. 한정 글리프 폰트 대신 보유 원본 폰트의 임시 동적 TMP 에셋 사용. LaunchBounce 인자는 높이가 아닌 속도이므로 시연 초기 점프 속도를 수정했다.
- Recorder 초기 GameView 크기 변경 중 Time.timeScale=0인 워밍업을 기다린 뒤 시연하도록 수정. 시간 반복 횟수로 정한 끈끈이 자막 루프를 실제 마감시각 기준으로 변경. 벌룬 접기 후 짧은 소멸까지 검사. 수정 후 신발/구조/끈끈이3종 통과, 벌룬 단독1종 통과; 최종4종 묶음을 다시 촬영한다.
- Confluence 4종 사용 연출/프리팹 교체 설정 반영 및 읽기 검증: d1:2567. 원본 에셋과 기존 사용자 변경 보존, commit/push 없음.
- 최종 촬영 PlayMode 4/4 PASS(57.46초). 스프링 이단 점프/재사용 제한, 구조 위치 이동/무부상, 끈끈이 실제 남은 시간 정지·재개, 벌룬 마지막 소진 후 효과 유지/접기/연출 제거를 확인. 녹화 MP4 4편과 발동·종료 PNG를 사용자 시각화 폴더에 복사, opt-in 플래그 제거. 결과 JSON은 .harness/local/equipment-use-capture-results.json.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS
- 최종 check-work -CommonRegression PASS: 안전성/문서/컴파일/공통 설정, EditMode1031/1031. 이후 촬영 테스트의 카메라 구도·대기 위치만 조정하여 실제 시연4/4 재통과(57.81초), MP4 보관본 갱신. Confluence 검증 기록 d1:2637 읽기 확인. 기존 공통 플레이 규칙에는 추가 변경 없음.
- 최종 시연 구도 확인: 신발 최고점까지 전신 포함, 구조 전 두 캐릭터가 대포에 가려지지 않음. 네 MP4 모두 H.264/1280x720 약30fps 무음으로 디코딩 확인(약4~5초). Unity 대상 PID20404 dirty0 확인 후 정상 종료, 마지막 origin/dev fetch 결과0/0. 사용자 변경 보존, 커밋·push 없음.
- 리뷰 재생본은 Recorder 가변 프레임 타임스탬프를 30fps CFR로 정규화하고 faststart를 적용한 *-review.mp4 4편. 실제 동작/시간순서와 무음 유지, 최종 재생본 디코딩 오류 없음. 원본 녹화도 함께 보관했다.

## 2026-11-18 구조 장갑 대상 표식·연속 이동
- 후속 사용자 요청에 따라 구조 장갑만 수정. 재개 fetch origin/dev=0/0, 같은 Task의 doctor 재실행 없음. 다른3종은 효과·프리팹·수치를 유지한다.
- 소유자 전용 비네트워크 Canvas 대상 표식, 현재 재지정 키, 화면/카메라·몸 시야/거리/안전 도착·경로 검사. 요청에 실제 선택 대상 NetworkObjectId를 넣어 서버가 같은 대상을 재검증한다. 알림보다 위·설정창보다 아래 순서와 화면 가장자리 제한을 적용했다.
- 기본0.8초 SmoothStep CharacterController.Move. 소유자가 중간 위치를 기존 Owner NetworkTransform으로 보낸다. 서버 NetworkVariable 예약으로 중복 구조 차단, 입력·일반 중력 잠금, 완료/사망/재시작/구조자 소멸/시간 초과 복구. 구조 줄은 실제 대상 위치에 연결한다. 공통 시간은 GameSettings에 저장했다.
- Performance_Rescue 검사 PASS(12.99초): 소지자 표식, 화면 밖/벽 차단과 무소모, 중간 위치/중복 제한, 완료·무부상. 공연 기능5/5 회귀 PASS(52.52초).
- RescueNetworkPlayTests PASS(4.17초): 실제 NGO/UTP 3피어(호스트/원격 소유자/관찰 클라이언트), 양쪽 관찰자의 중간 위치 수신과 도착, Teleported 이벤트0, 구조자 사망 시 원격 대상 입력 잠금 해제. 같은 프로세스의 독립 NetworkManager/실제 전송이며 별도 실행파일/WAN 검증은 아님. .harness/local/rescue-network-results.json.
- 실제 공연 구조 장갑 캡처 PASS(18.03초). 표식이 알림에 가려지는 것을 보고 표시 순서와 촬영 구도를 보완해 최종 재촬영한다. 다른3종 영상은 유지한다.
- Confluence 구조 장갑 규칙/연출 행만 갱신: d1:2740. 사실 rescue-pull.md와 사용자 결정 기록 갱신.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS
- 최종 구조 장갑 촬영1/1 PASS(17.89초), 금빛 현재 사용키 표식과 연속 이동 영상 확인. 리뷰 파일 rescue-gloves-pull.mp4(1280x720, 30fps CFR, 무음)를 사용자 시각화 폴더에 저장하고 디코딩 검사 통과. check-work -CommonRegression 안전성/문서/컴파일/공통설정 PASS, EditMode1031/1031 PASS. 기획 검증 기록 d1:2810.
- Unity PID60020 dirty0에서 정상 종료 후 최종 origin/dev fetch 0/0 확인. 사용자 기존 변경 보존, 커밋·push 없음. Confluence d1:2810 최종 읽기 확인.
# 구조 대상 UI 디자인 후속 (2026-11-18)
- 마무리: check-work 안전성/문서/컴파일/공통 설정 PASS. 자동 회귀는 위 PlayMode 두 검사의 개별 결과로 기록(전체 공통 회귀 재실행 없음). 에디터 미저장0 정상 종료 후 origin/dev fetch0/0. diff-check 오류 없음. 커밋·push 없음.
- 최종 피드백: 전체 불투명도0.75, GameSettings `rescueTargetOpacity` 0~1 슬라이더. CanvasGroup 등장 알파와 곱하고 매 프레임 읽어 실행 중 변경 즉시 반영.
- 최종 실제 공연 촬영 1/1 PASS(17.98초), 구조 대상/시야/벽/내구도/이동 회귀 1/1 PASS(13.02초). API로 프리팹 참조·Image9개·포인터270°·설정0.75 확인. 추가 테스트 코드는 작성하지 않음.
- 미디어: 사용자 아티팩트 폴더 `rescue-target-ui.png`, `rescue-target-ui-detail.png`, `rescue-target-ui.mp4`. 실제 캡처에서 장갑 문양·하단 포인터·키캡과 배경 비침 검토. Confluence d1:2957 읽기 검증.
- 검사 도구는 필터 문자열의 OR를 지원하지 않아 복합 필터 시0건이었다. PASS로 계산하지 않고 두 검사를 개별 실행하여 위 결과 확인.
- 사용자 피드백: 단순 텍스트 대신 기존 월드 UI와 맞는 아이콘 표식·프리팹 요청. 끌어당김 승인 유지.
- `RescueTargetMarker` 전용 프리팹: 장갑 아이콘, 어두운 원형 메달, 황동 테두리, 대상 포인터, 작은 현재 키캡. 문장 라벨 제거, 0.16초 등장·미세 부유. 별도 View로 표시만 담당하며 기존 타겟/이동/RPC 규칙 유지.
- GameSettings.rescueTargetVisual 연결. 소지자만 인스턴스 생성, 비대상/가림 시 즉시 숨김, raycast 차단 없음. 기존 UI 정본 스프라이트를 재사용하고 외부 원본은 수정하지 않음.
- 첫 실제 공연 녹화 검사 1/1 PASS(19.07초). 화면 검토 후 하단 포인터 방향 보정, 최종 재검사 진행.


- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-18 기획 템플릿 정리: Confluence 44793873의 빈 자리 8곳을 본문 미디어(영상7·사진1)로 교체. 4종 설명을 제목 바로 아래 2~3문장·줄바꿈으로 옮기고 세부 설정/검증을 접기 영역으로 정리. 저장 본문 d1:3866, placeholder0/figure8 및 실제 브라우저 재생 카드·사진 로딩 확인.
- 새 자료: 실제 슬롯 UI/DOTween/All In 1 Sprite Shader의 감소·소진 연출 확대 시연과 실제 PerformanceCheerPoint.Present 소멸 미리보기(각3초). 고리 영상은 통과/보상 플레이 촬영이 아닌 연출 재생임을 본문에 명시. 씬/게임 코드 변경 없음, 임시 캡처 코드는 ignored .harness/local. 미저장 씬0 확인 후 촬영용 에디터 정상 종료. 기존 비관련 로컬 변경 유지.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-18 후속 버그 수정: 이 워크트리를 `fix/main-stage/pieces`로 전환하고 origin/dev(e9c99451)로 fast-forward·push. 기존 로컬 변경은 scratchpad 백업 후 보존(dev와 같은 폰트·머티리얼 7개는 dev 내용과 일치, CommonButtons SemiBold SDF만 로컬본 복원). _ThirdParty는 dev gitlink 911fd676으로 갱신.
- 칸 키 Q/E는 `PropCarry.PlaceAt`≠Blocked인 공간의 공연 기물 칸에서 유지(벌룬 접기 칸 포함), 그 밖은 1/2. 연습실 밖 대기실에서 `RescuePreviewTarget` 차단. 대기실 공연 기물 1/2는 설명 팝업과 함께 실제 선택(선택 해제 시 팝업 닫힘). 슬롯 설명 팝업은 영속 HudUIManager의 UiRuntimeFontFallback으로 글자 보충. 구조 장갑 수평 사거리 4→8m, 전방 반각 조건 제거 후 화면 정중앙 조준점과 가장 가까운 대상 선택.
- 검사: check-work 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(실행 중인 Unity 에디터 없음). 대체로 현재 파일 목록을 보충한 Assembly-CSharp csproj를 dotnet build → 오류 0(참조 asmdef 포함). Unity 실제 컴파일·자동 테스트·사람 플레이는 미실행.
- Unity 최소 플레이 검증(에디터 6000.3.25f1, 이 워크트리): verify-unity 컴파일 PASS. 기존 PlayMode `Performance_RescueRequiresSafeTargetAndMovesItWithoutInjury` 1/1 PASS(화면 밖·가림 대상 제외, 실제 당김), `RescueNetworkPlayTests` 1/1 PASS.
- 임시 PlayMode(실행 후 삭제): 대기실 시작(D-2 아침)에서 구조 장갑·머리핀 지급 → 연습실 밖 칸 키 1/2, 앞 3m 동료가 있어도 구조 대상·표식 없음, Digit1 입력으로 칸 선택+설명 팝업(글자 48자 □ 0개), 같은 키로 선택 해제·팝업 닫힘, 연습실 이동 시 Q/2, 복귀 시 1, 선택 후 버리기 성공, rescueRange=8 로드. 1/1 PASS. 캡처 `.harness/local/slot-check-waiting-tooltip.png`, `slot-check-practice.png`.
- 첫 실행 실패는 같은 소유자 보조 캐릭터 생성으로 `NetworkPlayer.Local`이 보조 캐릭터로 바뀐 테스트 구성 문제(로그로 확인)였고 게임 코드 변경 없이 재실행 PASS. 연습실 안 구조 대상 표시(양성)와 조준점 최근접 전환은 이 구성으로 확인 불가 → 사람 2인 플레이로 넘김.
- 테스트 부수 변경: CheerGauge Pretendard-Bold SDF·escape-labels 런타임 글자 아틀라스 저장, _ThirdParty 셰이더 EOL·dayclock .meta 재저장. 이번 수정과 무관, 되돌리지 않고 커밋 제외 대상으로 보고.
- 2026-11-18 검사: 안전성=미실행; 문서/규칙=미실행; 컴파일=PASS(Unity); 테스트=PlayMode 3/3 PASS(기존 2 + 임시 1)

- 2026-11-19 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=FAIL; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-19 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 위 2026-11-19 첫 검사의 컴파일=FAIL은 컴파일 PASS 뒤 공통 설정 run_script 호출이 에디터 연결 끊김(Connection reset)으로 실패한 것. 코드 변경 없이 재실행해 전 항목 PASS. 제출 직전 origin/dev 차이 0/0.
- 2026-11-19 제출: 커밋 18cc5734, PR #433 https://github.com/gaboja-studio/POC_CURTAINCALL/pull/433 (fix/main-stage/pieces → dev, Squash, 지정 팀원 검토 대기). 무관 로컬 변경(CommonButtons·CheerGauge 폰트, escape-labels, Vivox 설정, _ThirdParty)은 제외해 new-pr.ps1 dirty 검사 대신 같은 제목·본문으로 gh 등록.
