# Log

- 시작: 에디터 종료 확인 후 origin/dev fetch 및 26c4cdb9 fast-forward, 새 브랜치 생성. private gitlink 119cb7d2로 갱신하며 기존 dirty 변경 보존.
- doctor: sandbox 접근으로 private/Unity/gh를 오판. 권한 있는 읽기로 Unity 6000.3.25f1 설치·MoHoDu 인증·private 저장소 정상 확인. 기존 Task ID 중복은 이번 작업과 무관하며 신규 번호158 사용.
- 구현: cheerPointsEnabled 기본 false, 호스트 스냅샷에 enabled 포함. OFF에서는 수집 판정·VFX 생성/표시를 중단. 전환 시 이동 이전 위치를 지우고 획득 기록은 유지해 소급/중복 보상 방지.
- Unity API로 PerformanceEquipment.asset의 cheerPointsEnabled=0 저장, 3종 프리팹 참조 보존. 초기 컴파일/공통 설정 PASS. 첫 check-work 실행 중 문서 갱신이 겹쳐 fingerprint 검사는 중단되어 최종 문서 상태로 재검사한다.
- PlayMode CheerPointToggle 2/2 PASS(29.13초): 실제 외줄·인간대포 호스트에서 OFF VFX 미생성/무보상, ON 복원/신규 획득, 실행 중 OFF 기존 외형 숨김, 과거 경로 소급 보상 없음, 재활성화 후 중복 획득 없음. 테스트는 변경한 설정을 finally에서 복원한다. 별도 원격 프로세스 플레이는 이번 범위에서 재실행하지 않았다.
- Confluence 44793873 d1:3916: 캐주얼한 인상 피드백, 기본 OFF, 설정 경로/호스트 기준, 기존 영상·사진은 ON 참고자료라고 명시. 기존 4종 기물·씬 배치·3종 외형은 유지.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 후속 요청 반영 (초기 OFF 방향 대체)
- 기본 ON으로 수정, 긴 수직 줄·하강 후 가속 상승·최소 높이와 카메라 밖 조건을 모두 만족할 때 비활성화. 정지된 파티클의 원점 빈 bounds를 제외해 숨김을 막는 문제 수정.
- 사용자 추가 확인: 배치 대상은 환호뿐 아니라 공연 장애물·대포·착지점까지. 기존 IMGUI 배치 창을 확장해 6개 공연/난이도 조합, 3D 지도, 편집 핸들, 설정 저장, 예상 경로와 실제 플레이 진입 구현.
- 인간 대포: PerformanceLayout은 실제 씬 참조/순서를 유지하며 난이도별 로컬 pose·active 저장. 저장 전/창 종료 시 기본 pose 복원. 신규 장치는 기존 대포/장애물/봉/운반/불 목록에 append 등록.
- 외줄: GameSettings.Tightrope의 난이도 독립 배치 복사본. 기존 추가 톱날의 밀도 예외를 직렬화해 보존. 삭제는 excluded로 번호/작동 순서 보존.
- EditMode PerformancePlacement 7/7 PASS: 3종 고리의 화면 내 숨김 방지·하강/상승·복구, 외줄 난이도 독립성과 직렬화, 양 공연×3 난이도 고리 추가/복제/삭제, 대포 실제 목록 등록·난이도별 pose·씬 저장/재로드. 초기 검사 실패는 비표시 테스트 창 Close 호출과 빈 파티클 bounds였으며 수정 후 통과.
- 배치 창 실제 UI 확인: 목록/지도/세부설정 표시, 겹치는 이름 라벨을 선택/호버로 제한. 인간 대포 난이도2 플레이 바로가기 LastLaunchSucceeded=true 및 캠페인 Difficulty=2 확인.

- 최종 EditMode PerformancePlacement 7/7 PASS(비활성 운반 발판/불 판정 포함). PlayMode CheerPointToggle 2/2 PASS: 양 공연 OFF/ON, 난이도 제외, 소급/중복 방지. 첫 Play 실패는 외부 명령 호환 로그였고, 이후 외줄 일반 회피 환호가 고리 보상 검사에 섞인 것을 분리했다. 설정은 TearDown에서도 복원한다.
- Confluence 44793873 d1:4060: 기본 ON, 수직 줄/하강-상승 회수, 난이도별 장치 배치와 참고 경로 설명 갱신. 새 영상 hanging-hoop-retract.mp4와 사진 hanging-hoop-stage.jpg 첨부/교체. 영상은 실제 인간 대포 배경의 연출 샘플이며 통과 보상 촬영이라고 주장하지 않는다.
- 원격 클라이언트 별도 프로세스와 팀 공동 플레이는 이번 턴에서 미실행. 기존 네트워크 장치 인덱스/캠페인 난이도/호스트 고리 스냅샷 경로를 유지했다.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work PASS: 안전성·문서/규칙·컴파일·공통 설정. 자동 테스트는 별도 EditMode 7/7, PlayMode 2/2 결과 사용. 기존 private dirty 29건·문서 길이 경고는 이번 작업에서 수정하지 않았다. 제출 전 저장되지 않은 씬 0 확인 후 Unity PID6420 정상 종료.
- 제출 직전 editor 종료 후 origin/dev fetch/fast-forward: 14ca2f3b(PR421 공연 진입 판정 보완). 충돌 없음. 공연 진입 경로가 바뀌었으므로 통합 상태 컴파일/PlayMode를 재확인한다.

