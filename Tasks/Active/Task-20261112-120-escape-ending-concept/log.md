# Log

- 2026-11-12: doctor 1회 실행. third-party git 메타데이터 접근/초기화, 기존 Task 번호 중복, Unity 6000.3.25f1 설치 확인 실패. CLI 버전·gh 로그인 경고. Unity 실행 프로세스 0개 확인. 로컬 private 에셋 파일과 기존 게임 캡처는 읽을 수 있어 시안은 진행 가능.
- origin/dev fetch 완료. feat/escape-ending-concept를 3af36b10에서 별도 worktree로 시작했다. 기존 브랜치/미저장 에셋은 보존했다. scaffold-task로 Task-120 생성.
- Confluence 41615374 snapshot d1:1747 전체 본문 확인. 결과창 왼쪽/배경 오른쪽, 두 탭의 모든 항목, 밤 갈대밭 정면 달리기, 서커스 원경화, 버튼 복귀를 시안 요구로 반영했다.
- built-in image_gen으로 실제 UI 캡처와 캐릭터 자료를 참조해 플레이어/통계 탭 원화 생성. 원화의 배경은 새 제안이며 Unity 구현 결과가 아니다. 커밋/push 없음.
- 세 시안을 Previews/에 저장하고 review.md 및 generation-prompts.md에 출처/예시 데이터/후속 구현 경계를 기록했다. 이미지에서 필수 항목·캐릭터 3명·밤·서커스 거리 변화·두 탭/버튼을 시각 확인했다.
- 전달 직전 origin/dev를 다시 fetch하고 3aabf021까지 fast-forward 완료. 신규 변경은 Task-120 문서/PNG뿐이다. 게임 코드·씬 검사는 이번 시안 범위에 해당하지 않는다.

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 실패 원인은 기존 Task-20261027-073-exit-route-space-design/log.md 132줄로 hard 130줄 초과. 새 Task-120 문서는 위반 없음. 다른 Task의 이력을 이번 원화 작업에서 편집하지 않았다. Unity 변경 없음/자동 테스트 미선택이며 실행 검증 PASS로 기록하지 않는다.
- PNG 확인: 플레이어/통계 각 1672×941, 배경 보드 1147×1371. 모두 열어 시각 검토했고 필수 텍스트/항목 확인. 다음은 사용자 피드백이며 Sprite/프리팹·실시간 배경은 아직 제작하지 않았다.

## 2026-11-12 시안 승인 후 키트 제작
- 사용자 방향 승인. 기존 UI 재사용·레이어 Sprite/프리팹 키트 제작으로 범위 확장. 배경은 실제 플레이어 이동+Cinemachine 우선, 문제가 있으면 연출용 모델/애니메이션 대안. 현재 배경 구현은 하지 않음.
- doctor -SkipEditor 1회: 기존 중복 Task/private 등록/CLI 설치 인식 실패. 권한 있는 프로세스 조회로 원래 프로젝트의 열린 Unity와 실제 설치를 확인. 원래 에디터/파일은 읽기 전용, 별도 .harness/local/ui120에서 검증.
- 기존 UI20+기물8 PNG를 원본 바이트로 재사용. 신규 원형/테두리/탈출/미탈출 SVG·PNG4, image_gen 캐릭터 투명아틀라스1(4 Sprite). 글자·수치 TMP 분리. 총33 PNG/36 Sprite.
- 실제 Unity6000.3.25f1에서 컴파일·프리팹3개·씬 생성. 초기에 빈 시작 씬 additive 제약과 TMP subasset 저장 순서를 수정. 시각 검사에서 탭 밑줄 자산을 바탕으로 쓴 문제와 구분선 높이를 보정. 실제 기물 아이콘 연결 후 최종 캡처 확인.
- 표시 참조/텍스트 검사 오류0, Play Mode 합성 pointer click·레이캐스트 기반19개 검사 통과. 엔진 SearchDatabase 초기 인덱싱 ArgumentOutOfRangeException은 별도 기록하며 전체 Console 오류0을 주장하지 않는다. NGO/실게임 연결·사람 입력은 미검증.
- 재사용 자산은 독립 설치를 위해 원본 그림을 복사하되 새 GUID를 사용한다. 원본 PNG/meta/GUID/런타임은 변경 없음. 키트 payload는 Unity~로 자동 컴파일을 방지하고 설치된 Assets는 별도 네임스페이스를 사용한다.
- 최종 UnityPackage 59 assets(UPM 자체 파일 제외), 재사용28 PNG SHA동일,33 PNG 알파·border·meta 확인,62개 고유 GUID/중복0, 초상 알파0~255,ZIP CRC 정상. 전달 직전 fetch/fast-forward 확인(origin/dev와 동일). 커밋/push 없음.

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 키트 묶음 check-work에서 Task 내부 C# 파일명의 하네스 규칙 위반을 발견해 Source/*.cs.txt 소문자 템플릿으로 보정했다. Unity 출력은 원래 클래스명 .cs를 유지하며 구조 재검사 Structure: OK. 남은 전체 문서 실패는 기존 Task-073 log.md 132줄(hard130)이며 새 키트 문서의 위반은 없다. 위 검사 컴파일/테스트 생략과 별도로 독립 Unity 컴파일/렌더/PlayMode19항목 검증 완료.

