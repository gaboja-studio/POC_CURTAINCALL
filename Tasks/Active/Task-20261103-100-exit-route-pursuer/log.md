# Log

이전 복구·초기 완성 기록: [History/002-recovery-and-initial-completion.md](History/002-recovery-and-initial-completion.md).

## 2026-11-13 플레이 피드백: 콘크리트 뒤 경로·지속 추격

- 사용자6개 개선 요청에 따라 기존 feat/exit-route/pursuer에서 작업. 실수로 만든 feat/exit-route/pursuer-improvements는 기존 브랜치로 복귀 후 삭제했다. 작업 시작 전 Unity 종료·dev9a5baaa7 반영. 이번 개선의 커밋/push는 요청되지 않았다.
- 갈색 갤러리/구형 가림 외장은 비활성 보존. 74개 경로를 기존 콘크리트 뒤로 이동하고21개 서버 시각 기반 파괴구와 동일 재질 주변 벽을 생성했다. 원본 벽·에셋을 보존했다.
- 노출6.6/벽 뒤13m/s, 달리기 상태 유지·NavMesh 자동 감속 해제. 복도/대포/외줄 예고0.45/1.1/0.85초, 손 뻗기0.22초/회수0.3초/회복0.35초. 발소리 거리 감쇠·강도 조정. GameSettings 사용.
- 팀 출발5초 후 진행 팀 추격, 진행도+최근 공격6초 기억으로 타깃 재선정. 보스는 발사대 사용자를 우선 고려하며 막히면 측면 진행을 검사한다. 고정 공격선·호스트 접촉·보스8절단·케이블 출구 규칙 유지.
- 몸통 sweep/공격19선/플레이어74표본/4대포 기본 탄도 검사에서 새 막힘0. 마지막 도약 X332가 반출기와 충돌하는 것을 발견해 X350으로 변경, 실제 곡선121표본 충돌0. 시야 표본의 노출0은 모든 카메라의 비노출 보장이 아니다.
- 실제 호스트1인 구간 재현: 첫 등장→달리기→경고/손 접촉→EscapeCaptured·운반·리타이어, 벽 파괴1→재시작0. 전체 EditMode258/258 및 정리 후 추격자15/15 통과. 전 구간2~4인 실플레이는 미확인.
- 벽 절단 도중 새 콜라이더 bounds는 Physics.SyncTransforms 후 사용해야 한다. 표본마다 반복 절단하던 중간 산출물은 에디터에서 재생성해 정리했고 최종 생성 루트589 Transform이다. 마지막 점프는 몸통을 포함한 곡선 검사 필요. record-knowledge로 기존 배치 Facts 갱신, verify-knowledge OK.
- 제출 전 새 dev6커밋 및 가이드 hotfix를 발견했다. Unity 종료 후 WIP stash 보존→devc0c11212 fast-forward. 탈출로 루트 자식 목록1곳 충돌을 확인했고 사용자가 두 작업 보존 해결을 승인했다. 엔딩/체크포인트를 남기고 추격자1개를 연결해 해결. private16982ba2 반영. 최신 A/test 저장 및 경로/공격/탄도/도약 재검사 동일 결과.
- doctor 기존 중복 Task ID10개 실패, CLI/Pipeline 차이·기존 다른 worktree 경고. 초기 종료 요청은 재임포트 중 timeout이었고 재시도에서 정상 종료했다. 관련 stash·폰트 자동 변경을 버리지 않았다.

- 최신 dev 통합 후 전체 탈출로 EditMode258/258 재통과, 실패/생략0. 갱신 테스트 씬 실제 호스트22초206표본에서 run1·Warning/Strike/Grab·EscapeCaptured/운반 확인. 현재 콘솔 오류0. Review/improvement-verification.json에 범위와 수치 보존.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 최종 check-work 안전성/문서/컴파일/공용 설정 PASS. Base 메타데이터에 설명을 함께 적어 첫 실행이 중단된 것은 Base=dev로 바로잡았다. 자동 테스트는 별도258개 결과 사용. 검사 후 Unity 정상 종료→origin/dev 재fetch/ff, c0c11212 이미 최신·HEAD...origin/dev 0/0 확인. 커밋/push 없음.

