# Log

## 2026-11-04 재개용 저장
- 사용자 요청: 현재 작업을 브랜치에 보관하고 나중에 재개. 추가 검증 없이 바로 저장하라는 후속 지시에 따라 doctor/check-work/Unity/자동 테스트를 다시 실행하지 않는다.
- 최신 기준은 v5 원화와 실제 FBX 비교 렌더. v3 의상은 유지하며 v4의 큰 머리/손/체형은 제외한다. 원본 FBX10종/CharacterArt 실제 캡처가 형태와 재질 기준이다.
- 재개 순서: plan → todo → design-brief, private Concepts/JointSeams/fbx-proportions-{male,female}-v5.png 확인. M02 추가 목 봉제·F06 상완 길이·가려진 관절을 먼저 교정하고 사용자 원화 검토 후 다방향 제작 도면/Blender 파생 모델로 진행한다.
- private 원화·프롬프트·측정 자료는 resource/pursuit-joint-seams-v5의 0c34c960에 commit/push 완료(LFS6개/13MB). 공개 feat/pursuit/enhancement에는 문서와 재개 위치를 보관한다. private main 또는 공개 dev 직접 반영은 하지 않는다.
- 공개 push 훅이 private main에 없는 0c34c960 gitlink를 거부했다. 정책을 우회하지 않고 공개 gitlink는 기존 main의 aadf0c66으로 유지한다. 실제 private 작업 폴더는 v5 작업 브랜치를 유지하므로 상위 git status에 submodule 차이가 표시될 수 있으나 원화 변경은 private 원격에 저장돼 있다.
- 재개/다른 PC 복원: Assets/_ThirdParty에서 origin의 resource/pursuit-joint-seams-v5를 fetch/checkout해 0c34c960을 읽는다. 공개 저장소의 기본 submodule update만 실행하면 v5 대신 기존 연결로 돌아가므로 주의한다.

## 2026-11-04 원화 작업 및 체형 피드백
- 최종 검사: verify-knowledge OK(16폴더), 원본 FBX10/10 해시 동일. check-work -SkipUnity 1회: 안전성/문서 PASS, Unity 컴파일 SKIPPED(-SkipUnity), 자동 테스트 SKIPPED. 전체 완료 PASS로 표기하지 않는다. 기존 문서 길이 경고51건, private 미커밋 변경 안내가 남았으며 요청 없는 commit/push는 하지 않았다.
- doctor 1회: Unity 지정 버전 미설치, third-party 상태/기존 Task 중복/브랜치 미연결 실패. Task102를 현재 별도 작업 브랜치에 등록했고 원화 단계는 계속 진행했다. 기존 로그의 별도 저장 작업 기록은 보존한다.
- 착수 fetch 후 HEAD=origin/dev(dac0f1bd). 재개 후 외부 저장/동기화로 HEAD20b9062e, 최신 origin/dev 대비4 ahead/0 behind를 확인했다. Unity 프로세스 없음, 기존 작업 보존, 이번 작업에서 commit/push 없음.
- 실제 Unity 캡처와 공통 셰이더에서 세로 봉제 원인을 확인. 사용자의 혈흔 증가·때/긁힘·리본 제거·다른 단원/인형 세계관·기존 체형 유지 지시를 순차 반영했다.
- v3 의상 방향 평가를 유지하고 v4 과장된 머리/손/체형은 제외. CharacterArt 네 실제 캐릭터 캡처와 README를 읽고 v5를 직접 FBX 렌더 기반으로 재제작했다.
- Blender5.2.3 설치 확인. 별도 --background --factory-startup으로 기존 FBX10종을 import/render/측정만 했다. 원본10개 SHA256 일치, blend/FBX 저장·내보내기나 리그 편집 없음.
- 최신 결과: private Concepts/JointSeams/fbx-proportions-male-v5.png 및 female-v5.png. Source~에 실제 FBX 비교 화면/치수/본/해시/재현 스크립트/내장 image_gen 프롬프트 보관.
- 그림은 2D 검토 초안. M02 추가 목 봉제, 의상에 가려진 관절, F06 상완 결손 길이 등 생성 오차를 design-brief에 명시했다. 기존 FBX를 원화의 과장에 맞춰 변형하지 않는다.
- 다음: 사용자 외형 검토, 관절 배치·다방향 도면 정리 후 Blender 파생 제작. Unity 코드·프리팹·셰이더·게임 동작 변경 없음.

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-04 작업 보관
- 사용자 요청으로 현재 글꼴 에셋 변경과 Task 문서를 feat/pursuit/enhancement에 보관한다. 원격보다 앞선 추격자 수정 이력도 함께 push한다.
- 사용자 지시로 doctor/check-work/Unity/자동 테스트는 실행하지 않는다. Git 저장·업로드 보호 훅은 유지한다. 관절 봉제선 작업 자체의 완료를 뜻하지 않는다.
- 다음 작업: 최신 origin/dev에서 통합 WaitingRoom의 추격자·리허설 반영 상태 확인.


