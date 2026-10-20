# W2 수화기 구매·판매 UI 검증

## 범위·계약
- 2026-10-20 PM 현재 요청. `ShopUi` 전용 코드/프리팹/독립 확인 씬으로 조립한다. 제공 Runtime은 `CurtainCall.UI.ShopV1`, Editor는 `CurtainCall.UI.ShopV1.Editor` 원위치 재사용한다.
- 현재 생산49PNG=신규9+승인40, 누적174고유 이력과 구분한다. 키트 전체276파일/PNG58, 기존 정본·UI/폰트·게임 Settings까지 보호 baseline1205파일을 비공개 scratch에 보존했다. private 개별 경로·SHA 전수 목록은 공개 저장하지 않는다.
- Canvas1920×1080/Match.5/PPU100, Sprite PPU200. 패널1056×552/(432,272), 상단240 HUD 보호·전면 딤 없음. 수화기224×80 고정·Simple, 목록592×296/행592×56/간격4, 꼬리 없는 설명376×296 유지.
- 행의 거래 불가와 입력 비활성은 별개. 거래 불가도 선택/설명을 읽으며 행동만 차단한다. busy 중 행동만 차단하고 탭/목록/닫기는 유지한다.
- 가격/수량/재고/경제/서비스/배송/입력 소유 정책·실거래/네트워크/저장 연결 없음. 구매·판매는 표시 자료와 요청/응답 검토이며 거래 시뮬레이션이 아니다. commit/push/PR/병합 제외.

## 제작·검증 계획
- 신규9 importer만 보정하고 재사용40은 원본 바이트/규격 대조 후 정본을 읽기 전용 참조한다. 기존 Static Medium/SemiBold/Bold 우선 재사용, 기존 폰트/GUID/원본/자료 보존.
- 제공 안전 조립 API·소유 Canvas 프리팹/독립 씬/명시 Preview·자동 테스트 제작. dirty 씬/Play/import/compile/PrefabStage/기존 출력/meta는 쓰기 전에 거절, additive 격리·부분 실패 보존·전역 SaveAssets 없음.
- 불변 자료/유효성·초기 Hidden·3탭/선택/empty·동적 스크롤·불가/disabled·buy/sell·busy 중 갱신·토큰/중복/stale/다중 View·닫기/disable/재진입·unscaled/긴문구·입력/참조를 검증한다.
- 구역→문서→컴파일→테스트 순서. 두 화면비/명암 실행 캡처·합성 ExecuteEvents/raycast와 물리 Mouse/Keyboard·사람 외형 승인은 구분한다. 실기기/공동·실게임 HUD/safe area/다른 메뉴 입력 소유는 별도다.

## 실행 이력
- 연속 Task의 최초 doctor 결과 유지, 중복 실행하지 않았다. 대상 Editor7801/6000.3.25f1 ready, 기존 SettingsUi 씬 clean·공개 추적 변경 없음 확인. 기존 미추적 results/title_logo와 results 로컬 보정은 보존한다.
- Runtime 정적 확인 후 좁은 보정: Open 구성 검증에 누락 참조 가드 추가, OpenDemo의 초기 표시 탭과 포커스 일치, 빈 행 재바인딩 시 이전 아이콘 제거. 조립 API는 임포트/저장 없이 주입 Sprite/폰트로 소유 Canvas를 조립하고 Hidden으로 반환한다. 실행 검증은 아직 진행 전이다.

- 생산 시트2064×1592의49조각과 개별 PNG RGBA/치수49/49 일치 확인. 보호 baseline1205파일은 원본/기존 정본을 포함하며 공개 전수 목록은 생성하지 않았다.
- 작성 도중 선행 scope 검사는 신규 코드/폴더의 Unity .meta4개가 아직 생성되지 않아 FAIL로 중단했다. 문서/컴파일/테스트로 진행하지 않았으며 작성 완료·정식 임포트 후 순서대로 재검사한다.
- 폰트: 기존 Bold 제목2글자 및 Medium의 U+2212 누락을 확인했다. 원문을 바꾸지 않고 전용 Bold Static2048/110글자, Symbols Static512/1글자, 기존 atlas/material 공유 Medium Static 변형11325글자+보조1단계를 생성했다. 기존 폰트/atlas/material은 수정하지 않는다. 임의 제목이 전용 Bold 집합 밖이면 승인 폰트를 제공해야 한다.
- 정상 임포트 후 Scope/Fast/Compile PASS. 유휴 update 조립으로 Canvas 프리팹/독립 씬 생성 완료. 첫 PlayMode35 중34 PASS/1 FAIL(긴 행 제목 말줄임표), 한 프레임/Canvas 반영 후에도 동일했다. 실제 렌더 조사에서 제목 높이28에 characterCount0을 확인해 행56·글자24/18을 유지하고 제목/부제 영역만32/24로 보정했다. 제공 Builder와 생성 프리팹에 동일 반영 후35/35 PASS.
- QA 도구의 첫 editor_status 호출은 HTTP400, eval 상태식은 return/세미콜론 누락으로 실패했다. Play 백그라운드 정지로 main-thread timeout도 발생해 대상 Editor 포커스 및 QA 중 runInBackground=true로 해소했다. 제품 자동 거래 처리나 프로젝트 전역 설정을 추가하지 않았다.

