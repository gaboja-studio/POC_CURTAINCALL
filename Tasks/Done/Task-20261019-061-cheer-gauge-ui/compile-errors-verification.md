# UI 키트 컴파일 충돌 수정 검증

## 요청·재현 (2026-10-20)
- PM 요청 원문: "유니티에 컴파일 에러가 55개 떠있는데 이걸 먼저 수정한 뒤에 저장해줘." 월드 라벨 공개 저장 대기까지 포함한다는 것은 AI 해석이며 명시적 재승인으로 기록하지 않는다.
- 대상: `D:/github/POC_CURTAINCALL-cheer-gauge-ui`, 공개 `feat/ui-resources`, 전용 Unity 6000.3.25f1/port7801.
- doctor: FAIL (`third-party HeadMismatch`), WARN(CLI 문서 버전 차이·skill-links 없음). 기존 private HEAD6ff6e18e와 공개 포인터 차이/무관 변경 보존, 자동 복구 없음.
- `unity recompile --project-path <대상> --strict`: exit6. `console_status.groundTruth`: compilationFailed=true, compiling=false, consoleErrors=55, consoleWarnings=0.
- 원인: 새 shop 키트가 `CurtainCall.UI`에 공통 Button/SelectableVisual/TabVisual을 중복 선언. CS0101·CS0579·CS0111과 settings Toggle/Tab·shop RowVisual의 CS0534가 함께 발생했다. 과거 버퍼의 CaptionView 오류와 현재55개를 구분한다.

## 수정·보존
- shop Runtime8파일을 `CurtainCall.UI.ShopV1`, Editor1파일을 `.ShopV1.Editor`로 격리했다. 단순 타입 참조는 같은 namespace로 이동해 추가 치환 불필요, 설정 키트/정본 변경 없음.
- 수정 전후534파일 SHA 대조에서 shop C#9개만 변경, PNG/meta/settings 변경0. 파일명·클래스명·GUID·기존 씬·공유 폰트 보존.
- 신규 UI 조립·실게임 연결·C11 제작·삭제·PR/병합은 이번 작업에 포함하지 않는다.
- 기존 공유 Bold·Interaction 1.meta·브랜치 정책3파일·private Leartes 변경 및 다른 키트는 저장에서 제외한다.

## 결과
- `check-work.ps1`: 구역/문서/컴파일 PASS, EditMode798/798 PASS.
- `verify-unity.ps1 -RunTests -Mode PlayMode -Filter WorldLabelPlayTests`:18/18 PASS, failed/skipped0.
- 실제 `console_status.groundTruth`: compilationFailed=false, compiling=false, consoleErrors=0, consoleWarnings=0.과거 버퍼 오류123개는 현재 오류가 아니다.
- 종료: WorldLabels 단일 씬 clean, 에디터 ready/Play stopped.새 상점 UI 기능·외형·전체 PlayMode는 미검증, 기존 Buttons/Tooltip 미결을 해결했다고 주장하지 않는다.
- private 상점 키트277파일(원본/라이선스/제작자료/meta 포함)만 선택 저장:9661aa85.원격 README3a5029dd를 일반 병합한6b4e2041을 main에 push 성공(LFS23개).settings 및 다른 키트는 로컬 보존/저장 제외.
- private diff --check는 제공 원본2건을 지적했다: `Source/Fonts/LICENSE.txt:22` trailing whitespace, `Tools/export_shop_assets.cjs:151` EOF 빈줄.원본 보존을 위해 수정하지 않았으며 컴파일/공개 저장소 검사와 구분한다.
- 공개 저장 DryRun은 최종 구역/문서/컴파일 PASS, 선택 인덱스 diff --check PASS. 최초 문서 폴더 UI 이름 오류는 Ui로 보정했고 다시 검사했다.
- 공개 선택 commit/push 호출은 실행 전 권한 검사에서 거부됐다. 월드 라벨 포함 명시 재승인 기록이 부정확하다는 사유이며 해당 문구를 정정했다. 공개 commit/push는 실행되지 않았고 HEAD9ede8a3·파일/인덱스를 보존했다. 다른 도구 재시도/권한 우회 없이 사용자 확인 대기.