## 보유 에셋 활용·링 크기 후속 요청
- Generic Rope_01/Rope_Knot_01, HorrorCarnival Hoola/Fire/Sparks 확인. 원본을 보존하고 private에 새 프리팹·로프 메시·삼줄 재질 추가. 로프는 반복 결합하고 매듭으로 고리에 연결, 불꽃 크기·수명·난류를 조절했다.
- 링 배율 UI와 실제 지름 표시, 크기 복제/분리 보존, 내부/외부 통과 경계 검사 추가. 최신 EditMode 7/7 PASS.
- dev 통합 직후 PlayMode 외줄 1건에서 테스트가 고리 외 관객 환호 +10을 관찰했다. 테스트의 관객 제외 대상을 첫 오브젝트에서 모든 인스턴스로 수정하고 재검증 중이다.
- 최종 EditMode 7/7 PASS(3.27초): 원본 기반 로프/고리 메시의 빌드 읽기 가능 여부 포함. 매듭의 원본 충돌체는 인스턴스에서 즉시 비활성 후 제거. PlayMode 2/2 PASS(27.77초) 후 마지막 재질·표면 발화 및 편집 창 씬 전환 갱신까지 재확인한다.
- 촬영/화면: 실제 인간 대포 배경에서 1280×720/30fps 4초 회수 연출 저장. 고리 표면에서 발화하도록 보완했고, 배치 창의 링 크기 배율/실제 통과 지름 표시를 확인. 씬 교체 직후 오래된 고리 목록을 그리던 MissingReference를 GUI 직전 갱신으로 수정.
- Confluence d1:4124: 보유 로프/매듭/고리 표면 불꽃과 크기 조절 설명 반영, 새 사진·영상 첨부 교체. 영상은 실제 획득 플레이가 아닌 공연장 배경 연출 미리보기라고 명시.
- 최종 외형/씬 전환 보완 후 PlayMode 2/2 PASS(29.31초). 제출 전 Unity unsaved=[] 정상 종료 후 dev 0b8f5e8f까지 fast-forward. 추가 대기실 수정과 충돌 없음.
- 동시 작업의 Task158이 dev에 먼저 들어와 이번 Task를159로 변경했다. branch는 유지한다.
- private main 4fb9d963 통합 후 파생 에셋 8개만 22e55ec1 커밋 및 resource/cheer-hanging-hoop push. 기존 private 29개 변경은 보존했다.
- 에셋 PR: https://github.com/gaboja-studio/CURTAINCALL_PaidAssets/pull/25 (main 대상, PM 검토 요청, 미병합). 메인 프로젝트 gitlink는 push된 22e55ec1을 참조한다.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최신 dev 0b8f5e8f 통합 상태 check-work: 안전성·문서/규칙·컴파일·공통 설정 PASS. 기존 private dirty29/문서 길이 경고는 보존. PR25 검토자 RagSpirin(PM) 요청 확인, 에셋 목록 CI SUCCESS.
- dev 통합 재검사에서 반복된 +10의 원인은 관객 반응뿐 아니라 톱날 접촉의 고정 절단 보상이었다(PerformanceRewardModel.Observe). 고리 보상 테스트 동안 실제 톱날 관찰만 함께 중지/복원하도록 분리했다. 제품의 장애물·절단 보상 규칙은 변경하지 않았다.
- 최종 통합 상태: 톱날 절단 보상 분리 후 컴파일·공통 설정 PASS, PlayMode CheerPointToggle 2/2 PASS. new-pr DryRun 대상 dev/Task159 확인. 기존 무관한 로컬 변경을 보존하기 위해 명시 목록만 커밋하고 gh로 PR을 등록한다.
- 메인 5b55768b 커밋 후 pre-push의 private-main 포함 조건으로 최초 push 중단. 사용자에게 PR25 병합을 구체적으로 확인받고 merge commit cfb0ea05로 병합했다. gitlink22e55ec1이 private main의 조상임을 확인한 뒤 push 통과.
- 제출: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/422 (dev 대상), 지정 검토자 RagSpirin 요청. 에셋 https://github.com/gaboja-studio/CURTAINCALL_PaidAssets/pull/25 는 사용자 승인 후 main 병합 완료. dev PR은 검토 대기이며 병합하지 않는다.