## 2026-11-12 실게임 엔딩·로비 연결
- 사용자 실게임 연결 승인. 작업용 private worktree/독립 Library를 구성하고 키트 payload를 실제 Assets에 설치했다. 시작 전 task editor 종료·origin/dev fetch/merge 완료(3aabf021). 원래 프로젝트 Unity/사용자 변경은 보존.
- doctor1회는 private 초기화/중복 Task/CLI 설치 인식에서 실패. 실제 설치/CLI/연결을 확인해 진행했다. 최초 전체 import 후 새 C# 소스 누락은 native AssetDatabase.MoveAsset으로 GUID를 보존해 등록했다.
- 실제 결과 Canvas/명부·기물 동결/모델 초상/세대 누적 통계를 연결했다. 10개 History와 별도 누적, 기존 RewardState/ShopChannel 공유, 미시도 —, 반복 결과 메시지는 현재 탭을 유지한다. Canvas 루트와 자식 View를 구분하고 결과600/전환 커튼2000 순서를 적용.
- 원본 소유자 PlayerMover·NGO·부상 속도·애니메이션을 유지한 실제 달리기, Cinemachine 정면 추적, 밤 갈대밭120m×3 순환, 기존 메시 재사용 서커스 원경을 구현. 기존 벽/위험물과 겹치지 않게 같은 씬 local z500에 연출 공간 배치. 복제 모델/영상 없음.
- 로비 진입 때 위치만 바꾸면 Arrived/Retired가 남는 원인 수정. 공간 해제/로비 환경 활성화 뒤 호스트 ServerRestoreEveryone, 입력 잠금 해제. Ending/Ended는 유지하고 레버로 다음 준비를 시작한다.
- 일반 에디터 실제 LAN 호스트 Start→Lobby→Escape→성공 경계 주입→결과 버튼→Lobby 검사. W0.5초1.5503m 이동, 레버 근처 배치 후 실제 F 입력으로 None/Waiting 복구. 상세 JSON/화면은 Verification/·Previews/와 integration-review.md.
- 컴파일 PASS, 씬 missing script0, 신규 통계3/3·기존 엔딩4/4 PASS. 전체 보상65개 최초 검사 신규 fixture 오류3개 수정 후3/3 재검사. 기존 AudienceReactionTests 기대50/실제150 불일치1개는 보상 규칙을 바꾸지 않고 기록. 전체65 PASS 주장 없음.
- 배치 모드에서 C16 Close가 렌더 보장 때문에 전환을 거절한 것을 확인해 일반 에디터로 다시 검증. 1인 호스트 검증이며 2~4 실제 클라이언트/부상·리타이어 혼합/전 구간 수동 완주는 미검증. 임시 키보드/Dev Mode 원복.

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work: 안전성/구조/지식 PASS, 기존 Task-073 log132줄>130으로 문서 FAIL. 자동 Unity 단계는 선행 실패로 생략됐고 별도 컴파일·7개 테스트·실게임 회귀 근거를 Verification에 저장했다. 새 Task 문서 길이 위반 없음.
- 작업용 에디터 정상 종료 확인 후 origin/dev fetch/merge 완료: 3aabf021 already up to date. diff --check 통과. 원래 프로젝트 에디터는 유지. public/private 커밋·push 없음.

