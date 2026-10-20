# C16 장소 전환 커튼 검증

## 범위·제작
- 2026-10-20 PM 현재 요청. private `curtaincall_curtain_v1` 원위치 제공 Runtime/Builder 재사용. 실제 장소 로드·네트워크·입력 제한·영속화·삭제·commit/push·PR/병합 없음.
- 정본: `Assets/Resources/Prefabs/UIs/TransitionCurtain/TransitionCurtain.prefab`(Canvas 전체), 독립 씬: `Assets/Scenes/Tests/TransitionCurtain/TransitionCurtain.unity`.
- `TransitionCurtainProjectBuilder.Build()`는 Play/import/compile/PrefabStage/dirty 씬/기존 출력·meta 충돌을 거절한다. additive 임시 씬에서 제공 `BuildProject`로 조립, 부분 실패 산출물은 보존하며 기존 정본 재생성 없음.
- 기존 C11 Static Medium·공용 Icons 별은 읽기 전용 참조. 신규 천/술6 importer만 명세에 맞게 보정. C16 출력은 private 자산과 C11 폰트 의존성이 있으며 단독 배포 패키지가 아니다.
- PreviewControls 9버튼은 합성 데모(닫기/열기/즉시/반전/시간정지·복구/명암/긴문구/기본문구). 커튼 자체는 raycast 비차단, Controls는 커튼 뒤 Canvas(-1)이다.

## 원본·보호
- 최초 baseline 1019파일, 그중 키트129파일/PNG15. 개별 경로·최초/현재 SHA256 전수 목록은 로컬 비공개 검증자료로 보존하고 공개 저장에서 제외한다.
- 좌/우 body의 원본 px `(32,0,2016,2240)` 영역 전수 alpha min=max255 확인. 명세의 불투명 중앙 겹침64UI·전체1952×1120 차폐 근거와 일치.
- 보존 검사 재실행: PNG/자료/기존 정본·폰트 변경0, 허용된 Builder1·신규 importer6만 변경. 원본 삭제0/누락0. 작업 중 외부 추가된 results/title_logo 키트는 최초 baseline에 없으므로 전체 SHA 보존 검증 대상으로 주장하지 않는다.

## 실행 이력·실패 보존
- doctor FAIL0/WARN2(CLI 문서 버전 차이·skill-links 없음), 자동 설정 수정 안 함.
- 최초 eval 읽기 조회·Game View focus 조회 각각 `Main thread operation timed out after 5000ms`. editor_status ready 확인 후 별도 호출은 정상. 표시 연결/컴파일 성공으로 이 실패를 숨기지 않는다.
- 첫 구역 검사 FAIL2: 신규 C# 임포트 전 `.meta` 없음. 첫 호출에서 세 검사를 `;`로 연결해 문서/컴파일까지 진행한 절차 오류가 있었다. AssetDatabase.Refresh 후 `&&` 계단식 재실행하여 구역/문서/컴파일 PASS 확인.
- 조립 성공·출력 저장 후 첫 PlayMode: total16/passed15/failed1/skipped0(12.39s).
- 실패: `TextRemainsLiteralAndNullIsEmpty`, 기존 테스트252행 `Assert.IsTrue(label.isTextOverflowing)`에서 Expected True/Actual False. 설치된 TMP `TextMeshProUGUI.cs:3987-4011` 가로 Ellipsis 경로는 `m_firstOverflowCharacterIndex`를 설정하지 않고 말줄임 문자를 대입한다. 플래그 대신 원문 폭 초과·실제 visible U+2026·문자 수 축소·원문 보존으로 검사 보정. 제품 모션/글자 크기/문구를 바꾸지 않았다.

