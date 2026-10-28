# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 완료된 런 포기·그래픽 구현 및 설정5탭 저장·PR229 제출 원문: [History/009-settings-before-v2.md](History/009-settings-before-v2.md).


- 환경 준비·M2 문서 반영 초기 기록 원문: [History/007-environment-and-m2-document-start.md](History/007-environment-and-m2-document-start.md).

## 2026-10-27 보상 시스템 기획
- Confluence 보상 시스템 초안(27459631) v3 게시 후 본문 재조회 확인. 토큰·공동 자금, 호응도와 생존·사망 정산, 난이도별 완주 보너스, 환호·야유·관심 감소, 공용 환호 포인트, 신체 복구 100·이용마다 +25를 표와 흐름표로 정리했다. 공연 실패는 전원 사망으로 명시했다.
- 계산 정정: 첫날 구성과 밤 난이도 선택을 유지하면 최고 단계 진행은 1·1·2·2·3·3이며, 1인 최대 2,700토큰이다. 확정한 3,000토큰 설계 목표와 구분해 조정 미정으로 남겼다.
- 다음: 기물별 가격, 포인트 배치 구현, 묘기별 성공·회피 판정, 최대 수입 목표 차이 결정. 관련 기획서·하네스의 이전 보상 규칙 동기화와 코드 구현은 이번 보상 문서 작성에 포함하지 않았다.

- 완료된 보상 동기화 초기 기록 원문: [History/011-completed-records.md](History/011-completed-records.md).

## 2026-10-28 동기화 재개·슬롯 기준 정리
- 사용자가 수동 승인 모드 전환을 알리고 동기화 마무리를 요청했다. M2 27721807 v3 정상 게시 후 HTML 전체 재조회로 복구100/+25·보상 정산·기물7종 가격·슬롯2/1/+2 반영 및 기존 표 열너비·mention·목차 보존 확인. 기존 미디어 없음.
- 재개 당시에는 기존2/1/+2를 동기화했으나, 이후 사용자 새 결정으로 대체: **배낭과 팔1개 슬롯 감소 삭제, 팔 개수 무관 기본2칸 유지**. 하네스 부상·게임흐름·공통세팅·도메인 지도·Task에 반영했으며, 관련 Confluence 후속 반영 중이다. 팔 절단·배낭 해제로 인한 초과 기물 처리 미정은 대상에서 제외했다. 코드는 아직 변경하지 않았다.
- 보상27459631 v4 게시·HTML/ADF 재조회 완료 보고: 새 제품7종 가격·가격만 확정/효과·내구도 초안 구분·관련 링크·미정 문구 정정, 기존 표·열폭·mention 보존 확인. 전송에 포함했던 일반 블록 local-id가 게시 후 제거되어 전체 ID 보존은 충족하지 못했다(mention local-id 유지). 게임 흐름·장소·기물 후속 게시 및 재조회는 진행 중이다.
- 검사: check-work(-SkipUnity -NoRecord) 안전성·문서/규칙 PASS, hard0·길이 경고13으로 종료코드2. git diff --check 종료0(공백 오류 없음, LF→CRLF 안내만 있음). 컴파일·자동 테스트 SKIPPED(문서 작업), commit/push 없음.

## 2026-10-28 기물 내구도 확정
- 사용자 '추천한 규칙으로 확정하고 문서에 반영해줘': 새제품 구조5/끈끈이3/벌룬바지4/신발3/머리핀5/뽑개4/UV3회, 실제 효과 성공만1 차감·중복 소모 방지·마지막 효과 완료 후 파괴/슬롯 제거·0회 새 발동 금지 확정.
- `item-durability.md` 신설 및 index·보상·상점·미정·게임흐름·개요·도메인 지도·plan에 반영. UV 동시 단서 차감 단위·끈끈이 장갑 구조 중 타이머·신발 바닥/피해 판정 순서는 미정 유지. 효과 수치·접두사·쿨타임·발견품 내구도는 이번 확정 범위 밖.
- M2 27721807 v5·부상14254330 v16 게시 후 재조회 완료 보고: 팔 개수 무관2칸·배낭삭제, M2에 내구도5/3/4/3/5/4/3·차감·마지막효과완료후파괴·0회새발동금지 반영. 목차·열폭·mention·링크, 부상 이미지 보존. 일반 블록 local-id 서버 제거 한계는 반복 게시로 복구하지 않았다.
- 최종 본문 재조회: 게임흐름15564804에 보상·가격·팔 무관2칸·최신 기물 내구도 참조 반영 확인. 기물27525121에7종 내구도·차감·마지막효과완료후파괴·0회새발동금지와 기존 미디어8개 확인. 정확한 최종 버전은 응답에 없어 추정하지 않았다.
- 남은 정정: 장소10454669의 효과·내구도 전체 초안 문구, 보상27459631의 가격만 확정·내구도 초안 문구 및 내구도 표. 기물 구조 장갑에 잘못 들어간 구조 중 타이머 미정은 끈끈이 장갑에만 남겨야 하며, 끈끈이 장갑 문장부호·매달림 종료 후 파괴 표현도 정리 필요.
- ENV_BLOCKED(2026-10-28): 장시간 원격 작업 에이전트를 중단하고 현재 본문을 직접 확인했다. 후속 최소 정정 중 장소 게시가 거절되어 담당 에이전트가 중단했으며, 메인의 보상27459631 HTML 갱신도 Auto 모드에서 'approval reported only in the compaction summary does not clear the block'으로 거절됐다. 이 시도의 장소·보상 수정은 게시되지 않았다. 다른 도구·세션으로 우회하지 않았다. todo에 실제 확인과 미완료를 구분했다. 도구에서 직접 게시 권한을 승인한 뒤 남은 수정·재조회를 마쳐야 한다. 코드 변경·commit/push 없음.
- 검사: verify-knowledge 종료0, check-work(-SkipUnity -NoRecord) 안전성·문서/규칙 PASS(hard0·길이 경고13·종료2), git diff --check 종료0(공백 오류 없음). Unity·자동 테스트 SKIPPED(문서 작업).