- 추가 보관: 비공개 CURTAINCALL_PaidAssets의 관절 봉제선 원화 3장과 .meta 4개를 resource/pursuit-joint-seams의 aadf0c66에 커밋·push(LFS 3개 업로드 완료). 개인 저장소의 기존 별도 작업 브랜치에서 미추적 상태였으므로 공개 저장만으로 보관되지 않았었다.
- 비공개 main 직접 push는 자동 승인 검토가 요청 범위 밖으로 거부하여 실행되지 않았다. 작업 브랜치 보관으로 완료했고 게임 gitlink는 변경하지 않는다.


## 2026-11-04 비공개 main 반영·게임 연결 갱신
- 사용자가 비공개 main 반영과 feat/pursuit/enhancement의 gitlink 갱신·push를 명시 승인했다. 이전 직접 main push 거부로 남았던 상태를 해소한다.
- 원화 3장과 메타를 담은 aadf0c66을 비공개 origin/main에 일반 fast-forward push 완료(a5747288 → aadf0c66).
- 최신 origin/dev 295b5bec를 feat/pursuit/enhancement에 병합했고, Assets/_ThirdParty 연결 대상을 aadf0c66으로 갱신한다. 공개 dev는 변경하지 않는다.
- 사용자 지시로 doctor/check-work/Unity 컴파일/자동 테스트를 실행하지 않았다. Git 저장·업로드 보호 훅은 유지한다.


- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-04 원화 검토 재개
- 실제 작업 폴더 D:/github/POC_CURTAINCALL의 feat/pursuit/enhancement=0eb8b97d에서 재개. 채팅 기본 worktree의 fix/pursuit/four-player-perf는 변경하지 않았다. 실제 작업 폴더의 공개/비공개 로컬 변경은 착수 시 없었다.
- Unity 미실행 확인 후 공개 dev/작업 브랜치와 private 작업 브랜치를 fetch했다. origin/dev는 이미 HEAD에 포함(7 ahead/0 behind), 공개 작업 원격과 HEAD 동일. merge commit 없음.
- private는 aadf0c66 detached에서 resource/pursuit-joint-seams-v5=0c34c960으로 전환/원격 일치 확인. 공개 gitlink는 그대로여서 상위 status의 Assets/_ThirdParty 차이는 예상된 상태다. private 파일 변경 없음.
- doctor는 채팅 기본 worktree에서 1회: third-party 미초기화/Task ID 중복/Unity 지정 버전 미설치 FAIL, Editor 미연결/skill links/gh 경고. 실제 작업 폴더는 private 복원 완료. start-work는 기존 Task102 ID 중복으로 실패하여 사용자가 지정한 정확한 Task 폴더/브랜치를 직접 확인하고 계속했다. 중복 Task 정리는 이번 범위 밖이다.
- v5 남4/여6, 실제 FBX 렌더, CharacterArt 네 실제 캡처/README 대조 완료. FBX10/10 해시 일치. M02 목 봉제/F06 상완 길이/가려진 관절 및 재질 교정 의견을 design-brief에 추가했다.
- Blender MCP 5.2.3 연결 확인. 저장되지 않은 기본 Cube/Camera/Light 씬을 조회만 했고 수정하지 않았다. 모델/리그/Unity/이미지 변경 및 commit/push 없음.
- 다음: 사용자의 원화 검토를 반영해 관절/결손/재질을 교정하고 다방향 도면으로 정리한 후 3D 제작한다.
- 재개 검사: check-work -SkipUnity -NoRecord 1회. 안전성/문서 규칙 PASS, 문서 길이 경고50건/hard0. Unity 컴파일·자동 테스트 SKIPPED. 프로세스 exit1 반환(출력상 FAIL 단계 없음)이므로 전체 PASS로 표기하지 않는다.

