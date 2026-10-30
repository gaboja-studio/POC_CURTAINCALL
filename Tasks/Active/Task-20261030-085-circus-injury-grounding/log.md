# Log

- 2026-10-30: doctor 1회. 기존 Task ID 중복, sandbox의 submodule/auth/editor 조회 제한. 승격된 읽기로 실제 private 에셋과 Unity 6000.3.25f1 연결 확인. Unity 정상 종료 확인 후 dev fetch, 최신 341be9eb에서 fix 브랜치 생성. 기존 미저장 파일 보존.
- 원인: 기존 메시 최저점 검사는 손끝 접촉만으로 통과해 골반이 뜨는 자세를 놓쳤다. 2,016 표면 샘플과 수정 전 렌더를 기록했다. 실제 지지면(외발 발바닥 / 양다리 없음 골반)을 별도로 맞추고 팔 관통은 팔만 보정한다.
- Blender: FlightLand/TrampolineCarry/RopePull의 Cut1/2/3 총 9개 재제작. 외발 무릎/몸통 반응, 골반 접지·팔 굽힘, 외팔 비대칭 노력, 운반 반복 이음새 조정. 44액션을 포함한 편집용 cannon-motion.blend 갱신. 기존 FBX GUID·컨트롤러 참조 유지.
- 초기 집중 EditMode 9/9 통과(지지면 1,116시점 + 기존 44클립/네 아바타 회귀). 전환48개 실제 60fps 샘플에서 최대 골반 이동 약 .04m/프레임. 최종 클립 운반 PlayMode 1/1: 4캐릭터×3절단 단계 운반/점프/손잡이/착지 지지 확인.
- 착지 PlayMode 첫 검사 실패는 같은 대포의 새 쿨타임이 끝나기 전 다음 절단 사례가 탑승하려 한 테스트 준비 오류. ReadyToUse 대기를 추가. 결합 필터의 0-test 실행은 검사 성공으로 세지 않고 별도 필터로 실행한다.
- 최종 결과: EditMode 9/9, PlayMode 세 독립 검사 통과(운반12조합, 대포 착지3절단 단계, 외줄 구조4단계 양역할/취소/완료). `Source/editor-tests.json`, `play-before-cooldown-wait.json`의 운반 결과, `landing-play-tests.json`, `rescue-play-tests.json` 참조. 실제 다중 클라이언트 네트워크 지연은 이번 변경의 검사 범위 밖이다.
- 최종 표면 2,016샘플 중 지면 절단 756시점 전체 최저점 .00155~.00200m, 양다리 없음의 골반 지지 504시점 .00200m. 손끝만 닿는 통과 조건을 제거했다. 48전환 검사에서 최대 골반 이동 .04m/프레임 이하.
- 갤러리: floor top을 0m로 저장, 착지로 시작, 지면/공중/매달림 표시 추가. 실제 Play 16배우·240 구간탐색과 확대 검사 통과, 프레임 162→174 진행 확인. 양다리 없음 확대를 실제 스킨/절단면으로 확인. 새 재생 오류 0(기존 CLI 신/구 명령 호환 조회 오류 이력은 별개). 종료 시 갤러리 열림·저장됨·재생 종료, 테스트용 background/환경변수 복원.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 첫 check-work는 새 비교 PNG 6개의 대문자 이름 때문에 중단됨. 해당 파일명/출력 규칙을 소문자로 수정했고 기능 변경 없이 필수 검사를 다시 실행한다.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 완료: 별도 실행한 최종 PlayMode 통과3개를 원본 출처와 함께 `Source/play-tests.json`으로 집계. check-work의 테스트 SKIPPED와 분리해 기록한다. 최종 씬 dirty 0, ProjectSettings/private 에셋 diff 없음. 작업 브랜치에 저장했으며 commit/push는 요청하지 않아 실행하지 않았다.
- 후속 저장·제출 요청: Unity의 저장된 Start 씬/재생 종료 상태를 확인하고 에디터를 정상 종료한 뒤 origin/dev fetch. 최신 341be9eb와 동일해 충돌 없음. 구현 이후 코드 변경 없이 기존 컴파일·집중 테스트 결과를 재사용한다. 글꼴 캐시 3개와 Task083 PNG 22개는 보존하고 이번 커밋에서 제외한다.
- 2026-10-31: da497c34 저장 및 LFS 포함 push 완료. dev PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/250 . new-pr DryRun의 미저장 경고는 보존한 타 작업 파일에 해당하므로 save-work의 선택 staging 절차로 제출했다. 지정 팀원 검토 후 병합하며 이 작업에서는 병합하지 않는다.