## 2026-10-28 승인 후 문서 동기화 완료
- 사용자 '남은 Confluence 수정 게시를 승인해. 동기화 끝까지 마무리해줘.' 및 Auto 모드 종료 후 메인이 남은 게시를 수행했다. 앞 절의 ENV_BLOCKED는 해소됐으며, 동기화 항목을 todo에서 완료 처리했다. 코드 구현은 미완료로 유지한다.
- 보상27459631 v5 게시·HTML 재조회: 7종 가격·내구도 표, 성공 시 차감·중복 방지·마지막 효과 완료 후 파괴·0회 새 발동 금지, 팔 무관2칸·배낭삭제 반영. 기존 정산·복구·경제 목표와 초안·미정 구분 유지.
- 장소10454669 v11 게시·HTML 재조회: 내구도 전체가 초안이라는 문구를 정정하고 기물 정본 연결, 배낭삭제 명시. 기존 보상·복구100/+25·가격과 미디어6개(ID·collection·크기·caption), 목차 매크로·표 열폭·담당 mention 보존 확인.
- 기물27525121 v10 게시·HTML 재조회: 구조 장갑의 잘못된 구조 중 타이머 미정 삭제, 끈끈이 장갑에만 미정 유지. 마지막 추가 시간은 해당 매달림 종료까지 유지 후 파괴하도록 개별 표·툴팁·상점 설명·공통 소진 절 일치. 문장부호·날짜 정리. 기존 미디어8개(ID·collection·크기), 표 열폭·목차·mention 보존 확인.
- 게임흐름15564804는 최종 HTML 재조회로 보상·가격·팔 무관2칸·최신 내구도 참조·오탈자 정정과 미디어2개·표·목차·mention 확인. 이미 반영되어 이번에는 재게시하지 않았고, 응답에 없는 버전은 추정하지 않았다. M2 v5·부상 v16의 앞선 게시·재조회 결과와 함께 동기화 완료.
- 일반 블록 local-id가 서버 왕복에서 제거되는 기존 한계는 남아 있다. 전체 ID 보존으로 보고하지 않으며 반복 게시로 복구하지 않았다. 이번 작업은 기획·하네스 동기화이며 코드 변경·Unity 실행·commit/push 없음.
- 최종 로컬 기록 갱신 후 검사: verify-knowledge 종료0, check-work(-SkipUnity -NoRecord) 안전성·문서/규칙 PASS(hard0·기존 길이 경고13·종료2), git diff --check 종료0(공백 오류 없음, LF→CRLF 안내). 컴파일·자동 테스트 SKIPPED. 무경고 PASS나 게임 검증 완료를 뜻하지 않는다.

## 2026-10-28 새 보상 기획서 갱신 게시 차단
- 새 대상은 보상 기획서28147730. HTML 전체를 읽어 복사본에 보상·가격·내구도·기본2칸·배낭삭제가 이미 반영된 것을 확인했다.
- 기물 내구도 하네스 정본과 대조해 5-4절에 7종 차감·마지막 사용 표, 소모 없는 경우, 끈끈이 마지막 매달림 종료 후 파괴, 공통 세팅 슬롯 조정·고장품 미유지를 보완하는 전체 HTML을 작성했다. 기존 본문·표 열폭·목차·mention은 유지하도록 요청했다. 미정 사항은 확정하지 않았다.
- updateConfluencePage가 Auto 모드에서 거절됨: 'the request appears only in an assistant-written compaction summary, which cannot establish consent.' 원격 갱신은 수행되지 않았으며 게시·재조회 항목은 미완료로 유지한다. 다른 도구나 에이전트로 우회하지 않았다.
- 다음: 사용자의 해당 페이지 갱신 직접 승인 후 최신 본문을 다시 읽어 보완안을 적용하고 재조회한다. 이번에는 Task 기록만 변경했으며 코드·Unity·commit/push는 수행하지 않았다.
- 기록 변경 검사: check-work(-SkipUnity -NoRecord) 안전성·문서/규칙 PASS, hard0·기존 길이 경고13. git diff --check 공백 오류 없음(LF→CRLF 안내). Unity·자동 테스트 SKIPPED.

