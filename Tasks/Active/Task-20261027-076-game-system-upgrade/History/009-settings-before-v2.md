# v2 교체 전 설정 구현·제출 기록

v2 교체 기록 공간을 위해 log의 완료된 두 절을 원문 보존한다. 아래의 당시 다음 작업·디자인 보류는 이후 v2 교체 요청으로 우선순위가 바뀌었다.

## 2026-10-28 런 포기·그래픽 설정 구현
- 계획 준비 후 사용자가 호스트 전용 런 포기를 확정하고 구현 요청. doctor fail0/warn1(skill-links), Unity6000.3.25f1/worktree 일치·미저장 씬0 확인. 기존 Task-076/feat/game-system-m2에서 구현, commit/push/외부 게시 없음.
- 정식 `Abandoned` 사유를 enum 끝에 추가, `RunState`/`CampaignRun.AbandonRun`이 권한·대기실 쉬는 시간/밤·전환 상태·확인 세대/시간대를 재검증해 기존 실패 경로로 다음 세대1일차 첫 공연 시작. 방 유지·부상 복구·자금 리셋 확인. 소지/배송/복구 비용의 기존 세대 감시 경로 유지, 실제2인 전체 데이터 검증은 미완료.
- `ClientSettings`에3모드·유효 지원 해상도·FPS30/60기본/90/120/144/165/240/-1 저장/이관/적용. `FrameRateController` 매 프레임 공통값 강제를 제거하고 로컬 설정 단일 적용. 공통 FPS 개발값은 이제 자동 반영되지 않음.
- C14의3탭×3행/Slider·Toggle 계약을 보존: 게임 탭에 그래픽 보조 분류/런 포기 버튼, `SettingsExtensionPanel`에서3행 이전/다음 선택·적용/취소·포기 예/아니요. 마이크/팀원 음성 유지. 씬/프리팹/private 원본 수정 없음, C# 신규 meta는 Unity가 생성.
- 검사: 컴파일PASS·EditMode RunAbandon6/6·PlayMode FrameRateSettings21/21·SettingsUpgrade2/2·CommonMenu3/3(포인터 클릭 포함). 최초 SettingsUpgrade1/2는 씬 전환 종료 직후 InputGate 해제를 같은 프레임에 요구해 실패, 최대3초 상태 대기로 수정 후2/2. 첫 캡처의 누락 화살표 글리프는 ASCII로 교체·최종 캡처 확인. 전체 회귀는 미실행, 기존 M2 회귀 실패를 해결한 것으로 보지 않음.
- 테스트 후 공용 CommonButtons 동적 글꼴에 ellipsis/underscore 캐시 변경 발견·보존(임의 폐기 없음). Vivox Settings.json은 git 상태에 잡혔지만 실제 내용 diff/numstat 없음(줄바꿈 안내만), 비밀값 출력·수정 없음. PC 빌드 실제 화면 전환/재실행·실제2인·게임패드 확인은 manual-verification에 미확인으로 추가.

## 2026-10-28 설정 5탭 재구성
- 게임/그래픽/조작/사운드/기타5탭·인게임 게임 탭 활성·스크롤/draft/취소/적용 구현. C14 원본·씬/private 불변. Common10키 재지정·중복/ESC/M 거절·기본값·GUID 저장·Reader 반영. 전체/BGM/SFX·음성 전체/입력/개별 팀원·마이크on/off·Vivox 기기 조회/선택 연결, 기존 종/금속음 SFX 직접 등록. BGM 재생 코드 없음·게임 출력은 OS 관리. 기타 개발자/출처·Pretendard OFL 원문, BM 등 미확인은 확인 중 표시.
- 검사: 컴파일PASS, 입력EditMode17/17·음량7/7·설정PlayMode3/3·메뉴/실제 포인터3/3. 초기 테스트 helper 미작성 컴파일실패는 helper 추가 후 해결, 복합필터 `|`는 NO_PROJECT_TESTS여서 각각 실행. 캡처 확인 후 슬라이더 손잡이 과대 높이·조작 축 레이블 보정/스크롤바 추가, 설정3/3 재통과. 전체 회귀 실행 아님.
- 최종 check-work(-NoRecord): 안전성·문서/규칙·컴파일PASS(hard0·경고15), Knowledge OK(11폴더)·diff-check 오류0(줄바꿈 경고만). 전체 테스트SKIPPED, 관련30건은 개별 통과. 이전 피날레3/대포간헐/Telemetry 실패는 유지. 실제2인/장치·PC빌드/재실행·게임패드·전체 라이선스 검토 미완료는 manual-verification에 기록.
- 사용자 후속: 디자인은 아직 만족하지 않으며 추후 지시로 개선. 현재까지 기능·관련 폰트 캐시를20d80e22로 저장·push 완료(Vivox 설정 실내용 diff 없음). 기존 검사 재사용·추가 자동 테스트 없음, 실제 검증/라이선스 미완료와 기존 회귀 실패 유지.
- 2026-10-28 사용자 제출 요청: 기존 PR227 MERGED 확인 후 https://github.com/gaboja-studio/POC_CURTAINCALL/pull/229 (feat/game-system-m2 → dev) 생성. OPEN·MERGEABLE, 기존 지정 pm 팀 검토 요청 완료. 병합 미실행. 다음은 기존 피날레/대포/Telemetry 회귀 원인 수정부터, 이후 기물7종 후속. 디자인은 사용자 추후 지시까지 보류.
