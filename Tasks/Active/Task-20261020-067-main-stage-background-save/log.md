# Log

## 2026-10-20

- PM 사용자의 직접 저장 지시로 이미 제작한 공용 공연장 작업을 등록했다. 번호는 new-task.ps1로 발급했고 기존 작업 브랜치 feat/main-stage-background에 연결했다. 임시 발급 slug의 새 작업 브랜치는 만들지 않았다.

## 2026-10-29 작업 재개

- 사용자 요청으로 C:/Unity/POC_CURTAINCALL을 feat/main-stage-background-submit으로 전환하고 원본 performance_background.unity를 Unity 6000.3.25f1에서 열었다.
- 기존 feat/exit-route/design-map 변경 4개는 stash 메시지 before-main-stage-background-resume-20261029로 보관. private 에셋의 이전 HEAD는 f513105073aa333f4f12c3eabd9047d15bcd4ebf, 현재는 제출본 b6cc29c7bf9cf7667d9130ca981cdc3760cfbee8로 맞췄다.
- 같은 브랜치의 기존 worktree는 다른 채팅 소유여서 연결하지 않았으며 변경하지 않았다. 현재 폴더만 동일 브랜치로 전환했다. 두 폴더에서 동시 커밋하지 않도록 주의.
- 에디터 종료 완료 전에 브랜치가 전환되어 escape-labels.asset 임시 저장의 대상 경로가 사라지는 오류 발생. 임시 파일을 C:/Users/robot/AppData/Local/Temp/curtaincall-before-background-20261029/escape-labels.asset에 보존하고 종료 후 재실행했다.
- 사전 doctor 결과: Task-20261028-077 중복으로 FAIL, CLI 버전/skill-links WARN. 브랜치 전환·씬 열기에는 영향 없음. 게임 코드/씬 편집 및 테스트는 수행하지 않았다.
- 다음: 사용자가 원본 공연장 배경 편집 진행.

## 2026-10-29 최신 dev 동기화