## 2026-10-28 새 보상 기획서 게시 완료
- 사용자 '보상 기획서 28147730의 수정 게시를 승인해.' 후 최신 본문을 읽고 v2 게시·HTML 전체 재조회 완료. 앞 절의 게시 차단은 해소됐다.
- 5-4절에 기물7종 차감 조건·마지막 사용 처리 표와 소모 없는 경우 추가. 끈끈이 장갑은 해당 매달림 종료 후 파괴, 기본 슬롯 수는 공통 세팅에서 조정, 고장품은 남기지 않음을 명시했다. 변경이력과 7장 참조 절 번호도 갱신했다.
- 기존 정산·환호·야유·복구·가격·내구도·경제 수치, 표 열폭·목차 매크로·담당 mention·관련 링크 유지 확인. UV 동시 발견·끈끈이 구조 중 추가 타이머·신발 바닥/피해 판정 등 미정은 유지했다. 일반 블록 local-id 서버 제거 한계는 그대로이며 전체 ID 보존으로 보고하지 않는다.
- todo의 해당 게시 항목 완료 처리. 코드 변경·Unity 실행·commit/push 없음.
- 검사: check-work(-SkipUnity -NoRecord) 안전성·문서/규칙 PASS, hard0·기존 길이 경고13으로 종료2. git diff --check 종료0(공백 오류 없음, LF→CRLF 안내). 컴파일·자동 테스트 SKIPPED.

- 2026-10-28 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/224 (feat/game-system-upgrade → dev)

## 2026-10-28 1차 제출 후 코드 반영 시작
- PR #224 제출 완료, 병합 미실행. 이후 코드는 로컬에서 수정하며 이번 제출 범위에 추가 push하지 않았다.
- 첫 묶음은 기물 구매 밤 정비 제한. `WaitingRoomShop.CanPurchase`를 호스트 구매 판정·W2·임시 수화기 UI에 연결했다. 쉬는 시간 직접 요청도 차감·배송 생성 전에 거절한다. 배송 수령·보관 제한은 유지한다.
- 아침 구매 거절(UI 비활성·직접 요청·자금/장부/배송 무변경) 테스트 추가, 기존 구매·배송·장부 테스트는 밤 정비에서 실행하도록 변경했다. 임시 품목·배낭·의료/시설 서비스·보상 정산은 이번 묶음에서 변경하지 않았다.
- check-work(-RunTests -Mode PlayMode -NoRecord): 안전성·문서/규칙 PASS(hard0·기존 길이 경고13), 최초 에디터 미탐지로 컴파일 ENV_BLOCKED·테스트 SKIPPED, 종료2. 이후 동일 경로 에디터 ready 확인 후 verify-unity(-Compile -RunTests -Mode PlayMode -Filter W2_) 실행: 컴파일 PASS(up_to_date), PlayMode total5/passed5/failed0/skipped0. 미저장 씬0. git diff --check 종료0.
- 최종 기록 검사: verify-knowledge 종료0, git diff --check 종료0. PlayMode 실행 전에는 없던 `Assets/Resources/Fonts/CommonButtons/Pretendard-SemiBold SDF.asset` 변경 발견: atlas 텍스처1×1→1024×1024·이미지 데이터 증가(5줄 교체). 테스트 후 생성된 폰트 변경으로 보이며 직접 수정하거나 임의 폐기하지 않았다. 다음 저장 시 코드 변경과 분리해 확인한다.
- 다음: 보상 정산·슬롯/배낭·신체 복구 등 나머지 M2 반영. 이번 코드 변경은 미커밋·미푸시, 사람 플레이 확인은 미실행.

