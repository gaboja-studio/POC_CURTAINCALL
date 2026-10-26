# C12 공연 결과 패널 UI 검증

## 범위
- 2026-10-21 사용자 제공 results 키트를 ResultsUi 전용 프리팹/독립 확인 씬·명시 데모로 조립한다. 실제 보상 지급/게임 진행/경제/네트워크/저장/입력 소유 연결·삭제·commit/push·PR/병합은 제외한다.
- 전체 원본/PNG/GUID/자료와 기존 정본/UI/공유 폰트를 보존한다. private 개별 경로·SHA 전수 목록은 scratch에만 두며 공개하지 않는다.

## 준비·조립
- 연속 Task의 기존 doctor 결과를 유지하고 중복 실행하지 않았다. 전용 worktree feat/ui-resources·Unity6000.3.25f1/pid28976/port7800 ready/Play stopped 확인. 선행 W3 공개0707167/private23b9d0a7 저장·push 완료.
- 보호 baseline1277파일과 PerformanceHUD 추가164파일 확보. 시작 공개 tracked 변경 없음, private results/title_logo 미추적 보존. ResultsUi 전용 구역과 제공 Runtime/Builder 안전 보정·신규 importer만 허용하도록 소유 표/Task 배정 기록.
- 시트25/25 RGBA·재사용 정본19/19 SHA 일치. 첫 대조 스크립트는 버튼의 패널 키트 내 복사본도 검색해 중복2개로 중단했으나 키트별 정본 경로 지정 후 통과했다. PNG 수정 없음.
- 제공 Builder에 주입 전용 BuildProject 진입점 추가, 공개 조립기는 신규6 importer만 설정한다. 공유 정본19/importer/폰트는 읽기 전용이다.
- PerformanceHud3폰트는 C12 글리프가 부족했다. Spectator Medium/SettingsUi SemiBold Static 재사용·ResultsUi 전용 Bold Static2048/245문자를 생성했다. U+2212는 제공 데모/실제 샘플/RequiredText에 없으며 임의 문구 치환은 하지 않았다.
- ResultsPanel.prefab·ResultsUi.unity·Preview28버튼·PlayMode28건 작성/조립. 실제 버튼 비활성/비상호작용 시 직접 Acknowledge 호출도 차단하도록 원본 Runtime 최소 보정.

## 최초 검사와 보정
- 구역 PASS, 문서 Fast OK(WARN12/hard0), Compile PASS 후 PlayMode28건 중25 PASS/3 FAIL, skipped0. 실행517.04초; 각 테스트는 사용/공유 폰트 SHA를 전후 검사하므로 실행 비용이 크다. 백그라운드 전환 뒤 결과까지 기다렸으며 중복 테스트 실행 없음.
- 실패1: 목적지 null 테스트에서 params 배열 자체가 null로 전달되어 Reflection TargetParameterCountException. `(object)null`로 단일 인자를 전달하도록 테스트만 수정.
- 실패2/3: 제목 실제 렌더 없음·긴 단원명 말줄임표 없음. 실행 씬에서 TMP 실측 결과 제목48pt 필요57.26 > 영역56, 단원/거리24pt 필요28.63 > 영역28로 characterCount0이었다.
- 글자 크기/패널/리본/레인 중심을 유지하고 제목 텍스트 높이64(상단36), 단원/거리32(레인 중심±16)로 보정. 기존 소유 프리팹의 RectTransform9개만 Editor API로 수정했으며 전체 재생성/삭제 없음.
- 측정 스크립트의 Vector2 JSON 직렬화가 순환 참조로 실패한 한 차례는 scalar 높이 출력으로 수정했다. 제품 코드 예외가 아니다.
- 보정 후 구역514파일 PASS·문서 Fast OK(WARN12/hard0)·Compile PASS·전체 C12 PlayMode28/28 PASS(failed0/skipped0). 실제 렌더/입력·회귀·최종 종료 검사는 별도로 이어간다.