## 2026-11-13 외부 에셋 적용·위협 행동 확장 진행
- doctor는 기존 중복 Task ID10개로 FAIL, CLI/Pipeline 및 기존 worktree 경고. 에셋 작업에 영향 없음. Unity 종료 후 origin/dev08600e05 fast-forward, 기존 미커밋 작업 보존.
- 다운로드된5패키지 경로를 _ThirdParty로 재매핑하고 GUID 중복을 보존해 임포트. Brute52/Mascot117/HumanBasic1/Boxer287/MonsterSounds1258항목. HumanBasic646항목 재사용. 데모 입력/이동/에디터 자동 설정 코드 및 데모 씬 제외, 원본 다운로드는 보존.
- Mascot3 원본은 Generic이며 Body/눈/치아3개의 skinned mesh. 복사본을 Humanoid로 전환하되 자동 매핑이 제어용 리그를 고를 수 있어 실제 pelvis/UE 변형 뼈대에 명시 매핑. 유효한 Human Avatar 확인. 원본 외부 FBX는 수정하지 않음.
- 사용자 추가: 돌진 준비/노림 신호, 봉·목마·비행 중 포획, 파편 투척 사지 피해, 단계적 사지 절단과 몸통 운반, 다른 참가자에게 몸통 투척, 무작위 파괴구 재등장. GameSettings/호스트 판정/기존 공통 절단과 점유 해제 API를 사용하여 구현 중.

- 사용자 최신 지시: 저장/제출을 철회했고 실제 commit/push/PR은 실행하지 않았다. 외형도 적용 보류. 기존 회색 외형·Legacy 동작·8부위 히트박스를 A/test/프리팹에 복원했고 Mascot 객체·파생 에셋은 비활성 보관했다.
- 새 카메라 컴포넌트 없이 기존 CameraSpectator/CameraViewDirector를 수정했다. 포획 동안 추격자의 이동 루트+2.5m를 중심으로1.5배 넓게 관전한다. 손/몸 스킨/포획 플레이어의 애니메이션 상하 이동은 따라가지 않는다. 실제 손 놓기 시 투척 대상 생존자를 우선 관전하고, 그 대상이 없거나 죽으면 기존 생존자 선택을 사용한다. 공용 카메라 수정 범위는 두 기존 파일과 회귀 테스트이며 다른 시점의 벽 충돌은 보존한다.
- 투척은 고정 길이 팔의 뒤로 젖히기→빠른 오버핸드 스윙→후속 동작으로 교체했다. Throw 동안 신장 팔/추가 손을 끄고 실제 손 위치에서 몸통을 놓는다. 낭떠러지 고정 Release 마커까지 팔을 뻗지 않으며 궤적도 낮췄다.
- 최초 실제32초 프레임 기록1920개에서 포획 카메라가 플레이어/손을 추적하고 Throw 진입 시 한 프레임6.10m 이동한 문제를 확인했다. 프레임 시간은 시작 재배치 한 번 외에는60fps 수준이었다. 새 Humanoid 외형은 사용 중지하여 그 동작 혼합은 현재 실행하지 않는다.
- 카메라 새 회귀의 초기 실패2회는 fixture가 이전 충돌 거리 초기화를 생략한 것과 public 전용 테스트 호출기로 internal 메서드를 찾은 것이었다. 실제 대상 변경 경로와 같은 초기화로 고쳐 재검사한다.
- 최종 dev 동기화: Unity 정상 종료 후 origin/dev2211c3c5로21커밋 fast-forward. stash abb00087350bf87c86536642f76c368fc7c256af를 적용해 모든 변경 복원했고 안전 사본은 보존했다. 씬/설정 자동 병합, CameraViewDirector 코드 충돌은 dev의 관전 대상 운반 기물 제외+이번 추격자 캡슐 제외를 함께 유지했다. private8ece127e60b95c2bbef0c773ee4bf62e56b432e2로 갱신, 신규 임포트 보존. staged 변경은 원래처럼 전부 해제했고 commit/push하지 않았다.
- 최종 카메라/투척 호스트 검사: 피포획자 상하30표본 카메라 변위0, 시점거리7.758m, Throw 신장 팔 비활성, 팔길이1.6043~1.6054m. 실제 놓기 시 지정 대상888 관전→사망 시777 대체. 몸통으로777 사지1부위 피해/벽 차단/포획 리타이어도 재통과.
- dev2211c3c5 통합 후 탈출로 EditMode274/274, 카메라 PlayMode1/1 통과(실패·생략0). A 씬 missing script0, 비활성 Mascot/기존8부위 및 dev 엔딩/체크포인트 보존 확인. 테스트 씬은 최신 A 복사본으로 열어 정지·수정 표시 없음 상태로 둠.
- 최종 check-work는 Task100과 기존 Task114가 같은 브랜치를 지정해 현재 Task를 고르지 못하고 중단했다. 다른 Task를 임의 변경하지 않고 verify-scope -Base origin/dev, verify-fast, verify-unity -Compile을 개별 실행: 안전성/문서/컴파일/공용 설정 모두 PASS. 통합 검사의 PASS로 기록하지 않는다. 기존 문서 길이 경고와 private 미커밋 알림 유지. 콘솔 이력의 Vivox403은 음성 서비스 오류이며 현재 compilationFailed=false, consoleErrors=0. commit/push/PR 없음.