## 2026-11-04 v6 원화 제작
- 최신 dev fetch 후 cc21a5c7의 추가2커밋(인간대포)을 --no-commit --no-ff로 충돌 없이 반영했다. Unity 미실행, 기존 Task 미커밋 변경 보존. 사용자 commit 금지에 따라 공개 저장소는 merge 미완료 상태(MERGE_HEAD 있음)이며 원격/HEAD는 그대로다.
- doctor는 같은 원화 작업에서 이미 1회 실행했으므로 반복하지 않았다. 내장 image_gen으로 v5/FBX/실제 플레이어 캡처를 입력하여 v6 남4/여6를 생성했다. 남성 M03 왼고관절 가독성만 추가1회 교정했다.
- M02 목 봉제 제거, F06 오른상완 유지·전완/손 결손, F02 오른어깨/F04·M03 왼고관절 노출, 덜 얼룩진 인형 바탕을 시각 확인했다. 기존 개체별 의상/팔레트/혈흔 차이를 유지한다.
- private 작업 브랜치에 fbx-proportions-{male,female}-v6.png + 고유 .meta 2개, Source~/generation-prompts-v6.json을 새 파일로 저장. v5/FBX/리그/Unity 에셋/Blender 씬은 수정하지 않았다. commit/push 없음.
- 다음: 사용자의 v6 외형 검토. 아직 정투상 제작 도면/3D 결과가 아니며 실제 관절 중심과 단면 치수는 후속 제작 때 FBX를 따른다.
- v6 검사: FBX10/10 저장 해시 일치. check-work -SkipUnity -NoRecord 1회, 안전성/문서 규칙 PASS, 경고51건/hard0, private 신규파일5개 보관 안내. 컴파일/자동 테스트 SKIPPED. 프로세스 exit1(출력상 FAIL 단계 없음)으로 전체 PASS 아님.

## 2026-11-04 v6 3D 및 표면 바리에이션
- 사용자 지시: 기존 애니메이션/추격 시스템에 바로 적용, 옷에도 때·마모·혈흔 바리에이션. 실제 작업 폴더/브랜치 유지, 최신 dev fetch 결과 cc21a5c7 동일. 기존 미완료 merge와 로컬 변경 보존, commit/push 없음.
- Blender MCP로 FBX10종을 기준으로 얇은 의상·패턴·얼굴 페인트·관절 봉제·개체별 혈흔을 제작. 사용자 기본 씬을 유지하고 별도 V6 씬/라인업과 pursuit-troupe-v6.blend를 private Concepts/JointSeams/Source~/V6에 저장.
- Unity TroupeV6 메시10개/공유셰이더/재질3개 생성, 기존 프리팹10개에 동일 GUID/본/클립으로 연결. 원본 FBX와 Reclaimed 원본 메시/애니메이션 파일 변경 없음. 이전 Reclaimed 목/보수 장식만 프리팹에서 제거. 기존 AdvancedWalk/Run 유지.
- PursuitAppearance.SurfaceState = Clean/Worn/Bloodied. 동일 메시/공유재질에서 property block으로 전환, 피부와 옷 모두 오염 가능. 개체별 seed/오염 부위 차이, CaptureCount 누적/혈흔 숨김 설정 연동. 신규 런타임 네트워크 동기화/랜덤 배정은 추가하지 않음.
- 원본과 Blender FBX import의 가중치 차이를 발견해 Unity 원본 신체 vertices/normals/triangles/bindposes/boneWeights를 그대로 재사용하도록 수정. 추가 의상만 원본 가중치에 맞춰 결합. scale100 스킨의 BakeMesh(useScale:true)로 동일 좌표계 검증.
- integration-verification.json: 모델10개×기존9클립×9표본=810, 원래 신체 표면 최대 오차0m, issues0. 프리팹 GUID/본 계층/애니메이션 참조 보존. 추가 의상은 정면 및 Walk/Run/Tear 시각 확인, 수치 검사가 미술 품질/모든 관통을 보증하지 않음.
- PlayMode PASS: PursuitSurfaceV6_SharedMeshVariantsCaptureStainsAndBloodToggle 1/1(26개 실제 추격자, 3상태,18프레임), 기존 PursuitBehaviour_GraphHearingChaseSearchAndCulling 1/1. 결과 JSON/Unity 캡처를 Source~/V6에 보관. 4클라이언트/실기 성능은 미검증.
- 환경: 실제 Unity6000.3.25f1 연결 확인. Unity CLI beta16으로 갱신. doctor는 같은 작업에서 반복하지 않았으며 기존 Task102 중복/start-work 제약은 그대로다.
- Unity 실행 후 글꼴 캐시 2개(Pretendard-SemiBold SDF, escape-labels)에 변경이 보인다. 임의로 되돌리지 않고 보존했다. 원화/모델 최종 미술 검토는 사용자에게 남긴다.
- 최종 check-work -NoRecord: exit0, 안전성/문서/Unity 컴파일 PASS. 전체 자동 테스트는 선택하지 않아 SKIPPED이며 별도 실행한 위2개 PlayMode만 PASS다. verify-knowledge OK(16폴더), git diff --check 통과, FBX10/10 해시 동일, 원본 .anim/.fbx/Reclaimed 차이 없음.
- 종료 상태: Unity WaitingRoom 씬 dirty=false, PlayMode stopped, Blender V6_Troupe_Lineup 열림/저장 확인. 사용자 기존 씬과 dev 병합 변경은 유지.
- 남은 환경 오류: PlayMode 종료/도메인 재로딩 근처에서 Unity 내부 Assert 'Internal error - unexpected guid mismatch' 2회(UTC10:57:27,10:58:32), stack 없음. 프로젝트 .meta19293개 검사에서 GUID 중복0, 추격자 프리팹/본/클립 참조 검증 정상. 원인은 확정하지 않았으며 콘솔 오류0이라고 보고하지 않는다. 이후 컴파일 PASS, 새 동일 Assert 없음. CLI의 과거 'editor status' 명령 불일치 로그도 남아 있어 flat editor_status를 사용했다.