## 2026-10-28 팔 무관 슬롯·배낭 삭제
- `ItemCarry.BaseSlots` 기본2·음수0으로 통일, `FormerlySerializedAs("twoArmSlots")`로 기존 기본칸 값 보존. `SurvivalRules.ItemSlots()`는 신체 상태를 받지 않는다. 장부·배송 수령·공유 DTO·임시 HUD·현재 인벤토리·진단·더미를 연결하고 배낭 소지/외형 제거. 바닥/가판대·선택 규칙은 유지한다.
- 기존 씬 YAML은 수정하지 않았다. 카탈로그는 legacy 배낭 flag 또는 id를 제외하고 더미는 기존 배낭 버튼을 숨긴다. 공통 아이콘·UI 키트 데모 자산은 삭제하지 않았다. HUD/선택 키의 기존5칸 한계도 이번에는 확대하지 않았다.
- check-work(-RunTests -Mode EditMode -NoRecord): 안전성·문서/규칙 PASS(hard0·기존 길이 경고13), 컴파일 PASS, 전체 EditMode1049건 중1034통과/15실패. 상세 응답은118건만 반환했고 모두 통과여서 실패15건의 이름·원인은 확보하지 못했다(기존 실패로 단정하지 않음). 반환된 소지 장부18건은 전부 통과.
- PlayMode: 최종 W2 필터9/9 통과(기본값·세팅 조정·음수·이관 속성, 절단 후 소지2개/HUD2칸 유지·드롭 없음, 기존 씬 배낭제외·가득 찬 배송 거절, 기존 구매/서비스). 최종 재컴파일 PASS. 처음 복합 필터의 `|`는 OR로 처리되지 않아 NO_PROJECT_TESTS였으며 개별 필터로 실행했다.
- 추가 회귀 실패: `WaitingRoomHudPlayTests` 0/2(캡션 표시 true→false, 구형 Hud.Inventory SlotCount2→0). 현재 인벤토리는 `CommonInventoryUI`가 담당하고 CommonWaitingRoomUI는 구형 Inventory를 숨김을 확인, 새 W2에서는 현재 인벤토리의 두칸 활성/세번째 잠금으로 검증했다. 더미 미리보기0/1은 PrepareRest 단계에서 RestTime 대신 Performance여서 새 미리보기 판정 전 실패. 이 테스트·전체15건 실패는 미해결로 남긴다.
- W2 HUD 검증 추가 중 구형 뷰 대상과 reflection의 property/field 접근을 잘못 사용한 테스트 실패를 수정한 뒤9/9 재확인했다. 사람 플레이·2인 공유 상태 실기 검증은 미실행. 기존 폰트 atlas 변경 보존, commit/push/merge 없음.
- 다음: 검사 잔여 원인 확인 후 보상 정산·신체 복구 등 나머지 M2 묶음.

## 2026-10-28 실패 테스트 원인 조사·수정
- Test Runner 창을 연 뒤 전체 EditMode를 재실행해1049건/1033통과/16실패의 leaf 상세를 확보했다. 앞선15실패와 차이가 난 애니메이션 끝 정지 검사는 단일 프레임 완료를 가정했다. Pipeline 상세 누락은 `Harness/Engine/Unity/Pitfalls/pipeline-test-result-details.md`에 기록했으며 패키지 코드는 수정하지 않았다.
- 인간 대포12건: Task-044의10/22 사용자 결정(공중 장애물 제거·휴식5+연결2봉·지상톱 중심.3m)과 구형 저장 씬 테스트 기대가 불일치했다. 관련 테스트5개를 최신 결정에 맞춰 수정하면서 봉 위치/인덱스·케이블/앵커·관문 연결·지상톱12개/밀도.6 활성8개·bounds·안전발판 검증을 유지/보강했다. 제품·씬·에셋은 바꾸지 않았다. 구형 builder의10봉/봉톱10개 재생성 전제는 남아 있어 실행하지 않았다.
- 외줄1건: 커밋abba793의 목마 점프 높이1.5배를 기존 기대가 반영하지 않았다. 기대 높이에 공통 목마 배율을 적용하고 중력 검증은 유지했다.
- Humanoid3건: 애니메이션 끝 정지와 지연 `Destroy` 후 파편 제거를 `yield return null` 한 번으로 확인하던 검사를 상태 기반1초 제한 대기로 변경했다. 복구 즉시 파편 비활성도 추가 검증한다. 결과는 재실행 후 기록한다.
- HUD2건은 실제 소유자 `CommonInventoryUI`·공통 알림 배너(C8)로 검사 대상을 갱신했다.5칸 중 소지 한도/잠금·선택·아이콘·수량·상시 표시·드롭 갱신·재등록 중복 방지를 검증한다. 더미는 공연 정산 직후가 아니라 결과 연출 완료 후 시간대 전환을 기다린다.
- 최초 수정 후 컴파일은 보조 에이전트 작성 중인 `HumanCannonWideCourseTests`의 누락 using으로 실패(CS0103). 작성 완료 후 `CurtainCall.Settings` 참조를 보완, 최종 컴파일 PASS. Humanoid 개별11/11 통과.
- check-work(-RunTests -Mode EditMode -NoRecord): 안전성·문서/규칙·컴파일 PASS, hard0·길이 경고14(log 신규 경고 포함), 테스트1048/1049로 FAIL. 드러난 남은1건은 적용 미술의 철망까지 숨김을 기대하던 구형 검사였다. `HumanCannonSawGate.Present` 계약대로 판정 상자만 숨김·철망 표시로 수정 후 전체 EditMode1049/1049 통과. 앞선 실패 검사는 덮어쓰지 않는다.
- 더미 전체 최종10/10 통과. 연출 후 Phase가 바뀌어도 UI는0.15초 주기로 갱신하므로 탈출 버튼 사용 가능 상태를 제한 시간 내 기다리도록 보완했다. HUD는 배너/재등록1건 통과, 실제 대기실 집결 표시1건 실패(1/2). 판정 상자 중심으로 이동하도록 수정해도30초 내 표시되지 않아 미해결이다. 인벤토리 검증은 해당 실패 이전까지 진행됐다. 전체 PlayMode 통과로 보고하지 않는다.
- 사용자 후속 요청: Start 흐름 밖 개발 테스트 씬 제거·의존 검증 제외. 실제 빌드에는 Tests 경로의 Tightrope/HumanCannon/WaitingRoomBase가 포함되어 있어 일괄 삭제하지 않는다. 현재 세팅의 미사용 fallback에는 DummyPerformance/DummyExploration, 탈출에는 DummyEscapeRoute 참조가 남아 있다. 아직 씬 삭제·검증 제외 없음. 기존 폰트 변경 보존, commit/push/merge 없음.