## 2026-11-13 저장·제출 재요청
- 사용자가 저장·제출을 다시 요청했다. 외형 적용 보류는 유지하고 관련 변경만 기존 브랜치에서 저장한다. 기존 Graffiti 폰트 변경은 제외·보존한다.
- Unity 정상 종료 후 origin/dev371c0481의7커밋을 fast-forward하고 stash374b15423b5de796e77b005dde0dc2d39c4221af 복원. dev의 공연 도착자 관전 기능은 자동 병합. EscapeRouteSettings 충돌은 새 한글 툴팁과 이번 추격자 조정값·신규 설정을 함께 보존했다. 사용하지 않는 과거 달리기/정지 간격 설정은 되살리지 않았다.
- private의 기존 main을2ca6d78b까지 fast-forward하고 관련 신규3251파일을447a5b13에 커밋했다. 기존 회색 외형이 활성이고 Mascot 파생본은 비활성 보관이다.
- 제출 기준371c0481에서 탈출로 EditMode274/274, 포획 카메라 PlayMode1/1 재통과(실패·생략0). 첫 EditMode 호출은 어셈블리 이름을 testName에 넣어0건이었으며 성공으로 세지 않고 실제 namespace로 재실행했다.
- new-pr -DryRun도 기존 Task100/114 동일 브랜치 때문에 중단. Task 문서를 임의 수정하지 않고 동일 PR 양식과 명시 경로 stage로 직접 저장·제출한다. 통합 검사 중단 내역을 PR에 공개한다.
- 제출 검사: 안전성/문서/컴파일/공용 설정 PASS. 테스트는 별도274+1개 결과를 사용하며0건 호출은 제외했다. Unity 테스트 씬 정지/dirty0 확인.
- 사용자 추가 지시: 필요한 에셋만 업로드. 진행 중이던2.3GB LFS 전송을 중단했고 원격 main에447a5b13이 반영되지 않았음을 확인했다. 로컬 파일을 지우지 않고 미공개 커밋의 추적 목록을 수정했다. 일부 전송된 LFS 객체는 참조되지 않으며 이전 전체 패키지 커밋은 최종 main의 조상이 아니다.
- Unity 의존성 조회 결과 신규 실제 참조는 돌진/투척 WAV2개뿐이다. A/test/프리팹에서 미사용 Mascot·절단 단면27객체를 제거하고 제거 전 파일은 .harness/local/pursuer-assets/pre-minimal-upload에 보관. 음원2개와 폴더/파일meta9파일(502820바이트)만 private a06b1871에 저장했고 최신 private main과 병합한 a31de665를 push했다. 나머지 패키지/실험 에셋은 로컬 미추적 보존이다.
- 제출 대기 중 devb96a22fd 추가9커밋을 Unity 종료 상태에서 병합했다. 탈출 엔딩 낮/밤 설정과 추격자 설정은 자동 병합됐다. 기존 공개 미제출 커밋도 최소 에셋 참조로 수정해 전체 패키지 커밋을 가리키는 gitlink가 제출 이력에 남지 않게 했다.
- 최소 에셋·devb96a22fd 기준 최종 검사: EditMode274/274, 카메라 PlayMode1/1, 컴파일/공용 설정/안전성/문서/원격 private 포인터 검사 PASS. A6545객체에서 missing script0, 기존8부위 활성, 엔딩/체크포인트 보존 확인. 최신 테스트 씬 dirty0/Play 정지로 열어 둠. 로컬 미사용 에셋과 폰트 자동 변경은 제출에서 제외한다.
- 저장·제출 완료: private a31de665(신규 효과음2개·meta9파일), 공개0935817b까지 push 후 https://github.com/gaboja-studio/POC_CURTAINCALL/pull/369 생성(feat/exit-route/pursuer → dev). 지정 팀원 검토 대기이며 병합하지 않았다. 이번 기록도 별도 문서 커밋으로 push한다.
- PR 생성 직후 devcf65ae53에 공연 배경 PR#368이 추가되어 gitlink 충돌이 표시됐다. Unity 종료 후 동기화했고, dev private d7464e91이 현재 a31de665의 조상임을 확인하여 두 작업이 포함된 a31de665로 해결했다. 실제 코드/씬 충돌은 없었다. 새 dev를 병합해 같은 PR을 갱신한다.

- devcf65ae53 통합 후 컴파일·공용 설정 PASS. 기존275개 테스트는 최소 에셋·b96a22fd 기준 결과를 유지한다. 원격 PR 충돌 해소(MERGEABLE)를 확인했다.
- GitHub 문서 검사에서 log132줄로 hard130을 초과했다. compact-docs에 따라 초기 복구·완성 기록을 History002에 원문 그대로 보존하고 현재 로그를 줄였다.