## 2026-11-04 v7 6방향 원화 및 재질 중심 재작업
- 최신 지시: 모든 표정·머리카락 제거, 기존 마네킹 얼굴, 개체별6방향 원화 후 재작업, 완성형 옷 대신 꿰맨 천 파편. 사용자는 재질 중심 구현 여부를 AI 판단에 맡겼다.
- 실제 작업 폴더/공개·private 브랜치 유지. origin/dev fetch cc21a5c7 동일, 기존 MERGE_HEAD/인간대포 staged 변경/폰트캐시 및 v6 작업 보존. doctor는 같은 Task에서 반복하지 않았다. commit/push 없음.
- image_gen으로 M01–M04/F01–F06의 6방향 원화10장 생성 후 Blender 제작. 기존 FBX·플레이어 재질 참조, 각자의 관절/혈흔/팔레트와 결손 유지. 프롬프트와 원본6방향 참조 저장.
- 원본 UV가 좁은 곳에 겹쳐 새 UV가 필요했다. Blender 높은 폴리곤 천/실밥은 베이크에만 사용하고 런타임에서 제거. TroupeV7의 원본 형상+새 UV 메시10개/재질10개/텍스처30개/공유셰이더로 기존 프리팹을 교체했다. importer와 외형 builder 메뉴도 v7에 연결.
- 원본 vertices/normals/triangles/bindposes/boneWeights/클립을 canonical 매핑으로 보존했다. UV 경계 정점만 원래 속성 그대로 복제한다. F03/F04 베이크 프록시 weld로 각각2개의 미세 면이 사라져 importer 검사에서 실패했고, 원본 인덱스 목록으로 UV를 매핑해 해당4면까지 보존하도록 수정했다.
- 6방향 실제 모델 렌더와 Walk/Run/Tear 검토. 일부 천 조각이 측면으로 치우친 문제를 재배치/재베이크했다. 마지막 integration-verification: 10종/810표본/오차0m/issues0, 원본 삼각형 총10993개 유지.
- PlayMode PASS: PursuitSurfaceV7_SharedMeshVariantsCaptureStainsAndBloodToggle 1/1(13.89초, 실제26개 추격자/3상태/18프레임), PursuitBehaviour_GraphHearingChaseSearchAndCulling 1/1(23.78초). JSON은 Source~/V7. 최초 표면 검사는 Read/Write=false 원본 triangles 접근으로 실패했으며 GetIndexCount 조회로 수정 후 통과했다. 원본 에셋 설정은 바꾸지 않았다.
- Unity 내부 GUID Assert는 v7에서도 UTC12:00:38/12:01:05/12:02:11에 발생(stack 없음). 이전 동일 환경 증상이며 원인은 아직 미확정이다. 두 PlayMode 검사 결과/컴파일과 구분한다. CLI refresh 명령 불일치 오류는 AssetDatabase.Refresh eval로 해결했고 사용자 콘솔 이력은 지우지 않았다.
- Blender pursuit-remnants-v7.blend 저장, V7_Material_Lineup 열림/dirty=false. Source~/V7에 재현 스크립트/README/원화 링크/모델10종6방향/동작/재질 비교를 보관. Blender 베이크 원본과 실제 런타임 메시를 구분해 기록했다.
- 남은 사용자 미술 검토와 4인 렌더/성능은 별도이며 호스트 결과를 전체 완료 검증으로 표현하지 않는다.
- v7 최종 검사: verify-knowledge OK(16폴더), git diff --check 통과, FBX10/10 SHA256 동일, 원본 .fbx/.anim/Reclaimed 차이 없음. .meta19358개 GUID 중복0. 공개 HEAD0eb8b97d/MERGE_HEADcc21a5c7/private HEAD0c34c960 유지.
- check-work -NoRecord 1회: 안전성/문서/Unity 컴파일 PASS, 기존 문서 경고51/hard0. 전체 자동 테스트는 선택하지 않아 SKIPPED이며 별도 표적 PlayMode2개가 PASS다. Unity WaitingRoom dirty=false/PlayMode stopped, Blender v7 작업본 저장 확인. 재개는 Source~/V7/README.md부터 한다.