- 2차 PlayMode도15/16: 보정 테스트가 `GetPreferredValues`를 메시 검사 전에 호출하여 characterInfo의 말줄임 문자가 원문 문자로 바뀌었다. 독립 진단에서 측정 전 visible U+2026/문자17개, 측정 후 동일 인덱스 원문 '중'·preferredWidth599.35 확인. 메시 검사→폭 측정 순서로 재보정했고 아직 실행 통과를 주장하지 않는다.
- `check-work.ps1 -Filter ...` 호출은 지원하지 않는 인수로 실행 전 실패. 파일의 실제 매개변수는 SkipUnity/Mode이며 최종 표준 검사는 아직 실행되지 않았다.
- 최초 실행 캡처 자동화는 editor_play 직후 domain reload 중 다음 run_script 연결이 끊겨(Connection reset/HTTP400) 화면 수집 전 중단. finally editor_stop 이후 stopped/현재 Console오류0·경고0 확인. 진입 호출과 수집을 분리해 재시도하며 최초 실패를 보존한다.
- 캡처2차는 프로젝트 외부 저장 제한으로 실패. 3차는16장 성공했지만 요청한 `.harness/local`을 도구가 `Assets/.harness/local`로 바꿔 저장했다. 이 생성물 때문에 표준 구역 검사 FAIL18→후속 단계 SKIPPED. 생성16PNG를 scratchpad로 이동하고 생성한 빈 폴더만 제거 후 표준 check-work/EditMode798/798 PASS. 원본 삭제/설정 우회 없음.
- 첫16화면 육안 확인에서 Preview 버튼이 보이지 않아 조사: Canvas renderMode 기본값 WorldSpace. ProjectBuilder/저장된 C16 씬의 Preview Canvas만 명시적 Overlay로 보정. 합성 이벤트는 렌더 검증을 대신하지 못한다.
- 같은 화면 명암 픽셀 대조에서 화면 가장자리1px에 배경 혼합 발견(예 x0,y500 dark65/29/35, bright174/164/155; 안쪽2px 제외 차이0). RectMask2D 경계 감쇠 방지를 위해 공개 조립기와 C16 프리팹의 clip padding만 -2UI로 확장, 원본 천/술·좌표·크기·Runtime 불변. 초기 구조 테스트에 padding 회귀 추가. 보정 후 렌더 재검사 중.
- Overlay 보정 직후1920×1080 dark의 버튼9개 raycast/PointerClick13건/비활성 콜백1건 PASS. 다음 조회는 Network error, 정리 호출은 Connection reset으로 실패했으나 editor_stop 후 stopped 확인. 현재 Console1오류는 앞선 캡처 저장 거부 로그이며 제품 Runtime 예외가 아님. 첫 실패·부분 화면은 보존한다.
## 최종 결과
- 과거 차단(2026-10-20, 아래 승인·재검사로 해소): 시작 이후 새로 추가된 미추적 `Assets/_ThirdParty/curtaincall_results_v1/`의 `CommonButtons/Runtime/CurtainCallButton.cs`가 기존 CurtainCall.UI 버튼 타입과 충돌. 정식 Compile FAIL21(CS0101/CS0579/CS0111), 보정 후 PlayMode·회귀/EditMode·실행 화면/4화면비·최종 Console 검사는 선행 컴파일 실패로 SKIPPED.
- PM 질문 응답 `최소 수정 후 계속`으로 results CommonButtons namespace를 ResultsV1로 격리하고 ResultsPanelView/Builder 참조만 수정(3파일). 첫 정식 컴파일 재확인은 빈 `COMPILE_FAILED`로 실패했으나 후속 recompile_status completed/failed=false/compilationFailed=false·현재 Console오류0 확인. 그 이전 컴파일 중복 오류는 누적 버퍼에 남아 있으며 현재 오류와 구분한다. 그 뒤 정식 재검사 성공은 아래에 기록한다.
- 후속 정식 Compile PASS(up_to_date), C16 신규 PlayMode16/16 PASS·failed/skipped0 확인. 앞선15/16·컴파일 차단은 과거 이력으로 보존. results는 승인3파일만 수정, 기능 제작/원본 삭제/Git 저장 없음.
- 보정 후 정식 Compile/C16 PlayMode16/16 재통과. 그 직전 run_tests HTTP400 실패는 통신 이력으로 보존한다.
- 최종 실행 캡처16장 PASS: 1920×1080/1920×1200/2520×1080/3840×1080 × dark/bright × open/closed. 각 닫힘 상태 IsFullyCovered=true/실제 Screen 크기 확인. 로딩 애니메이션 영역만 제외한 dark/bright 전체 화면 픽셀 차이0(가장자리 포함), 열림 잔상 없음·닫힘 천/술/밸런스·로딩 별/문구 렌더 확인. 
- 보정 후8조건의 버튼 화면 내부/최상위 raycast72건·합성 PointerClick104건·비활성 콜백8건 PASS. 물리 Mouse/Keyboard 입력 검증은 아니며 기존 DEV MODE 전역 배지는 소유 밖이므로 보존. 
- 캡처는 도구 반환 savedPath에서 scratchpad로 즉시 이동, 생성 빈 폴더만 정리해 Assets 산출물0. finally OpenImmediate/1920×1080/runInBackgroundfalse/editor_stop 수행. C11 선택 회귀26/26 및 최종 check-work 구역/문서/컴파일·EditMode798/798 PASS. 최종 원본 SHA 재검사도 승인7파일 외 변경0/누락0.
- 종료 참조 점검1070/missing0/Missing Script0·기존 출력 재생성 거절 PASS. C16 편집 씬 clean/Play stopped/timeScale1/1920×1080/runInBackground 원래false. 최종 현재 Console오류0/경고0(누적 버퍼의 과거 오류·예상 실패 경고는 별개). 문서 크기 WARN10/hard0·private 미저장 주의는 보고하며 commit/push하지 않았다.
- 사람 최종 외형·4화면비에서 주름/술/밸런스·전환 손맛·실기기 메모리/플랫폼·공동/실게임 검증은 자동 테스트와 별도다.
## 후속 저장 요청 (2026-10-20)
- 사용자 직접 `커밋 푸시 진행해줘.` 승인으로 이번 C16 결과/기록 및 private 포인터 저장 진행. 기존 미저장 문구는 제작 당시 이력이다. 외부 추가 results/title_logo 키트 전체와 results3파일 로컬 보정은 제외·보존한다.
- private C16 승인7파일 `dec9c907`, 원격 main의 공연장 에셋/README 이력을 일반 병합한 `76a7e908` main push 완료. 원격 대비 C16 7파일만 추가 차이, 기존 원격 이력 재작성 없음. 공개는 save-work 미리보기/컴파일 후 feat/ui-resources에 저장한다. 첫 실제 저장 호출은 private 경로/SHA 전수 CSV 공개 배포 우려로 실행 전 권한 거부됐다. 우회하지 않고 해당 목록을 공개 대상에서 제외·로컬 보존한 뒤 검증 요약/제작물만 다시 검사한다.