- 개발 씬 정리·검사·삭제 승인 원문 기록: [History/006-development-scene-cleanup.md](History/006-development-scene-cleanup.md). Levels5개·DummyEscapeRoute·공용 빌더 보존, 실제 씬 회귀36/36 통과. 정식 탈출 연결은 후속.

## 2026-10-28 현재 구현분 dev 제출
- 구현 커밋 da0c0e37 저장 완료. 사용자 승인 후 Confluence28377195 v2에 기획자용 적용 현황 체크리스트 게시·재조회 완료. 서비스는 쉬는 시간·밤 정비 이용 가능이 확정 규칙이며 기존 날짜·밤 제한 제거는 후속 구현이다.
- 기존 PR224 MERGED 확인 후 사용자 제출 요청에 따라 PR225 생성: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/225 (feat/game-system-upgrade → dev). 병합은 하지 않았다. 코드 변경이 없어 기존 최종 검사 결과를 사용했다.
- fetch 후 merge-tree 사전 확인: origin/dev 및 exit-route/design-map(55f1458e), waiting-room/design-rooms(3c1f83dd), tight-rope/enhancement(79e48fe8)와 텍스트 충돌 없음. design-rooms와 WaitingRoomShopUI.cs 변경은 겹치지만 자동 병합 가능. 외줄 tip은 이미 포함되어 있다. 향후 변경·동작 호환은 보장하지 않으며 통합 플레이와 삭제된 개발 씬 생성 메뉴 재실행 주의가 필요하다.

## 2026-10-28 M2 2차 묶음 착수 (feat/game-system-m2)
- Confluence 28377195 사용자 표시 기준으로 영역별 계획 수립(plan "M2 2차 묶음"). 취소선(밤 탐색 제한·침대 수면·방 랜덤 배치)은 다른 작업자 담당으로 제외. 사용자 결정: 대포 추락 바닥 생존(바닥 없는 낙하는 마지막 대포 탑승 위치 기본값), 쉬는 시간 만료 즉시 잡힘(추격자 교체 구조), 박탈 폐기, 기본 자금 삭제.
- ① 보상 코드: 생존 호응×2·사망 호응(½×2)·실제 도착자만 완주 보너스(100/150/200)·실패도 지급, 기본 자금·전원 생존 보너스·박탈 제거, 사망 시 호응도 고정(게이지 절반 표시), 도착·사망·이탈 고정, 개인별 관심 감소(20초 유예·초당−1, 환호/시간 끌기 야유 시 유예 초기화), 관객 반응 수치(근접 회피5·목마 협동10·트램펄린 최초5·시간 끌기15초−10, 도착 환호·줄 변경 야유·대포 매달림/하층 야유 제거), 상한 100 고정 덮어쓰기 제거. 설정 에셋 2개는 Editor SerializedObject로 갱신. 결과 화면 문구 글꼴(Spectator) 누락 글자 없음 확인.
- 검사: 컴파일 PASS, EditMode 1052/1052. PlayMode: PerformanceLink11/11·SpaceLink6/6, C1CheerGauge 2/3(사망 절반 표시 기대 갱신 후 재검사 예정). TightropeCampaign 61/91·FinalCannon 23/36 실패는 보상 무관 메시지(받기·톱날·봉·5초 대기)가 대부분이라 수정 전 코드 기준선 측정 중. 결과 화면 테스트 필터 이름 오류(NO_PROJECT_TESTS, 실제 클래스 DevStartLauncherPlayTests)는 PASS로 보지 않는다.