## 2026-11-04 배치별 혈흔 외관 선택
- 사용자 요청: 혈흔 정도로 마네킹 외관 다양화, 추격자 배치 에디터에서 개별 선택. 기존 로컬 v6/v7·폰트캐시·인간대포 병합 변경 보존. 같은 Task이므로 doctor 반복 없음, commit/push 없음.
- origin/dev fetch 결과15702992로 갱신(추가59파일). WaitingRoom dirty=false/Play stopped 확인 후 정확한 프로젝트 에디터만 종료하고 cc21a5c7→15702992 바이너리 diff를 git apply --index로 충돌 없이 통합. 대상 index가 dev와 동일함을 검사했다. 이전 미완료 MERGE_HEADcc21a5c7는 유지하며 저장 요청 시 병합 이력을 마무리해야 한다. reset/stash/discard/commit 없음.
- 환경: Pipeline quit 명령은 EditMode에서 DontDestroyOnLoad 호출로 실패. 지연 eval 종료가 처리되지 않아 EditorApplication.Exit(0) 직접 호출 후 PID41004 종료 확인. Unity가 Temp를 지워 dev 패치를 시스템 임시 경로에 재생성했다. Unity6000.3.25f1로 같은 프로젝트를 재개, 이후 콘솔 오류 조회는 비어 있다.
- SurfaceState 기존 직렬화 값 Clean0/Worn1/Bloodied2 유지, LightBlood3/HeavyBlood4 추가. 혈흔 농도0/.25/.6/1에 따라 단순 붉은 틴트 대신 실제 얼룩 면적·깊이를 바꾼다. 천 조각에도 얼룩/마모를 적용한다. 원본 메시/본/클립 변경 없음.
- PursuitPlacement에 Appearance(자동/프리팹/고정5상태), AppearancePattern(0~15) 저장. 기본 자동은 방 내부 배치 계층의 안정 해시 사용: 위치/회전·재실행 및 다른 복제 루트에서 일치. GetInstanceID/GetHashCode/랜덤 추첨/신규 패킷 없음. 기존26체의 기본 분포5/6/4/5/6을 기록했다.
- Actor.Initialize와 배치 미리보기가 같은 ApplyAppearance/RefreshSurface를 쓴다. 모델·재질 복제 없이 property block만 다르다. 포획 수 초기화는 선택한 기본 외관을 보존하며 피 숨김은 기본/포획 혈흔 모두 가린다.
- 배치 창 선택/순찰 탭에 한국어 외관 선택·얼룩 무늬·현재 자동 외관·선택 외관 가까이 보기 추가. 씬 저장/재열기·Undo/Redo를 지원한다. 기존 타입/크기/위치/순찰/활성 단계 변경 없음.
- 검증 PASS: EditMode PursuitAppearanceAuthoringTests3/3(1.68초), PlayMode PursuitSurface*2/2(29.52초). 실제26체/5상태/30프레임, 7선택지의 복제 초기화/포획 reset35프레임. Source~/V7에 결과 JSON과 혈흔5단계/무늬 비교 PNG 저장. 실제4클라이언트 성능은 미검증.
- computer-use 앱 접근 승인이 시간 초과하여 창 스크린샷 검토는 생략했다. 승인 거부로 판정하거나 우회하지 않았다. Unity API로 배치 창 열림/Mannequin_Male_02 선택/scene dirty=false 확인. 재질 렌더와 미리보기 기능 검사는 완료했다.
- 사용 경로·선택지·제약은 appearance-editor.md, 구현 사실은 appearance-source.md에 갱신했다.
- 최종 check-work -NoRecord 1회 exit0: 안전성/문서/Unity 컴파일 PASS, 경고52/hard0(새 dev의 기존 Task097 로그 길이 경고1 추가). 전체 자동 테스트 SKIPPED, 별도 대상 EditMode3개/PlayMode2개 PASS. verify-knowledge OK16폴더, diff --check 통과. 기존 WaitingRoom 씬 dirty=false, 씬의 변경은 가져온 dev 변경뿐이다.

