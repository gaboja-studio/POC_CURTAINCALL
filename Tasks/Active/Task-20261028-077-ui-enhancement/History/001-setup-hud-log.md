# Log

## 2026-10-28 작업 환경 준비
- 요청: UI 고도화 브랜치 환경 준비 및 feat/game-system-m2 병렬 워크트리 생성. 담당 MoHoDu.
- 시작 실제 상태: dev `1bb84ab1`, 변경 없음. M2 PR #227 병합 확인. 현재 폴더의 Unity는 실행 중이며 조작·재실행하지 않았다.
- UI: `D:/github/POC_CURTAINCALL`, 새 브랜치 `feat/ui-enhancement`, Task-077. origin/dev 기준 생성 후 잘못된 상위 브랜치 추적을 해제했다. UI 기능·디자인 구현은 미실행.
- M2: `D:/github/github-worktrees/POC_CURTAINCALL/feat-game-system-m2`, 기존 브랜치·HEAD `4d90f674` 유지, 기존 Task-076 plan·todo 확인. 다른 워크트리는 보존했다.
- 기본 worktree.ps1은 gitlink가 있는 새 워크트리를 차단하므로 Git worktree add 후 서브모듈을 별도 준비했다. 비공개 에셋은 지정 SHA `65ed2de61bd660592d525a1fec538f564205f60d`로 배치했다.
- Git 객체는 기존 저장소를 reference로 사용하며, M2 서브모듈 로컬 lfs.storage는 기존 다운로드 캐시 `D:/github/POC_CURTAINCALL/.git/modules/Assets/_ThirdParty/lfs`를 사용한다. 기본 LFS 캐시로 상태 조회 시 파일 이동 Access is denied가 발생하여 캐시 위치를 명시했고 이후 git status 정상·변경 없음 확인. 에셋 작업 디렉터리는 독립적이다.
- 바이너리 씬 EOL 오탐 방지용 info/attributes를 새 서브모듈 Git 디렉터리에 초기 checkout 전에 준비했다. 추적 에셋·포인터 수정 없음.
- 사전 doctor -SkipEditor: fail 0, warn 1(skill-links 없음). setup-links.ps1은 Windows 심볼릭 링크 생성 관리자 권한 부족으로 실패. M2 링크 생성은 선행 실패로 생략. 원본 Harness 지침은 읽을 수 있으며 Git 작업에는 영향 없다.
- M2 Unity는 실행하지 않았다. Library·Temp·Obj·Logs·UserSettings를 복사하거나 연결하지 않았다. 첫 실행 시 독립 import가 필요하다.
- commit/push·원격 게시·구현 테스트는 미실행. 다음: UI 변경 범위를 정하고 각 폴더에서 별도 세션으로 진행한다. 공통 UI 파일·씬 중복 수정은 조율한다.

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-28 HUD 배치 변경
- 사용자 이미지 기준 공연 C2 제한 시간은 표시만 숨겼다. 기존 시간·불 추격·종료 판정은 유지한다. C3 진행도 우상단은 유지한다.
- 공통 공연 씬 C1 호응·공통 대기실 씬 W1 시계 위치를 좌상단 (48,-24)로 수정했다. 원형 하루 시계는 기존 W1 위치를 이어받는다.
- 공통 메뉴 씬 인벤토리를 우하단 (-48,28)으로 이동하고 2칸·1/2 키·빈손 안내로 변경했다. 기존 기물 소지 설정 기본 2개는 변경하지 않았다.
- `BaseGameSettings.asset`의 HUD 배치 → 균형/추락 게이지 위치: 1안 하단 중앙 (0,28, 기본), 2안 상단 중앙 (0,-40). `PerformanceSceneBinding`이 실행 중 변경을 읽는다.
- `NotificationTextLayout`은 상단 배너를 중앙 (0,-40), 외줄 2안일 때 게이지 아래 (0,-140)로 정렬한다. 오른쪽 토스트·상호작용 안내는 위치를 유지한다.
- 기존 정본·비공개 제작 원본은 수정하지 않았다. 에디터에서 실게임 공통 씬·외줄 씬 배치를 저장했다. 인간 대포 씬은 배치 대상이 없어 실질 위치 변경은 없으며, 에디터 재저장에 따라 이미 삭제된 코드 필드의 직렬화 잔여만 정리됐다. 외줄 씬에도 동일한 잔여 정리가 포함된다. 실제 PerformanceSceneBinding.cannonFires 참조 2개는 보존 확인했다.
- 시작 doctor FAIL: 기존 private 미추적 `curtaincall_dayclock_v2/`를 보존했다. Unity import 후 같은 폴더의 `.meta`가 추가돼 private 미추적 항목은 2개다. 이 작업에서 private 파일 내용·gitlink는 변경하지 않았다.
- 에디터 대상 `D:/github/POC_CURTAINCALL`, Unity 6000.3.25f1 확인. 초기 Start 씬 clean. 첫 다중 씬 eval은 기본 5초 시간 초과·저장 없음 확인 후, 호출을 분리하고 timeout 60초로 실행했다.
- check-work: 안전성 PASS, 문서/규칙 PASS, 컴파일 PASS(up_to_date), 기존 문서 길이 경고 14건. 스크립트 자체 테스트는 선택하지 않았으며 아래 MCP 대상으로 따로 검사했다. verify-knowledge PASS.
- EditMode: HudLayoutTests 2/2 PASS(두 위치 왕복·상단 배너 간격·기본 설정), NotificationTextLayoutTests 3/3 PASS(줄바꿈·높이 복구).
- PlayMode: WaitingRoomHudPlayTests 2/2 PASS(실제 대기실·소지·중복 표시), PerformanceSceneBindingPlayTests 13/13 PASS(실행 중 설정 왕복·숨김·공통 씬 수명 포함). 비동기 호출은 HTTP 400을 반환했지만 test_status에서 각 실행 완료와 결과를 직접 확인했다.
- 관련 코드·씬은 M2 병렬 작업과 겹칠 수 있으므로 이후 통합 시 CommonInventoryUI/PerformanceSceneBinding/BaseGameSettings 및 공통 UI 씬 변경을 함께 대조한다. 외부 메시지·commit/push는 하지 않았다.
- 다음: 사용자가 실제 플레이에서 1·2안 가독성과 팝업 위치를 확인한다. 최종 플레이 경험은 미검증이다.

## 2026-10-28 추락 게이지 상하 순서 추가 수정
- 사용자 추가 지시: 상단 중앙 2안일 때만 추락 게이지를 균형 게이지 아래로 이동한다. 하단 중앙 1안은 기존 위쪽 배치를 유지한다.
- HudLayout에서 추락 트랙·채움 위치만 변경한다. 2안 y=-56/-60, 1안 y=13/9. 기존 폭·높이·참조·위험 비율과 깜빡임은 보존하며 설정 왕복 시 위치도 복원한다. private·정본 프리팹·씬 재저장 없음.
- doctor는 기존 private 미추적 항목으로 FAIL, 해당 파일은 보존했다. check-work 안전성·문서/규칙·컴파일 PASS, 기존 길이 경고 14건. git diff --check PASS.
- EditMode HudLayoutTests 2/2 PASS(트랙/채움 위치·크기·왕복 복원 포함). PlayMode 대상 1/1 PASS(실제 프리팹의 실행 중 상하 전환). 동기 HTTP 실행은 미지원으로 실행되지 않았으며, 비동기 전환 후 HTTP 400에도 test_status의 최종 성공 결과를 확인했다. 최종 외형 사람 확인은 남아 있다.

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
