# Handoff
## 지금 상태
- 최신 중단: 공개 선택 스테이징과 save-work DryRun 통과(구역/문서/컴파일 PASS, 테스트 SKIPPED). 이후 commit/push 호출이 auto mode 권한 검사에서 실행 전 거부됐다. 검사기는 요약에 담긴 승인으로는 경계를 해제할 수 없으며 사용자 직접 확인이 필요하다고 보고했다. 공개 새 커밋/push 없음, 파일·인덱스 보존, 우회 없음. 저장 후 C7이라는 요청 순서를 지켜 C7은 아직 시작하지 않았다. 아래 저장 재개 문구는 이번 재차 차단 이전 이력이다.
- Task working: C6·C15 구현/검증 완료, 차단 보고 후 PM이 "지금까지 변경 사항을 commit, push" 명시 재요청하여 저장 재개. 전용 `D:/github/POC_CURTAINCALL-cheer-gauge-ui`, `feat/ui-resources` → `integration/ui-resources`. 정식 인계·PR/병합 없음.
- PM C15 근접·패널 품목 설명 묶음 구현 및 완료 후 저장 요청. AI가 이전 C6까지 포함한다고 확대 해석했으나 결합 저장은 실행 전 권한 검사에서 차단됐다. 해석 철회·앞선 C6 커밋/push 금지 유지, 신규 commit/push0·우회 없음.
- C15 모드 전환 프리팹1종·독립 Tooltips 씬·Preview20버튼·안전 Builder·Static Medium/SemiBold2개·PlayMode20건 구현. 제공 private Runtime/Builder 원위치 namespace 격리·주입형 조립·인스턴스 초기화로 재사용.
- 툴팁 원본49실파일/PNG14 전체 보존, 생산 신규7/기존 PNG 재사용0. 시트 RGBA7 일치·보호baseline613 SHA 변경0·원본 누락0. [전수 목록](tooltips-inventory.csv), [C15 검증](tooltips-verification.md), `Harness/Project/Facts/tooltips.md` 참조.
- 표시 API/폭256..480·TMP 높이160..240·근접 고정 꼬리/패널 연속 아래 선·unscaled120/100ms만 구현. 감지/거리/시선/투영/추적/가장자리 보정·우선순위·구매·게임/네트워크 연결 없음.
- C6 안내판/진행 링·독립 Interaction 씬의 완료 결과는 미저장 보존. 이전 최종52/52·입력90·56상태/배치8화면 이력은 [C6 검증](interaction-verification.md), `Harness/Project/Facts/interaction.md` 참조.
- 기존 모든 UI 정본 보존. 공유 Dynamic TMP2개 과거 dirty/추가 SHA 차이·Buttons 과거 실패 원인은 미결이며 이번 통과로 닫지 않는다. 임의 복구/재작성 금지·PM 확인 필요.
- private 무관 tracked 변경·미추적 unity_only·공개 `Assets/Scripts/Systems/Interaction 1.meta` 보존/저장 제외. private 작업 브랜치 `resource/ui-interaction-tooltips`·스테이징226파일 보존, 공개 인덱스 비어 있음.
- 후속 명시 승인으로 C6·C15 private226파일 `73fd48aa88bd5330c7b1837d94936fb063365a89` main push 완료. 공개 결과/하네스/gitlink는 이 문서 포함 저장 실행. 이전 차단 이력은 아래 상세/검증 문서에 보존한다. C7은 이번 저장 후 별도 조사·구현.
## 다음 할 일
1. 새 명시 승인으로 저장 차단 해소. public 선택 스테이징/DryRun·push 확인 후 C7 guides 조사·작업 구역/계획 갱신 및 구현·검증한다. 새 C7 결과는 이번 저장 대상이 아니다.
2. 사람 최종 외형 확인은 기능 완성 후 한 번에 진행한다. 근접/패널·긴문구·꼬리/두 화면비/배경·시간 정지/초기화를 독립 씬에서 확인하며 공동·실게임 확인과 구분한다.
3. 공유 폰트/Buttons 미결·DEV MODE 전역 배지·테스트 씬 처리·PR/통합은 별도다.
## Verification
- 구역 검사: PASS (10-20)
- 1 문서/규칙: PASS (10-20)
- 2 컴파일: PASS (10-20)
- 3 테스트: PASS (EditMode, 798/798, 10-20)
- 신규 Tooltip20/20 + 기존 Cheer3/Buttons2/Panels3/Icons3/Performance9/Backstage7/Notifications9/Interaction16 = PlayMode72/72 PASS, failed/skipped/inconclusive0. 실제 Update·unscaled·반전과 elapsed 주입 곡선 검사는 구분했다.
- 최종 실제 Mouse/Enter82건 각1회·persistent listener1, 1920×1080/1600×1000·밝고 어두운 배경56화면 PASS. glyph/빈mesh·화면 밖·Controls 전체 rect와 툴팁 꼬리/모션 여유 겹침 오류0. 최초 집계 오류와 회귀 수집 HTTP400/전환 reset 이력은 C15 상세에 보존했다.
- 구조: 직렬화 참조3115/missing0·Missing Script0·Sprite7/플랫폼49 override0·Static2·EventSystem/module 각1·Controls raycaster1·sorting8/10. Preview 원래timeScale.37 Reset/비활성/예외 복구·독립 Play 재진입 초기 숨김·정지 종료 후1 복구 PASS.
- 종료 stopped/Tooltips 씬clean/1920×1080/timeScale1/background=false. 편집 Show 거부/보존·Builder 기존 출력 거부 PASS. 현재 Console errors0/compilationFailed=false, 과거 보존 버퍼와 구분한다.
- doctor FAIL은 시작부터 보존할 private 변경으로 보고 완료. 최종 검사 후 저장 차단 문서만 갱신, scope268/verify-fast/knowledge9 재검사 PASS(길이 경고11·hard0). 현재 Console warnings5는 errors0/실제 컴파일 PASS와 구분한다. reload 설정 변경/both OFF 지원·전체 프로젝트 PlayMode·사람 외형/공동/네트워크는 미검증.
