# C15·패널 품목 설명 검증 (2026-10-20)
## 승인·구현 경계
- PM 요청: `curtaincall_tooltips_v1`의 근접·패널 내부 품목 설명 묶음 구현·검증 후 저장. 단수 경로가 아니라 실제 복수형 키트를 사용했다. 직전 C6까지 저장 승인으로 본 AI 해석은 철회했다. 앞선 C6 커밋/push 금지는 유지하며, 결합 저장 시도는 실행 전 권한 검사에서 차단됐다.
- 모드 전환 프리팹 `Assets/Resources/Prefabs/UIs/Tooltips/Tooltips.prefab`, 독립 `Assets/Scenes/Tests/Tooltips/Tooltips.unity`, Preview20버튼·공개 안전 Builder·PlayMode20건·Static Medium/SemiBold2개를 추가했다.
- 제공 Runtime/Builder는 private 원위치 `CurtainCall.UI.TooltipsV1` namespace로 재사용. 전역 EditorWindow/PlayReset/삭제·전역 저장을 제거하고 주입형 조립·인스턴스 초기화·선행 Awake Show 안전성을 보정했다.
- 감지/거리/시선/투영/추적/화면 가장자리 보정·C6 경쟁/우선순위·구매·실게임/네트워크 연결은 없다. 자체 수명/자동 숨김도 없다. 사람 외형 승인·공동 플레이는 자동 검증과 별개다.
## 전수·보존
- 최초49실파일+meta62=111행을 [전수 CSV](tooltips-inventory.csv)에 경로/크기/SHA로 보존. PNG14·Source/Tools/Docs/Previews/TTF/라이선스 전체 유지, 누락0.
- 생산 신규7종: plate4=256×192/9-slice border24, 아래 선32×8/Simple, 꼬리2=32×24/Simple. 기존 PNG 재사용0. 시트 지정 rect와 RGBA 픽셀7/7 일치, alpha 범위0..255.
- 원본 대비 변경은 승인한 제공 코드3개와 신규 PNG7개의 importer meta뿐이다. PNG/제작 도구/원본 폰트/문서/미리보기 바이트 변경0.
- 보호baseline613(기존 UI 프리팹·TMP asset·Packages/ProjectSettings·기존 키트 PNG/meta, 완성 C6 포함) SHA 변경0. 공유 Dynamic 폰트 수정/참조 없음.
- private 무관 tracked 변경·unity_only·공개 시작 목록의 `Assets/Scripts/Systems/Interaction 1.meta`는 보존하고 저장 대상에서 제외한다. 기존 공유 폰트2개 dirty/Buttons 과거 실패 미결을 이번 통과로 닫지 않는다.
## 표시·상태 계약
- Image8/TMP2, 좌상단 부모 UI 좌표(+오른쪽/+아래), 기본384×160·폭256..480. 본문 실제 TMP 측정 높이+84를4UI 올림해160..240 제한, 제목28/본문24·자동 축소 없음·Ellipsis/본문 Wrap.
- Nearby 아래 선2개/고정16×12 꼬리·중심24..W−24, Panel 꼬리2/오른쪽 선 숨김·왼쪽 선 연결. 발자국 Nearby H+8.5, 모션 여유4 별도. SetPosition/SetTail은 활성 표시 중만, 거부값 비캐시·Panel 꼬리 거부.
- unscaled120/100ms·아래4UI/EaseOutCubic. 반복 Show/Hide 비재시작·중간 반전 현재 alpha/위치 보존. stable root 유지·Motion만 숨김·첫 alpha0 HasRuntimePresentation과 실제 IsVisible 구분.
- 비유한/편집/비활성/미설정 거부가 빈 문구 숨김보다 우선. 유효 빈 문구만 즉시 숨김, 제목 개행 정리/본문 실제 개행 유지·제어 문자 정리·태그와 리터럴 `\n` 그대로 표시.
- Canvas1920×1080/Match.5/PPU100·Sprite200, Overlay sorting8(패널 위·커튼 아래), Controls10. Mask 밖·모든 툴팁 Graphic/CanvasGroup 입력 비차단, Controls만 raycaster.
## 자동 테스트
- 신규 TooltipPlayTests **20/20 PASS**, failed/skipped/inconclusive0. 실제 프리팹 연결/10레이어·두 모드 기하·폭/높이/꼬리 경계·TMP 측정/Wrap·무효/빈 값 순서·활성 setter·문자 처리·숨김/초기화·모션/반복/반전·timeScale0·비활성/재활성·선행 Awake를 검사했다.
- 50% elapsed 주입의 .875 및 현재 .875에서 퇴장 반값 .109375는 곡선 로직 검사다. 실제 Update 등장/퇴장·unscaled 진행·반전은 별도 UnityTest로 확인했다.
- 기존8종 회귀 Cheer3/3·Buttons2/2·Panels3/3·Icons3/3·Performance9/9·Backstage7/7·Notifications9/9·Interaction16/16 **52/52 PASS**. 신규20 포함 총72/72, failed/skipped/inconclusive0. 전체 프로젝트 PlayMode 일괄 실행과 구분한다. 표준 check-work 구역/문서/컴파일·EditMode **798/798 PASS**. 저장 차단 후 문서만 갱신하고 scope268/verify-fast/knowledge9 재검사 PASS, 문서 길이 경고11·hard0. 최종 Console errors0/warnings5/compilationFailed=false, 경고가 컴파일 PASS의 근거는 아니다.
## 실행 화면·입력
- 실제 Game View1920×1080/1600×1000 × 밝고 어두운 배경 ×14상태=56 PNG를 수집했다. 근접/패널·최소/최대 폭·긴문구·꼬리 좌/우/중앙·반복/제어문자/리터럴/이동 포함.
- 첫 실행의 실제 Mouse/Enter20버튼×두 해상도+timeScale0 두 입력=82건은 각 호출1회·persistent listener1 통과했다. 직접 onClick/메서드 호출은 실제 입력 근거로 세지 않았다.
- 반환형 보정 후56화면 재수집 및82 입력 재검증 모두 PASS. 각 화면 glyph/빈 mesh·alpha1·Controls20 화면 밖·툴팁 전체 rect+꼬리/모션 여유와 겹침·화면 밖 오류0. 대표 두 화면비/배경·12상태 확대 비교에서 줄바꿈/말줄임·고정 꼬리 접합/모드 전환 확인. 사람 외형 승인과 별개다.
- 실제 씬+정본 직렬화 참조3115/missing0·Missing Script0·Image8/TMP2·Sprite7/플랫폼49 override0. Static SemiBold28/NoWrap·Medium24/Wrap, EventSystem/module 각1·Controls raycaster1·Canvas sorting8/Controls10 확인.
- 원래 timeScale.37에서 Preview Reset/비활성/예외 복구 PASS, 독립 Play 재진입 초기 숨김·시간 정지 중 종료 후1 복구 PASS. 편집 Show false/직렬화 보존·Builder 기존 출력 충돌 거부 PASS. 종료 stopped/씬clean/1920×1080/timeScale1/background=false.
## 실패·환경 이력
- Console groundTruth는 compilationFailed=false/compiling=false/consoleErrors0을 확인했다. Pipeline 보존 버퍼의 과거 error68/warn787과 현재 Console 수를 구분하며, 버퍼를 지우거나 과거 실패를 통과로 덮지 않았다.
- doctor FAIL은 시작부터 보존할 private 변경 때문에 발생해 보고했다. CLI 문서 버전 차이/skill-links 경고를 컴파일 통과로 취급하지 않았다. 이번 컴파일은 실제 PASS다.
- 첫 캡처 실행은56 PNG와82 입력 기록 저장 후 집계 단계에서 `TypeError: string indices must be integers, not 'str'`로 실패했다. eval의 Vector2 포함 익명 결과가 문자열이어서 발생, 검사를 float 배열로 보정했다. 원래 기록을 최종 성공 판정으로 대체하지 않고 보정 후56화면/82입력을 재수집해 실패0을 확인했다.
- 기존 UI 회귀 수집 중 Performance test_status1회 HTTP400으로 중단했다. 프로젝트 경로로 Editor ready를 다시 확인하고 이미 완료된 Performance9/9 결과를 보존한 뒤 나머지 클래스만 재개했다. Editor 포트가 재등록되어도 대상은 항상 정확한 worktree project-path로 지정했다.
- 독립 Play 전환 직후 eval1회 `Connection reset by server`로 중단했다. 부분 결과를 보존하고 unity status의 해당 worktree/7801 ready를 확인한 뒤 분리 명령으로 재개해 재진입/정지 종료/편집 거부를 통과했다. 권한 서비스 실패나 우회로 해석하지 않는다.
- 테스트 소스의 실제 U+2028/U+2029 구분자를 char 조합으로 보정한 후 컴파일 PASS·신규20건 PASS를 확인했다. 테스트 이전 코드가 성공했다고 주장하지 않는다.
- 조사 Explore1명은 읽기 전용 범위에서 완료, 테스트 쓰기는 수행하지 않고 보고했다. 이후 쓰기 가능한 보조 AI1명이 테스트2파일만 작성했으며 동시 실행 최대1명·Unity/Git 실행은 메인만 수행했다.
- 권한 서비스 우회·원본 삭제·기존 정본 재생성/Apply·전역 저장·reload 설정 변경 없음. both OFF 지원·전체 프로젝트 PlayMode·사람 공동/실게임 검증은 주장하지 않는다.
## 저장
- 최종: PM이 현재 대화에서 직접 재승인한 후 공개 `b16c3282338aa2cbb83eb1f7f000ac7fb2f61add` 커밋·feat/ui-resources push 완료, 로컬/원격 SHA 일치 확인. private73fd48aa origin/main 조상 확인. 아래 차단/저장 예정 문구는 이전 이력이며 최신 저장 상태와 구분한다. C7 제작은 이 저장 확인 뒤 시작했다.
- 최신 재시도: 공개 선택 스테이징·diff-check·save-work DryRun 완료. 구역268/문서/컴파일 PASS, 테스트 SKIPPED(이전 check-work 결과와 별개). 이후 commit/push 호출이 auto mode 권한 검사에서 실행 전 거부됐다. 사유는 요약의 승인으로는 저장 경계를 해제할 수 없어 사용자 직접 확인이 필요하다는 것. 공개 신규 commit/push 없음·인덱스/파일 보존·우회 없음, Task blocked. C7은 선행 저장 미완료로 시작하지 않았다. 아래 공개 저장 실행 예정 문구는 이번 거부 이전 시점이다.
- 후속 PM 명시 요청 "지금까지 변경 사항을 commit, push"로 C6·C15 저장 승인/차단 해소. private226파일 `73fd48aa88bd5330c7b1837d94936fb063365a89` main push 완료·LFS19/34MB 업로드. 공개 결과/하네스/gitlink는 이 문서 포함 선택 스테이징/DryRun 후 저장 실행. 아래 차단 시점의 HEAD/스테이징은 과거 이력이며 현재 저장 결과와 구분한다. 새 C7은 이번 저장 후 구현한다.
- private 선택 스테이징226파일은 키트2개/상위 meta뿐이다. 원본 OFL LICENSE.txt 두 파일22행의 공백을 diff-check가 보고했으며 원본 보존을 위해 수정하지 않았다. 해당 원본2개 외 공백 검사는 통과했다.
- private `resource/ui-interaction-tooltips` 브랜치 생성·두 키트226파일 스테이징까지 진행했다. 결합 commit/push 시도는 auto mode 권한 검사에서 실행 전 거부: C6까지 포함한 명시적 저장 승인 필요라는 판정. 범위 확장 해석을 철회했으며 다른 도구/경로로 우회하지 않았다.
- 신규 commit/push **0**. private HEAD `1c0f6f0c94ac9d2ff9f13a7a79a9b6553ca572e3`, 공개 HEAD `d7f54e7f93af1b907be418d9b0fc4054aa5376cb` 유지. 공개 인덱스 비어 있음·private 스테이징226파일/기존 무관 변경/모든 원본 보존. Task blocked(저장 권한/범위 확인 대기).
- 공개 save-work DryRun/커밋·push는 선행 private 저장 차단으로 SKIPPED. 저장 완료라고 보고하지 않는다. 현재 파일 전체 add-A·무관 파일 숨김/삭제·reset/강제 push 없음.