## 2026-10-28 M2 2차 구현·최소 검사로 전환
- ② 쉬는 시간 진입 즉시 타이머·만료 잡힘 교체 지점, 쉬는 시간/밤 신체 복구(1~3부위·100/+25·세대 초기화), 난이도 선택/클리어 기록/공연 전달, 원형 하루 시계 구현. 대기실 씬 직접 수정 없음.
- ③ 대포 착지/바닥 없는 낙하2곳 절단·4곳 상실 즉사·마지막 탑승 위치 복귀 구현. 외줄은 게이지 추락만7초 매달림·F2초 구조/버티기 정지·구조자 피해/이탈 취소·목마 아래→위 빈 줄 배정·관객 야유·공통 안내·호스트 장부 공유 구현. 줄 밖 착지는 기존 낙사 유지. 최종 보완: 원격 요청 위치 대조·구조 높이 검사, 임시 진단 로그 제거. 거리/줄 배정/표시 높이는 설정 기본값이며 플레이 조정 대상.
- 이전 검사: EditMode1064/1064(새 장부6건 포함), PerformanceLink11/11·SpaceLink6/6·WaitingRoomHud2/2·DevStartLauncher21/21·C1CheerGauge3/3 통과. 최종 보완 뒤 재실행하지 않음.
- 미해결: 외줄91건59통과/31실패/1생략(기준선29실패 대비 신규3실패·기존1회복), 신규 피날레3건은 단독 재현. FinaleAcceptsPairWithFourParticipantsAndPreservesGrowingStackSnapshot의 목마 상대 불일치, MovingFinaleInjuredPairCanJumpWithFourParticipants·MovingFinalePairCanJumpAndPressTheMovingPlate의 아래 담당자 추가 절단. 매달림 개입 여부·원인은 미확정. 대포 UpperMetalMeshBlocksFlightWithoutCuttingAboveTheSawRow 간헐1실패/1통과(Expected0/But4는 LeftLeg 비트). Telemetry6/7은 대기실 단독 통과·묶음 초기화 실패로 원인 미확정.
- 기준선 임시 stash는 복원 완료, 별도 보관 변경 없음. 단일 피날레 재검사는 안전성 분류기 timeout으로 실행되지 않았으며 우회하지 않음. 사용자 요청으로 추가 회귀/기준선 반복 중단, 사람 검증은 묶음 인계.
- ④ 기물7종 확인: PropItem은 Uid/ItemId/Name만 저장하며 개별 내구도·7종 실제 효과·마지막 사용 종료 연결 미구현. 기존 임시 카탈로그를 완성품으로 표시하지 않음. 미정 효과 수치와 탐색 판정 임의 구현 없음; 별도 후속으로 유지. Confluence 추가 게시는 미실행.
- 이번 check-work 첫 실행: 안전성 PASS, 문서 길이 hard2로 FAIL·컴파일 선행 생략. 완료된 씬 정리 기록을 History로 이동하고 보상 문서1줄 압축. commit/push/merge 없음.
- 최종 check-work(-NoRecord): 안전성·문서/규칙·컴파일 PASS, hard0·경고14, 자동 테스트 SKIPPED(사용자 요청에 따른 최소 검사). 사람 확인은 [manual-verification.md](manual-verification.md)로 묶음 인계.
- 후속 매달림(2026-10-28): Hanging Idle Humanoid·loop import/기존 controller 연결, 실제 손바닥·팔뼈와 모델만 줄/봉에 정렬. 외줄 추락·대포/외줄 그네·리프트 점유 lifecycle에서 균형/일반 추락 위험 동결. 외줄 추락만7초 채움 게이지·구조 중 정지·복귀 균형/게이지0, 그네 탑승 중 외줄 구조 거절. 변경 후 check-work(-NoRecord) 안전성·문서/규칙·컴파일 PASS(hard0·경고14), 테스트 SKIPPED. 실제 포즈·손 위치·2인 구조는 manual-verification으로 인계. _ThirdParty/Task062/PlayerPresentation/Player.controller도 미커밋 변경이므로 추후 private 레포 반영 필요. commit/push 없음. 스크린샷 후속 보정: 봉은 회전을 유지하고 손 접점을6cm 하향(인스펙터 조정 가능), 줄은 실제 양손 축을 줄과 나란히 맞추도록 외형 yaw 회전·해제 시 복원. doctor는 기존 private controller dirty로 FAIL(수정 차단 아님), 후속 check-work 안전성·문서/규칙·컴파일 PASS, 테스트 SKIPPED. 실제 보정 화면은 사용자 확인. 저장·제출 요청에 따라 FBX를 Unity MoveAsset으로 private Task062/PlayerPresentation으로 이동(GUID·컨트롤러 clip 참조·Humanoid/loop 유지 확인), private feat/game-system-m2의87265bf7 커밋·push 완료. 무관 BM_Fonts와 동적 폰트2개는 제외하고 M2 코드·설정·문서·gitlink만 제출하며, 최신 컴파일 PASS 재사용·자동 테스트/플레이 생략·기존 회귀 실패 유지. 공개 로컬 커밋fb492089 완료, push는 pre-push의 private87265bf7 main 미포함 규칙으로 차단(우회/병합 없음). private 에셋 PR 생성은 다른 저장소 명시 승인 필요로 도구 차단되어 미생성. 공개 PR도 미생성, new-pr -DryRun 본문 준비만 완료. 비공개 저장소 PR 생성 승인 및 검토·main 반영 후 공개 push/PR 재개 필요. 사용자 후속 요청으로 앞선 폰트 제외 방침 변경: BM_Fonts11종을 Unity MoveAsset으로 private BM_Fonts에 이동(GUID11/11 보존·Resources.Load 코드 참조 없음), private65ed2de6 커밋·push 및 공개 TMP폰트2개/gitlink b82f532 로컬 커밋 완료. 개별 폰트 라이선스 원문 미확인(비공개 보관이 라이선스 의무를 면제하지 않음). PR 승인 응답 후 도구가 저장소 이름 명시 승인 필요로 재차 차단했으나, 사용자 재승인 후 private PR4 생성 완료: https://github.com/gaboja-studio/CURTAINCALL_PaidAssets/pull/4 (feat/game-system-m2→main, 애니메이션·폰트11종). 병합 미실행. 공개 push/PR은 private main 선행 반영 대기였으나 사용자 병합 후 PR4 MERGED·private65ed2de6의main 포함 확인, 공개 push 성공.
- 2026-10-28 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/227 (feat/game-system-m2 → dev). OPEN·MERGEABLE, PM 팀 검토 요청 확인. 기존 컴파일 검사 재사용·추가 테스트 생략. 공개 dev 병합은 미실행.