- 사용자 상시 지시: 작업 시작·재개 전에 origin/dev fetch 및 작업 브랜치 반영. AGENTS.md 협업 원칙에 기록.
- origin/dev c529c6fb를 --no-commit으로 병합. 통합 meta의 Task-066/067 행 충돌은 두 행을 모두 보존해 해결. 미해결 충돌 없음. 커밋·push 별도 요청 원칙에 따라 MERGE_HEAD와 스테이징된 병합 결과 유지.
- private 에셋은 dev gitlink 98904299967b4c29c1323cc23a6244a0de14457f로 맞춤. 배경 씬 존재 확인.
- 이전 작업 재개 로그는 background-resume-log-before-dev-sync-20261029 stash에서 복원, stash도 유지. 기존 탈출로 작업 stash 보존.

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=FAIL; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 검사 세부: 안전성·문서/규칙 PASS. 컴파일은 에셋 import 중 editor_status 30초 timeout으로 FAIL(실제 C# 컴파일 오류 여부 미확인). 자동 테스트 미실행. 병합 결과는 커밋 대기 상태.
- 최초 import 완료 후 performance_background 씬 열기 성공. Unity 창 제목 및 open_scene 결과로 확인.

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-29 철골·쇼 조명 동적 연출

- origin/dev 재조회 결과 c529c6fb로 기존 MERGE_HEAD와 동일. 기존 병합 대기 상태·탈출로 stash를 보존하고 작업했다. commit/push 없음.
- PerformanceBackgroundMotion 및 Editor 미리보기/설치 도구 추가. HeroSawGantry 상부 3개를 연결 부재 단위로 분리(이동 삼각형 15,768개). 원본 5개 메시 에셋은 보존하고 Dynamic/ 파생 메시로 고정/회전부를 나눴다. 재질별 삼각형 합계 동일 확인.
- 크레인 회전은 Y축 ±7/8.5/10도, 주기 85/98/111초. 고정 가로보·베어링·회전 칼라를 각 3개 추가해 축을 지지했다. 기존 기초/통로는 고정.
- 기존 실시간 Spot Light 7개(천장 6 + GantryWarmKey 1)는 약 48~66초, DustBeam 12개는 약 52~67초 주기로 부드럽게 왕복. 각기 위상차 적용. 입구 고정 스폿은 유지. 새 광원은 추가하지 않았다.
- 28_DynamicShow 선택 후 Inspector Preview로 편집 중 재생. 저장·선택 변경·Play 전환·재컴파일 시 원래 자세 복구. 저장 중 최대 회전 복구 오차 0도 및 scene dirty=false 확인. Play에서는 자동 재생, disable 시 rest pose 복구.
- EditMode 4/4, PlayMode 1/1 PASS. 최초 EditMode disable 검사에서는 일반 MonoBehaviour의 Editor 콜백이 실행되지 않아 실패했으며, 명시적 복구 EditMode 검사와 실제 disable PlayMode 검사로 나눠 검증했다.
- 누락된 스크립트/target 0, 3개 크레인 sample(25초)에서 모두 회전 확인. 전후 Scene 캡처로 철골 상부/빛기둥 방향 변화 확인. 비교 캡처는 추적하지 않는 Temp/MotionPreview/에 보관.
- check-work 첫 실행은 캡처 도구가 만든 Assets/Temp.meta로 FAIL. 생성한 빈 임시 폴더를 Editor에서 정리한 뒤 재검사: 안전성·문서·컴파일 PASS(기존 문서 길이 WARN). 자동 전체 테스트는 미실행, 위의 대상 5개 테스트를 별도 실행했다.
- 후속: 사용자 미감·속도·회전 폭 조정, 대상 PC 프레임 측정. 실제 Spot Light와 장식 빛기둥 track은 독립적이다. 최종 게임 경험/성능 검증으로 보고하지 않는다.

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-29 배경 전체 움직임 확장

- 사용자 피드백: 부분 연출로는 부족하므로 전기톱·안개 등 배경 전체가 동적으로 보여야 한다.
- 최신 dev e58b207f 수신. 사용자 승인으로 이전 병합을 0dc9ff5c, 추가 dev 반영을 3e727ff9로 커밋했다. 연출 코드/에셋 변경은 커밋하거나 push하지 않았다. private 포인터는 최신 dev의 80ff0ad7로 맞추고 배경 작업은 양쪽 background-motion-before-latest-dev-20261029 stash로 보존·복원했다.
- 복원 시 CommonButtons 폰트 atlas의 자동 임포트 변경(1byte)과 dev 원본이 충돌하여 최신 dev 원본을 유지했다. 자동 변경본은 stash에 보존했다. 씬/프리팹 충돌 없음. 최신 dev 임포트가 만든 resttimer PNG .meta 변경은 별도 보존, 수동 편집하지 않았다.
- 톱날 15개를 평면·원형 부재 분석으로 찾아 치형·표면 디테일까지 독립 회전축으로 분리. 기존 크레인 상부의 톱은 해당 크레인을 따라간다. 회전 속도 48~84도/초, 교대 방향. 원본 기계 메시 관련 삼각형 127,800개 총합 보존 및 15/15 회전 확인.
- Atmosphere 50개: 안개 14개(스모그 11 + 심부 베일 3)의 이동/난류, 깃발·천 22개와 케이블/현수물 14개 흔들림. FlowingVenueHaze 파생 셰이더/재질을 만들고 원본 ScenicHaze 재질은 보존했다.
- 작업등 44개는 10~18%, 발광 부재 84개는 20% 내 밝기 맥동. Surface 98개 중 14개는 안개 시간 제어. 추가 실시간 광원 없음.
- 파티클 18개: 공간의 먼지/불티 12, 큰 톱날의 그라인딩 스파크 6. 스파크 위치를 각 톱날 평면의 외곽 접점에 맞춤. Collider/Particle collision/추가 광원 없음.
- AmbientSetup.Build 호출 응답은 5초 제한으로 timeout이었지만 후속 읽기에서 전체 설치·씬 저장 완료를 확인했다. 재실행하지 않았다. Shader 오류 0. Scene 렌더로 안개/현수물/전기톱/입자 구성 확인.
- 미리보기 저장 검사: 누락 target 0, 위치/회전 복구 오차 0, 파티클 382개→0개, 씬 dirty=false. MaterialPropertyBlock의 기존 값 복구 및 Play disable 시 조명/자세 복구 확인.
- EditMode 7/7, PlayMode 1/1 PASS. check-work: 안전성·문서/규칙·컴파일 PASS(기존 문서 길이 WARN); 전체 자동 테스트는 미선택, 위 대상 8개를 별도 실행했다.
- 다음: 사용자가 전체 미리보기에서 속도/밀도/흔들림 범위를 검토. 대상 PC 실제 프레임 성능과 최종 미감은 미검증.

## 2026-10-29 저장 후 탈출로 전환

- 사용자 저장/브랜치 전환 요청. Editor가 이미 종료되어 Pipeline 미연결인 것을 프로세스 조회로 확인했다. 직전 저장/복구 검사와 컴파일·대상 8개 테스트 결과를 재사용하고 verify-fast PASS를 확인했다.
- 공연장 원본 씬·Dynamic 파생 에셋을 private resource/performance-background-motion의 aead6c7e로 커밋·push 완료. 무관한 resttimer .meta 임포트 변경은 제외했다.
- 공개 코드·Task 문서·지식 및 private gitlink를 현재 공연장 브랜치에 저장한 뒤 feat/exit-route/design-map으로 이동해 origin/dev e58b207f를 반영한다. 기존 폰트·프로젝트 설정 변경과 탈출로 stash는 보존한다.

## 2026-10-30 공연장 작업 복귀

- 사용자 요청으로 feat/exit-route/design-map에서 feat/main-stage-background-submit(6bbd91d9)으로 전환. 탈출로 미커밋 7개는 exit-route-preserved-before-background-resume-20261030 stash로 보존했다.
- 열린 escape_route 씬 dirty=false를 확인하고 Unity 정상 종료 후 전환했다. 최신 origin/dev cf8bcab4를 --no-commit으로 충돌 없이 반영했다. 별도 저장 지시가 없어 병합 커밋·push는 하지 않았다.
- private 실제 HEAD와 병합 gitlink 모두 94c98cb9 일치. 사전 doctor는 기존 Task ID 077/078/079 중복으로 FAIL, CLI/skill-links WARN. 브랜치 전환 영향 없음.
- 검사: 브랜치·충돌 없음·private 포인터·공연장 씬 존재 확인. 게임 구현 변경이 없는 브랜치 복귀이며 Unity/테스트를 다시 실행하지 않았다. 다음은 원본 공연장 배경 작업.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-30 깃발 깜빡임

- 실제 메시 확인: HangingVelvet의 앞뒤 정점 좌표가 같고 법선은 반대(8정점/4삼각형). 삼각형 대각선이 달라 완전 일치 중복 검사에서는 0으로 나왔으나 면은 같은 평면을 덮는다. Cull Off 재질이 두 면을 중복 렌더링해 깊이/명암 경쟁이 발생했다. Editor.log의 Vivox 403은 깃발 렌더와 무관했다.
- Editor API로 Velvet_FadedCrimson.mat, OldCircusVelvet.mat의 _Cull만 0→2(Back) 변경·저장. 두 재질을 쓰는 Renderer 24개에서 값 일치 확인. 메시·흔들림·조명 변경 없음. private diff는 두 재질 각 1줄 변경이다.
- 수정 전후 근접 렌더 및 앞/뒤·3초 흔들림 자세 확인. 뒷면도 계속 보이며 겹친 면 렌더를 제거했다. 비교 캡처는 Temp/FlagReview/에 보관하고 임시 에셋은 정리했다. 씬 dirty=false.
- check-work: 안전성/문서/컴파일 PASS, 기존 문서 길이 WARN. 재질 값 수정이므로 자동 테스트는 추가/재실행하지 않았다. 커밋·push 및 기존 dev 병합 상태 변경 없음.
## 2026-10-30 접합 재배치·기둥 밀도 조정

- origin/dev cf8bcab4를 재조회해 기존 MERGE_HEAD와 같음을 확인했다. dev 병합 대기·탈출로 stash·재질 Cull 수정과 원본 씬 GUID를 보존했다. commit/push 없음.
- ConnectionRefit 편집 도구를 추가했다. 천장을 289.7 내려 외벽 상단 y245와 맞추고, 통로/난간 181개를 발판 경계·벽 연결점으로 정렬했다. 관람대 받침·좌석 9세트, 톱 15개 축받이, 천장 조명 6개·장식 빛기둥 12개 지지대, 현수물 22개의 상단 힌지를 연결했다.
- 크레인의 원래 회전 메시에서 고정 부재 282개를 분리하고 고정부와 회전 베어링을 맞췄다. 기존 원본 메시 에셋은 유지하고 Connections/와 Dynamic/의 파생 에셋을 사용했다. 저장 시 원래 자세로 복구한다.
- 겹친 보조 플랫폼 1개와 부속 118개, 완전히 묻힌 거의 동일 평면 바닥 조각 3개를 제거했다. 최종 층 높이 차 8 이내·평면 면적 25% 초과 플랫폼 중첩 감사 결과 0쌍이다. 이는 플랫폼 외곽 검사이며 모든 임의 메시 교차를 없앴다는 뜻은 아니다.
- 초안은 모든 기둥을 땅까지 연장해 과밀해졌고, 후속 사용자 요청에 따라 자체 초안을 Editor에서 원본 씬 스냅샷으로 교체했다. 그 뒤 플랫폼별 장기둥/격자를 제거하고 8개 벽 거더·376개 짧은 캔틸레버·512개 벽 연결 새들을 설치했다. 기존 장기초는 비활성으로 보존했다.
- 사용자는 이 간소화안도 너무 허전해 최초의 분위기·압도감이 사라졌다고 지적했다. 최종안은 원본 외곽 샤프트의 철골/발광 4개 메시를 복원하고, 개별 발판 기둥 대신 12개 공용 대형 타워·8층 깊은 벨트 트러스·368개 발판 전달보로 겹겹의 실루엣을 되살렸다. 전달보 끝을 벨트 하현재에, 벨트는 타워 횡보에 실제 접하도록 보강했다. 공용 구조의 신규 부재 9664개는 메시 2개로 합쳤으며 광원·충돌체는 추가하지 않았다.
- 사용자 미감 기준은 Decisions/performance-background-density.md에 기록했다. 최초 분위기가 기준이고 구체적인 타워 수는 구현안이다. 미감 최종 승인·성능 측정은 별도다.
- 시각 확인: 최초/간소화/밀도 복원 전경 비교, 좌우·후면·심부. 동작 검증: EditMode 8/8, PlayMode 1/1 PASS. 22개 힌지 양 끝의 0/5/20/45/90초 최대 오차0, 천장/벽 틈0, Missing Script0, 신규 Collider0, 중립 저장 dirty=false. 전체 프로젝트 테스트는 재실행하지 않았다.
- 도구 제약: 대형 초안 Build의 CLI 5초 타임아웃은 작업 중단이 아니었다. 상태 파일과 씬 저장을 확인한 뒤 진행했고 중복 설치하지 않았다. 비교 스크린샷은 추적하지 않는 Temp/ConnectionReview/로 옮긴다.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work 안전성·문서/규칙·컴파일 PASS. 기존 문서 길이와 미커밋 private 에셋 WARN, 전체 테스트 선택 안 함. 별도 대상 테스트 9개 통과. 최종 target 누락0·Surface116·중립 저장 dirty=false 확인, 공연장 씬을 열린 상태로 두었다.

## 2026-10-30 프레임·톱 스파크·상하 쇼 조명 확장 및 저장

- 사용자 요청: 크레인처럼 움직이는 프레임 추가, 회전 톱 스파크, 위아래 조명 추가 후 저장. save-work에 따라 작업 브랜치 commit/push까지 진행한다. 이전 접합·깃발 수정도 공연장 변경으로 함께 보존한다.
- 작업 전 doctor: 기존 Task 077/078/079 중복 FAIL, CLI beta13과 문서 beta15 차이·skill-links WARN. private 포인터 FAIL은 최신 origin/dev가 26d5009e로 앞선 상태였다. 기존 병합을 e112c80b로 저장한 뒤 최신 dev b19189eb를 fd15846c로 반영하고 private 26d5009e 위에 공연장 수정/새 에셋을 보존했다. 충돌 없음.
- PerformanceBackgroundShowExpansion 편집 도구와 ShowExpansion 파생 에셋을 추가했다. 공용 대형 타워 6곳에 고정 횡보·마스트·회전 베어링·카운터웨이트·보강 케이블을 두고 상부 프레임만 느리게 회전/상하 운동한다. 지상 기둥을 추가하지 않았다. 주기76~131초, yaw10~14도·pitch4.5~6도. 기존3개 포함 Crane9개.
- 프레임 끝단의 상향6개/하향6개 Spot과 보이는 빛기둥12개를 추가했다. 따뜻한 주황/차가운 푸른색, 제한된 느린 스윕과 약한 맥동. 추가12개는 그림자 없음·거리155로 제한. 기존 포함 ShowLights31개. 실제 타깃 PC 성능은 미측정이다.
- 기존 희박한 GrindingSparks6개는 비활성으로 보존하고 톱15곳의 실제 반경을 측정해 접촉 슈와 새 파티클을 연결했다. 회전 날이 아닌 고정 프레임에 방출기를 붙이고 World simulation으로 이미 튄 스파크가 프레임 회전으로 휘말리지 않게 했다. 연속14개/초+확률 burst18~32, 중력·감쇠·늘어진 불티. 먼지/불티12개 포함 Particle27개.
- Build CLI는5초 타임아웃을 반환했으나 뒤이은 씬/배열 검사로 완료·중립 저장 dirty=false를 확인했다. 중복 실행하지 않았다.
- 확인: EditMode8/8 PASS. 0/20/40/90초 샘플에서 새 프레임6개 pivot 오차0, 최대 회전14.535도, 잘못된 상향/하향 방향0. 스파크15개 모두 방출,1초 샘플236개. Missing Script0·추가Collider0. 중립 복구·저장 dirty=false. PlayMode 동작 구현은 이전 통과 결과를 재사용하고 이번에는 전체 테스트를 반복하지 않았다.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work 안전성·문서/규칙·컴파일 PASS. 문서 길이 WARN 및 private 미커밋 안내만 있었으며 전체 테스트는 선택하지 않았다. 비교 캡처는 Temp/ShowExpansionReview/에 보존하고 임시 PNG 에셋을 정리했다. private 에셋은 resource/performance-background-show-expansion의 df7890fd에 저장했다. 공연장과 무관한 ProjectSettings 자동 변경2개는 저장 범위에서 제외한다.
- private df7890fd를 origin/resource/performance-background-show-expansion에 push 완료. 공개 저장은 feat/main-stage-background-submit에서 공연장 코드·참조 포인터·Task/지식 문서15개로 한정한다. 기존 밀도/접합/깃발 수정도 포함하며 dev 직접 반영/PR 생성은 하지 않는다.
- 공개 pre-push가 private main 미포함 포인터를 거부했다. private main의 README 변경만 병합해 a1511645를 main에 정상 fast-forward push했다. 에셋 기하는 df7890fd와 동일하며 이 최신 포인터로 공개 저장을 마무리한다. 검사 우회/force push 없음.

## 2026-10-30 dev 추가 최신화

- 사용자 요청으로 origin/dev 88cbb574(인간 대포·트램펄린 모델/씬 교체)를 현재 공연장 브랜치에 --no-commit으로 반영했다. Pitfalls/index.md의 링크 추가 충돌1건은 공연장 깃발·Human-cannon 양쪽 항목을 보존해 해결했다. 씬/프리팹 충돌 없음.
- 공연장 에셋 a1511645는 dev의26d5009e보다 앞선 검증·게시된 커밋이므로 유지했다. 기존 ProjectSettings 자동 변경2개를 보존했다. 별도 저장 요청 없는 동기화이므로 이번 dev 병합은 커밋 대기로 두고 push하지 않는다.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 후속 사용자 제출 요청으로 dev 88cbb574 병합을 저장하고 feat/main-stage-background-submit → dev PR을 준비한다. 현재 에셋·코드와 일치하는 직전 안전성/문서/컴파일 PASS 및 대상 EditMode8개·기존 PlayMode1개 통과 결과를 재사용한다. ProjectSettings 자동 변경2개는 임시 보관 후 제출 뒤 복원한다.