## 2026-11-12 빠른 확인용 치트
- 시작 전 task editor 종료 상태에서 origin/dev fetch/fast-forward: 836dd382. 기존 엔딩 작업과 원래 프로젝트 변경을 보존했다. doctor1회 기존 private 초기화/중복 Task/설치 인식 실패, 실제 CLI/에디터로 검증 가능.
- F9 후반6지점과 실제 출구 개방 후 갈대밭 지점, 외줄/대포 난이도6개 바로가기, F8 난이도 선택을 추가했다. 난이도는 현재 테스트 공연에만 적용하며 해금/클리어/GameSettings는 그대로다.
- 검증 중 F8 직접 탈출 시작은 IsCampaign=false라 도착 판정 자체가 빠지는 문제 발견. 권한 있는 Escape 런에서 참여자를 직접 수집해 같은 도착 판정을 적용하고 전원 실패 시 기존 재시도를 유지했다.
- 처음 문 주입 검사는 다른 장치를 가리킨 EscapeExitSaw.Current 때문에 시간 초과. 실제 content.exitSaw로 fixture를 보정했고 제품의 갈대밭 표시도 content.ExitOpen으로 통일했다. 이후 도착 Arrived/Ending 확인.
- DevStartPointTests28/28, 실제 호스트6난이도/6이동지점/리타이어 재시도/갈대밭 엔딩 PASS. 최종 컴파일 오류0. Dev 설정 원복·Play 종료. 수동 완주/다중 클라이언트는 미검증. 상세 cheats-review와 Verification.

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최신 dev에서 기존 문서 길이 초과가 해소되어 이번 check-work 전체 필수 단계 PASS. 선택 자동 테스트는 생략했으며 별도로 실행한 시작 지점28개 검사는 PASS. 작업 에디터 정상 종료·프로세스 부재 확인 후 fetch/merge: 836dd382 already up to date, diff --check PASS. 커밋/push 없음.

## 2026-11-12 마지막 문 열기 치트
- 요청에 따라 F9 호스트용 마지막 문 열기 추가. 실제 content.exitSaw의 필수 배선 절단을 완료하고 ShareCut/Changed로 기존 장치 공유·네트워크 발행·문 회전을 사용한다. 씬/프리팹 변경 없음. 기존 작업 변경 보존.
- 시작 전 editor 부재 확인 후 origin/dev fetch/merge 836dd382 동일. doctor1회 private 초기화/중복 Task/Unity 설치 인식 FAIL이나 실제 작업 프로젝트 에디터는 정상 동작. CLI beta17 최신 확인.
- 실제 LAN 1인 호스트에서 F9 공개 호출로 배선24/24, 문 회전57.94도(관측 순간), 갈대밭 Arrived→Ending→결과창 표시 확인. Dev-off 거부·중복 호출 revision 유지·재시작 후 닫힘/재개방 PASS. 검증 스크립트의 이전 상태 주입을 제품 치트 호출로 교체했다.
- 컴파일 오류0, Play 마지막 Console groundTruth 오류0. Dev 설정 원복/Play 종료. 다중 클라이언트 실검증은 포함하지 않으며 기존 스냅샷 발행 경로를 재사용한다. 근거 Verification/door-cheat-play.json.
- check-work 안전성/문서/컴파일 PASS, 선택 자동 테스트 생략(위 실제 Play 검증 별도). 검증 에디터 pid55148 정상 종료 후 시작 Error 창에서 대기한 자체 중복 실행 pid53872를 확인·정리했다. 작업 에디터 프로세스 부재 확인 후 origin/dev fetch/merge 동일836dd382, diff --check PASS. 커밋/push 없음.