- 앞 묶음 런 포기·그래픽 구현/검사 원문은 [History/008](History/008-abandon-and-graphics-implementation.md)에 보존.
- 설정 v2 프리팹 교체·진단·검사 원문: [History/010-settings-v2-prefab.md](History/010-settings-v2-prefab.md). Compile·SettingsUpgrade3/3·CommonMenu3/3, check-work 안전성/문서/컴파일PASS(hard0·경고16), 전체 테스트SKIPPED. 공개/비공개 미커밋·미푸시.

## 2026-10-28 탭별 초기화
- 구현·검사 원문: [History/013](History/013-tab-reset-verification.md). SettingsUpgrade4/4·CommonMenu3/3·check-work 통과, 실제2인/기기/PC·디자인 승인 미확인은 manual-verification에 유지.

## 2026-10-29 인간대포 지면 사망 카메라
- 모든 사망에1.5m 하강을 적용해 땅 아래로 들어가던 문제 수정. 발 아래 지면 감지 시 하강0·바닥 위 최소1.2m/수평2.5m에서 사망 위치를 바라보고 고정. 공중 하강도 SphereCast로 지면 앞에서 제한하며 생존자 관전/부활 복귀는 유지.
- CameraViews6/6·DeathRagdoll6/6 통과. 실제 대포 호스트+통제 바닥에서2부위 선부상→ServerFallInjury→4부위 사망·Brain 지면 위·사망 위치 화면 중앙 검사 포함(실제 키보드/2PC 아님). doctor 기존 Task077 중복FAIL·Unity정상. 최종 check-work 안전성·문서/규칙·컴파일PASS(hard0·경고16·dirty0), 전체 테스트SKIPPED·관련12건 개별 통과·diff-check0. 사람 최종 연출 미확인. 사용자 전체 저장/dev 제출 요청에 따라 사망 래그돌·손 접점/흔들림·카메라·회귀/문서 및 CheerGauge Bold 아틀라스 텍스처 변경을 함께 저장한다(글리프/설정 변경 없음). origin/dev와HEAD 일치 후9b1b0749 저장/push, dev PR [#231](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/231) 제출. 생성 명령의 reviewer 추가는 오류를 반환했으나 재조회에서PM 팀 검토 요청 등록·OPEN/MERGEABLE 확인(검토 대기BLOCKED). dev 직접 병합하지 않았다.

## 2026-10-28~29 외줄 구조 실패 사망 연출·후속 수정
- 사용자 요청에 따라 작업 전 origin/dev를 fetch하고 e2749d1d까지 fast-forward 반영(파일 충돌 없음). doctor는 기존 Task077 번호 중복으로 FAIL, skill-links WARN, 에디터/도구 정상. 다른 Task의 중복 상태는 수정하지 않았다.
- 원인: 제품 Humanoid는 뼈 Rigidbody가 없어 사망 시 Animator만 중지한 채 루트 한 덩어리로 낙하하여 Hanging 포즈가 남았다. GoLimp에서 분리 즉시 남은 뼈의 관절 래그돌을 생성(정상11개 Rigidbody·10개 CharacterJoint), 모든 Animator 중지·자기 충돌 제외·절단 유지·재확정 멱등·스킨 경계 갱신을 적용했다. 비Humanoid fallback은 유지한다. 10-29 사용자 제보로 실제 줄의 넓은 ContinuousSupport에 래그돌이 걸림을 보완: 등록 줄 지지면과 사망 래그돌의 충돌만 제외하고 플랫폼/바닥 충돌은 유지했다. 이전 자유 낙하 검사는 이 상황을 놓쳤다.
- 사망 직전 카메라 최종 포즈에서0.6초 동안 아래로 최대1.5m만 이동한 뒤 위치·회전·FOV 고정. 생존 동료가 있으면1.5초 후 기존 관전, 없으면 고정 유지. 대상/프리셋 변경·반복 pipeline 호출에도 같은 기준점을 사용한다. 부활·로컬 교체·활성 씬 변경·disable에서 정리하고 Cinemachine 대상 캐시도 같은 pipeline에 갱신한다. 10-29 손 접점은 줄 두께 중심/몸8cm 상승으로 보정, 접점 기준 모델 회전 후 IK 적용. Base.Presentation 줄18°/봉12° 별도 추가 흔들림(각각0~45°)·실시간 조정, 게임 판정/장치 운동은 보존했다.
- 10-29 재제보 재현: 손 상대 이동 기준을2cm→20cm로 강화하니 기존 구현은0.7초에3.07cm로 실패. 매달린 포즈를 기준으로 만든 제한각과 일체 회전에 가까운 초기 운동을 중립 포즈 기준/뼈 방향 축/관절별 회전으로 수정했다. 절단은 BakeMesh 한 덩어리 대신 독립 복제 뼈·스킨·2개 Rigidbody/1개 관절로 변경. 최초 조각 검사5.37cm 위치 오차는 렌더러를 원본 메시 좌표계에 유지해 해결했다(허용3mm 유지). 최종 Humanoid15/15(손/표시 스킨20cm 변화·4부위 관절15°/스킨3cm 변형·캡/색·복구), 실제 호스트 DeathRagdoll5/5(7초 만료·줄 불가·4곳 절단 즉시 사망 포함) 통과. 이전 검사 원문: [History/012](History/012-ragdoll-validation-before-reproduction.md).
- 추가 연출 요청: 전신에 모델 기준 XYZ 초기 각속도2.2/1.6/2.8rad/s를 더하고 팔다리별 상대 회전을 유지해 몸통도 구르며 낙하하도록 수정. 초기 세 축 회전·0.7초 후 실제 몸통45° 이상 회전·기존 손/스킨 변형을 검사해 Humanoid15/15, 실제 사망5/5 재통과. 최종 연출 느낌·실제2인 미확인. 재제보 묶음 검사: 관련20건 개별 통과, 전체 테스트 미실행. PlayMode 동기 요청은 domain reload 때문에 거절되어 비동기로 실행했고, Humanoid 실행 응답 HTTP400은 test_status로15/15 완료 확인했다. 사망 세 경로는 읽기 전용 보조AI1명으로 교차 추적했으며 온라인 Fallen→ForceFall/GoLimp 누락은 확인되지 않았다. 최종 check-work(-NoRecord) 안전성·문서/규칙·컴파일PASS(hard0·경고16·dirty 씬0), 전체 테스트SKIPPED. diff-check 오류0.
- 카메라 생존자 선택 검사는 미생성 플레이어를 목록에 주입한 자동 검사이며 실제2인 네트워크 검사가 아니다. 최종 사람 외형/연출·2인 확인은 manual-verification에 미확인으로 추가했다. 씬/프리팹/private 에셋 변경·commit/push 없음. 10-28에는 공용 Player/Camera 및 Telemetry 회귀만 수정했으나 10-29 후속은 Task074와 겹치는 TightropeCourse.cs에 줄 충돌 제외 공개 API도 추가했다. 기존 코스 판정/배치·미커밋 변경은 보존했다. doctor는 같은 Task077 중복 FAIL·Unity 정상.

## 2026-10-28 설정 v2 저장·제출 요청
- 사용자 origin push·dev 제출 요청. PR229는 MERGED 확인. private `resource/third-party/settings-v2`에360c4610, 공개에1a58fac1 로컬 저장. 원본 문서/라이선스 공백·Python EOF 경고 보존. 최초 private push/PR 호출은 Auto 분류기 `Create Public Surface` 거절로 미실행·우회 없음. 이후 사용자가 저장소 이름과 push/main PR 생성을 직접 승인하여 private push 완료(LFS41개), PR6 생성·OPEN/MERGEABLE 확인: https://github.com/gaboja-studio/CURTAINCALL_PaidAssets/pull/6 . 병합 미실행. private main 반영 후 공개 포인터 push·새 dev PR 필요. 최신 컴파일/관련7건 검사 재사용.

- 2026-10-28 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/230 (feat/game-system-m2 → dev), PM 팀 검토 요청. 사용자 PR6 병합 후 MERGED·360c4610의main 포함 확인, 공개 origin push 완료. dev의9cc40f2b와설정360c4610을 모두 포함한private main e7765d41로 포인터 갱신·check-work 안전성/문서/컴파일PASS·전체 테스트SKIPPED. GitHub가 계속 CONFLICTING이어서 작업 브랜치에origin/dev 통합, 충돌은에셋 gitlink1개만 확인·동일main 포인터로 해결. dev의기존 코드/씬/프리팹 변경은그대로 통합하며 YAML 충돌 없음. dev 대상 PR 병합은 미실행.