- 1차 두 화면비1920×1080/1920×1200×명암×9상태=36캡처/합성 입력 통과. 각 화면비 raycast18/PointerClick16/Move9/Submit1/Scroll1/Cancel1, 요청2건 성공/거절 확인. Preview24버튼은 합성 클릭81회/직접 disabled 콜백 차단1/disable 전역복구1 통과했다. 물리 입력은 아니다.
- 1차 컨택트 시트에서16:10 Preview 오른쪽 끝 버튼 잘림을 확인했다. 확인용 Canvas만 MatchWidth0으로 보정하고 실제 패널 Match.5는 유지했다. 공개 Builder와 생성 씬에 동일 반영, 재컴파일/Play35/35 통과. 최종 두 화면비36캡처에 버튼 화면 경계 검사도 추가했다.
- 중간 보호 검증1205파일: 누락0/PNG변경0/예상 밖 변경0. 변경12개는 승인 코드3+신규 importer9뿐이다. 기존 폰트/정본·게임 Settings는 불변이다.

## 후속 행 높이 변경 (2026-10-20)
- 사용자 요청: 0N_Row 높이56→80. 행 간격4/목록 viewport296/글자24·18은 유지하고 제목·부제와 아이콘/가격을 아래로12 이동해 텍스트 상하 여백12를 확보했다. 한 화면에는 완전한 행3개와 다음 행 일부가 보이며 나머지는 스크롤한다.
- 제공 Builder/Runtime 배치·content 높이/포커스 스크롤 보폭을84로 일치시키고 소유 프리팹5행을 Editor API로 보정했다. 동적 복제12행의80높이/84간격/상하 여백·마지막 행 노출 회귀를 추가, Compile/W2 Play35/35 통과.
- 1920×1080/1200에서 기본 목록/10행 마지막 항목 총4캡처·시각 확인. glyph누락0/Preview중첩0, 마지막 항목 전체 노출·scroll540 확인. Play 종료/1920×1080/timeScale1/runInBackgroundfalse 복구. check-work는 handoff41줄 제한 초과로 문서 단계에서1회 FAIL/후속검사 생략, 두 W2 요약을 합친 뒤 구역/문서/Compile/Edit798/798 PASS. commit/push 없음.

## 후속 저장 (2026-10-20)
- 사용자 직접 커밋·푸시 요청으로 W2 전체와 행 높이80 변경 저장 승인. private 코드3/importer9만42477d6f로 main push·원격 SHA 일치 확인했다. 무관 results/title_logo와 results 로컬 보정은 제외·보존한다.
- 공개 W2 코드/프리팹/씬/폰트3종/기록/gitlink를 feat/ui-resources에 저장·push한다. 아래 commit/push 없음은 제작 당시 이력이다. 공개 DryRun은 구역/문서/Compile PASS였지만 실제 save-work 호출이 실행 전 권한 검사에서 거부됐다(요약에 기록된 승인으로는 불충분). 공개 commit/push는 실행되지 않았고 HEAD6e5fb38/작업 파일을 보존했다. 다른 도구 우회 없이 중단했다. 이후 사용자 직접 "공개 변경도 커밋 푸시해줘" 재승인에 따라 공개 W2 전체·gitlink 저장을 재개한다. 완료는 원격 SHA/최종 보고로 확인하며 PR/통합 병합은 하지 않는다. 새 폰트는 각각8.5MB 이하로 추가 LFS 지정이 필요하지 않다.

## 현재 결과·남은 확인
- 최종 두 화면비1920×1080/1200·명암×9상태=36캡처 완료, 두 컨택트 시트 시각 점검 및 Preview 화면 이탈0/패널 중첩0/글리프 누락0 확인. 각 화면비 합성 raycast18/클릭16/Move9/Submit1/Scroll1/Cancel1·buy/sell 요청2 재통과. 실제 거래 완료/물리 입력으로 해석하지 않는다.
- 기존 출력 재조립 거절·프리팹/씬 SHA 불변 PASS. 참조2536/missing0/MissingScript0, EventSystem/module 각1. 종료 ShopUi clean/1920×1080/timeScale1/runInBackgroundfalse·Play stopped 확인. 캡처 임시 Assets/.harness 디렉터리는 남기지 않았다.
- 최종 W2 PlayMode35/35·C14 선택 회귀23/23·C16 선택 회귀16/16 PASS. check-work 구역/문서/Compile·EditMode798/798 PASS. 마지막 audit도 위 종료 상태/참조 수 유지. 현재 Console오류0/경고2는 C16 방어 테스트의 예상 경고이며 과거 Pipeline timeout 버퍼 오류와 구분한다.
- 최종 보호 baseline1205: 누락0/PNG변경0/예상 밖 변경0, 승인 코드3+신규 importer9만 변경. 기존 무관 results/title_logo와 로컬 보정은 보존했다. commit/push/PR/병합은 하지 않았다.
- 사람 최종 외형/손맛·물리 입력·실기기/공동 확인은 별도. 기존 전체 PlayMode Tooltip/Buttons 및 공유 Dynamic 폰트 미결은 이번 선택 검증으로 닫지 않는다.