## 후속 제목/부제목 간격 조정 (2026-10-21)
- 사용자 요청으로 패널1120×608→1120×640, 부제목 상단104→128. 제목 영역 아래 여백4→28·리본 아래16 확보, 글자 크기/너비/상단 배치 유지. 하단 거리/마커/보상/동전/확인을32 내리고 Builder/Runtime/프리팹 및 기하 테스트 기대값을 함께 보정했다.
- 두 화면비·명암 기본/긴 문구8캡처 확인, 겹침/누락 글리프/빈 렌더0. 화면비별 합성 raycast29/클릭29/Submit1·Preview28·비활성1 재통과. 초기 캡처의 이전 Runtime 좌표는 AssetDatabase 갱신 후 재검증해 해소. 재로드 직후 QA 연결 실패1회는 실제 Play 상태 조회 후 재실행 통과했다.
- 최종 참조1646 누락0·씬 clean/Play stopped/timeScale1/1920×1080/runInBackgroundfalse·Console오류0/경고0 확인. verify-scope는 기존 TimeManager.asset로 여전히 FAIL, 계단식 문서/정식 Compile/PlayMode 재실행은 SKIPPED. 위28/28·회귀55/55는 간격 변경 전 결과이며 이번 재통과로 주장하지 않는다.

## 후속 저장 승인·재검사 (2026-10-21)
- 사용자 커밋/푸시 요청 후 첫 DryRun은 TimeManager 구역 FAIL로 저장하지 않았다. 질문 응답 `변경 포함 승인`으로 단일 파일 예외를 setup에 기록, 구역516/문서Fast OK(WARN12/hard0)/Compile PASS. 기존 timestep0.02의 새 rational 표현2822399/141120000(약0.019999993)·serializedVersion2를 그대로 포함하며 추가 튜닝은 없다.
- 제목 간격 기하 Play1/1·최종 check-work 구역/문서/Compile/Edit798/798 PASS. 전체 C12 28건 재실행은 아니며 이전28/28와 구분한다.
- private233파일 f60ae144 main push·원격 SHA 일치 완료. 원격 README 자동 갱신만 FF 수신, title_logo 미추적 보존·저장 제외. 원본 LICENSE/Tools2개 공백 경고는 원본 보존상 수정하지 않았다. 공개 C12 결과/기록/gitlink/승인 TimeManager를 feat/ui-resources에 저장·push한다. 아래 저장 제외/구역 차단은 당시 이력이다.

## 보존·남은 확인
- 현시점 baseline1277 누락0/승인 코드2+신규 importer6 외 변경0/PNG변경0, 추가164 변경·누락0 확인. 최종 종료 후 다시 검사한다.
- 실행 검증: 1920×1080/1920×1200·명암 각 실패/성공/대성공·2/3인·로컬 없음·위치 겹침/누락/범위 밖·긴 문구·동전·시간 정지 총48 PNG 확인. Preview 겹침/누락 글리프/빈 렌더0, 두 contact sheet와 개별 기본/긴 문구 캡처를 직접 확인했다.
- 화면비별 합성 raycast29/클릭29/Submit1·Preview28버튼/비활성 콜백 차단1 통과. 확인은 의도만 통지하며 자동으로 닫지 않는다. ScreenCapture 실제 렌더이며 물리 장치 입력은 아니다.
- 종료 audit: 프리팹 참조1646/missing0/MissingScript0, EventSystem/InputModule 각1. 기존 출력 재조립 거절·프리팹/씬 SHA 불변. ResultsUi 씬 clean·Play stopped·1920×1080·timeScale1/runInBackgroundfalse 복구 확인. 회귀 W3 20/20·W2 35/35 PASS(failed0/skipped0). 회귀 후 C12 씬을 다시 열어 동일 종료 상태를 확인했다. 현재 Console groundTruth 오류0/경고0(과거 누적 버퍼와 별개).
- 최종 check-work는 구역 FAIL: `ProjectSettings/TimeManager.asset`에 serializedVersion2·Fixed Timestep의 scalar→rational 직렬화 변경 발견. 시작/캡처 후 status에는 없었고 회귀 후 나타났으나 정확한 발생 테스트는 미확정이다. 금지 구역이므로 파일 보존·복구/검사 우회 없음. 후속 문서/Compile/EditMode 단계 SKIPPED이며 이번 EditMode798 PASS로 보고하지 않는다. PM 확인 필요.
- 합성 입력/실행 렌더 확인은 물리 입력·사람 최종 외형·실기기/공동 승인과 구분한다. 사람 확인은 기능 완성 후 한 번에 진행하며 실제 보상/진행 연결과 commit/push는 이번 범위가 아니다.
