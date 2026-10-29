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