## 2026-11-04 관절 봉제 보강 및 독립 외관 레이어
- 사용자 요청: 관절 실밥을 더하고 피부/봉제 → 때·마모 → 혈흔을 함께 쌓아 표현, 각 오염 정도를 독립 조절. 현재 실제 프로젝트 D:/github/POC_CURTAINCALL, 공개/private 브랜치와 기존 미완료 merge·staged 변경·폰트 캐시 보존. fetch한 dev15702992가 이미 반영된 값과 같아 추가 통합 없음. 같은 Task이므로 doctor 반복 없음, commit/push 없음.
- 사용자가 직전에 저장한 WaitingRoom.unity 변경을 보존했다. 작업 전후 SHA256 1DBEF27628C795E7BC212DE6C7667FAE1401CEDBCC778EE1623C9FFD63095BD6 동일. 에디터 PlayMode stopped, WaitingRoom dirty=false 확인.
- Blender에서 10종의 지정 관절에 교차 실밥과 양끝 바늘 자국을 추가했다. 실제 표면에 붙는 568개 베이크 원본 실밥을 기존 V7 canonical corner UV에 재베이크했다. LayerTextures에 Albedo/Normal/Surface 각10장, 기존10개 재질의 텍스처 참조만 갱신했다. 원본 형상·결손·UV·본·가중치·애니메이션·런타임 메시·프리팹은 이번 변경에서 수정하지 않았다. 이전 V7 텍스처와 Blender 작업본도 남겼다.
- 한 공유 셰이더에서 기본 피부/봉제, 때/마모, 혈흔 순서로 합성한다. 때와 마모 양은 각각 독립이며 혈흔과 무늬 seed도 분리했다. 별도 메시/재질 슬롯을 추가하지 않는다. 피 표시 설정은 혈흔층만 숨긴다.
- 배치 창 선택/순찰 → 마네킹 설정 → 외관 레이어에 때 정도/마모 정도/혈흔 정도(0~1), 때·마모 무늬/혈흔 무늬(0~15), 기본 조합 복원 추가. 기존 enum·저장 선택은 CustomSurfaceLayers=false로 그대로 읽는다. 직접 조절 시 나머지 현재 값을 보존하며, 저장/Undo/Redo·미리보기·실제 생성·복제 초기화에서 같은 적용 경로를 사용한다.
- EditMode PursuitAppearanceAuthoringTests 5/5 PASS(3.01초): 저장/재열기/Undo, 기존 선택, 양/무늬 독립, 공유 메시/재질, 조합별 피 숨김. PlayMode PursuitSurface* 3/3 PASS(37.44초): 실제 호스트 추격자/로컬 복제 객체, 포획/리셋·피 표시 연동, 기존 선택 및 새로운 독립 층. 실제4클라이언트 성능 검증으로 해석하지 않는다.
- 실제 Unity 재질 렌더로 층별 비교와 봉제10종·10종6방향 자료를 생성했다. 때+마모만 있는 화면과 혈흔 추가 후 피를 숨긴 화면은 313600픽셀에서 총 RGB 차이0이다. Source~/V8에 재현 스크립트·Blender 파일·이미지·검증 JSON·README 저장, appearance-editor.md와 지식 문서 갱신.
- 2026-11-05 최종 check-work -NoRecord 1회 exit0: 안전성/문서/Unity 컴파일 PASS, 문서 경고53/hard0(이번 Task 로그 길이 경고1 추가). 전체 자동 테스트는 SKIPPED이며 별도 대상 EditMode5개/PlayMode3개 PASS. verify-knowledge OK16폴더, 공개/private diff --check 통과. 현재 Unity 콘솔 오류0, 테스트 종료 시 기존 WorldLabels·그림자 관련 경고는 남아 있다.