## 2026-11-12 체크포인트 표시·클리어 이력
- 열린 작업 에디터는 Start 씬/변경 없음 확인 후 정상 종료·origin/dev 동기화(836dd382 동일)·재실행. doctor 1회 기존 private 초기화/중복 Task/에디터 인식 실패; 실제 CLI 검증 가능.
- 수정 전 F8 직접 탈출로에서 6개 후반 위치가 등록되는 것은 확인했다. 사용자 환경에서 빠진 정확한 조건은 재현하지 못했다. F9 상단 전용 목록과 열 때 스크롤 초기화, 씬 활성 분기 안의 숨긴 표식 포함, 진행 순서 고정을 적용했다.
- F8 옵션으로 이동 전에 실제로 달성한 묘기별 최고 단계를 읽고 새 테스트 런 초기화 직후 복원. 해금 한계가 같이 유지되며 선택 해제/일반 새 런 초기화 유지. 보상/결과 내역 재생성 없음.
- 실제 호스트 테스트: 클리어 API로 외줄2/대포1 기록을 준비한 뒤 공간 이동, 두 단계 및 선택 한계3/2 보존·후반6지점 이동·갈대밭 엔딩 결과창 표시 확인. 엔딩 상태에서 즉시 새 공연 이동은 기존 전환 가드로 거절됐으며, 결과 버튼으로 로비 복귀 후 체크 해제 이동에서 이력 초기화 확인. 스크린샷2/JSON3 보존, Dev 원복·Play 종료.

- check-work 안전성/문서/컴파일 PASS, 선택 자동 테스트 생략. 작업용 에디터 정상 종료·프로세스 부재 확인 후 origin/dev fetch/merge 동일, diff --check PASS. 커밋/push 없음.

## 2026-11-12 결과창 피드백·부상자 멀티 캡처
- 빈 원 숨김·원형 클립 안쪽 조정·토큰 행 중앙 정렬을 기존 프리팹과 빌더에 반영. 실제 프리팹에 기록 없음 입력 시 바탕·테두리 숨김과 — 중앙 확인.
- 시작 전 Start 씬 미저장 없음 확인 후 에디터 종료·origin/dev 동기화(동일), 재시작. doctor 기존 private 초기화/중복 Task/설치 인식 실패, 실제 Unity 연결과 검증 가능.
- 임시 2인 시나리오로 보조 에디터를 준비했고 자동 동시 Play 전환이 되지 않아 각각 Play로 진입한 뒤 LAN으로 연결했다. 테스트 최초 부상 입력은 비순차 LeftArm 표현 예외로 실패해 복원 후 공용 순서 LeftLeg 적용. 실제 외줄 정산과 같은 런 탈출, 양쪽 탈출 2명·토큰200·호응0 일치 확인.
- 최초 갈대밭에서 대형이 벌어져 선두가 사라지는 문제를 찾음. 선두 입력 보정 후 2인 전체 흐름 재검증 및 둘 다 프레임 안인 1920×1080 3장 저장. 출구 치트 클라이언트 표시·수동 완주는 미검증/별도. 상세 `ending-feedback-review.md`.
- Dev 설정 원복·Play 종료·임시 시나리오 삭제. 독립 캡처만 검증; 실게임 화면 변경은 프리팹/코드로 보존.

- 최종 check-work 안전성/문서/컴파일 PASS, 선택 자동 테스트 생략. 호스트와 보조 에디터 모두 종료 확인 후 origin/dev fetch/merge 최신, diff --check PASS. 커밋/push 없음.

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-13 최신 dev 통합·제출
- 다른 작업 공간을 유지하고 escape-ending-submission에서 origin/dev 9a5baaa7의 추격자·타이머와 기존 엔딩을 통합. F9 순서는 각 씬의 체크포인트 편집기에서 저장하며, 외줄 6 / 인간 대포 11 / 탈출로 15개를 배치했다. 종료 트리거와 갈대밭은 이동 전용.
- 실제 LAN 호스트에서 마지막 문 앞 부활 → 문 열기 → 갈대밭 → Cinemachine 및 결과창 확인. 2인 LAN에서 초기 로딩이 검증 대기 한도를 넘겼으나 로딩 완료 뒤 호스트·클라이언트 둘 다 은퇴/부상 상태에서 정상/부상 없음으로 복구되고 서로 겹치지 않게 마지막 문 앞에 도착. 근거 Verification/checkpoint-{seed,play,team}.json.
- 비공개 결과창 에셋 PR 20을 private main에 반영했으며 게임 저장소가 가리키는 16982ba2가 private main의 조상임을 확인. Unity 자동 생성 변경은 격리 작업 공간의 별도 stash로 보존했다.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 최신 dev의 기존 Task-115 log.md 길이 제한으로 전체 문서 검사 FAIL. 해당 Task 변경 없이 직접 Unity 검사를 별도로 실행해 컴파일 및 공통 설정 PASS 확인. 선택 자동 테스트 생략.
