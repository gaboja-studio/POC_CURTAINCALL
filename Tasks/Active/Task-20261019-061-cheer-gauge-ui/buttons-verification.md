# 공용 버튼 검증 (2026-10-19)

## 작업 범위
- PM 지시로 기존 Task를 UI 리소스 확보·조합·테스트로 확장. 로컬 브랜치 feat/ui-resources, integration/ui-resources로 개명. Task/통합 폴더 ID와 worktree 경로는 유지, 원격 생성·커밋·push 없음.
- Resources/Prefabs/UIs/CommonButtons에 Confirm/Cancel/Settings/General4개, Resources/Fonts/CommonButtons에 SemiBold TMP, Scenes/Tests/CommonButtons에 연결된 프리팹 인스턴스4개 씬 생성.
- private v1 런타임·조립기 재사용. 다른 unity_only_v1 복사본과 중복 타입 컴파일 오류를 확인하여 v1만 CurtainCall.UI.ButtonsV1로 namespace 분리. 다른 키트는 수정·삭제하지 않음.

## 검증 결과
- 컴파일: recompile_status completed, failed=false, errors=[], compilationFailed=false.
- PlayMode CurtainCall.CommonButtons 2/2 통과(1.65초): 4종 geometry/4상태/Focus/클릭·Submit 각1회/Disabled 무반응/재활성. 두 번째는 합성 장치 이벤트→InputSystemUIInputModule→각 프리팹의 마우스·Enter 경로 검사. 사람 입력과 구분.
- 버튼 구현 후 C1 회귀 재검사: EditMode18/18(0.51초), PlayMode3/3(1.06초) 통과.
- 최초 테스트 실행은 신규 테스트 파일 import 전 0건이었다. PASS 처리하지 않고 recompile 후 발견·재실행해 위 2건 통과를 확인.
- 저장 자산 조회: 4개 모두192×56, 정상 sprite/alpha0.92/SemiBold 글리프, Missing Script0, 확인 씬 인스턴스의 원본 prefab 연결 정상.
- 실제 확인 씬 Play: EventSystem1, InputSystemUIInputModule point/click 액션 활성, 커서 None. Full HD Overlay 캡처로 원본의 외형·아이콘·색 확인.
- 확인 씬 EventSystem 합성 Move로 Confirm→Cancel→Settings→General 및 왼쪽 Settings 복귀 확인. 사람 키 입력 검사는 아님. 회귀 후 씬 clean/Play 종료 확인. 상태 조회 1회 메인 스레드 timeout은 재조회 정상으로 복구.
- 임시 런타임 너비128/192/320·긴 한글 Ellipsis와 9-slice 표시 확인. 확대 버튼이 이웃에 접근하는 것은 확인용 고정 배치 변경이며 저장하지 않음.
- 16:10 작은 GameView에서 확인 패널이 중앙 버튼과 가까워지는 문제를 발견, 화면 크기에 맞춰 패널을 축소하도록 보완. 프리팹 크기는 변경하지 않음.
- 개별 구역75파일 PASS, fast PASS(기존 WARN7), knowledge9폴더 PASS, git diff --check PASS.
- 표준 check-work FAIL: origin/integration/ui-resources 부재로 구역 검사 중단, 후속 문서/컴파일/테스트 SKIPPED. 위 개별 검사를 표준 PASS로 보고하지 않음.

## 보존·제한
- C1 prefab hash4f91ce4ffe456ccdbf1a08051f7cf5911c7ee456, scene8fd14d25190c8ce9a27fd4aadc10b0406b3b1ebf 불변.
- private 기존 추적 자산 diff220파일/4886삽입/931삭제 불변. 기존 자동 변환 차이 폐기·커밋 없음.
- private 키트/수정/.meta 미커밋이므로 다른 PC 재현 미보장. PNG20·폰트·라이선스·제공 코드 보존. 2026-10-20 PM 동작 확인 후 정리 승인, 아래 결과 참조.
- 실제 게임 메뉴 연결·사람 공동 테스트·네트워크·전체 프로젝트 테스트는 미실행. 기존 C1 검증은 verification.md 참조.

## 2026-10-20 정리·저장
- PM이 정상 동작 확인 후 저장·미사용 제작 자료 삭제·하네스 기록을 승인. 실행/직렬화/동적 경로 조사 후 Unity DeleteAsset 사용, 원본34개와 파일 meta34개·빈 폴더3개(meta 포함) 정리.
- 키트 내부127파일/22,885,437 bytes →56파일/2,825,546 bytes. 감소20,059,891 bytes(19.13 MiB/87.65%). 루트 meta 보존·집계 제외. Git 이력/Library/빌드 절감량은 아님.
- PNG20·Runtime/Editor·SemiBold TTF/OFL·상위 meta56파일 SHA256 불변. 삭제 자료는 scratchpad/buttons-cleanup-backup에 임시 보관. 무관 private220파일 diff와 C1 정본 git hash 불변.
- 정리 전 버튼2/2(1.66초), C1 EditMode18/18(0.50초). 정리 후 prefab4개 참조/연결 정상·Missing Script0, recompile up_to_date, 버튼2/2(1.43초)·C1 PlayMode3/3(1.05초)·EditMode18/18(0.50초) 통과.
- 확인 씬을 디스크에서 재로드 후 Play, 실제1920×1080 Overlay 캡처로 4종·아이콘·글리프·하단 패널 정상 확인. 재생 종료, C1/버튼 정본7파일 SHA1이 정리 전과 동일.
- 결합 필터 CommonButtons|CheerGauge는0건 발견: PASS 처리하지 않고 각각 분리 재실행. 합성 입력 검사를 사람 검사로 보고하지 않음.
- 정리 당시 개별 구역76파일 PASS(로컬 integration 기준), fast PASS/WARN7, knowledge PASS, diff --check PASS. 최초 save-work DryRun은 origin/integration/ui-resources 부재로 실패.
- 이후 PM 원격 준비·저장 승인. 최신 origin/dev 5e9b1b04에서 원격 integration/ui-resources 초기화, private 필수86파일은 main c15d9a11에 push 완료. 공개 저장 DryRun 구역·fast·컴파일 PASS(테스트 SKIPPED). 기존 실패와 구분하며 이 문서 포함 feat 저장, PR·병합 제외.
