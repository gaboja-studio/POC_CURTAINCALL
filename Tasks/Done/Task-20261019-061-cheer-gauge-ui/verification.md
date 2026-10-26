# 검증·환경 기록 (2026-10-19)

## 결과
- 로컬 구역: `pwsh -File Scripts/verify-scope.ps1 -Base integration/cheer-gauge-ui` PASS, .meta 포함.
- 문서/규칙: `verify-fast.ps1` PASS (기존 WARN7건, private 미저장 경고). handoff 크기 HARD는 압축 후 재검사.
- 컴파일: 전용 Unity6000.3.25f1 `recompile_status`: completed, failed=false, errors=[], compilationFailed=false.
- EditMode: 필터 `CurtainCall.CheerGauge`, 18/18 통과.
- PlayMode: 동일 필터, async_tests, 3/3 통과. `Temp/pipeline_test_status.json`에 결과 (재실행 시 덮어씀).
- 표준 `check-work.ps1 -Mode EditMode` FAIL: `origin/integration/cheer-gauge-ui` 없음. 문서/컴파일/테스트 후속 SKIPPED.
- 표준 검사 실패를 개별 검사로 숨기지 않음. 전체 프로젝트 테스트/정식 제출 검증은 미완료.

## 확인 범위
- 표시 자동 검사: 155 합계, 1인150, 상한200에서800, 빈 링, 추락 강조, 0.2초 전환, 반복 갱신·재활성.
- EditMode: 상한100/150/200, 1~4명, long 합산, 명부·색 고정, 사망·이탈, 새 공연/세대/세션, 잘못된 입력.
- 실제 오프라인 공개 API: 늦은 서비스 생성→0, 환호→20, 야유→15, 사망→10, 제거→‘—’, 새 서비스→‘—’.
- 1920×1080 실제 Overlay 화면 원본 비교 완료. 상한150→600 직접 표시 확인.
- 16:10 GameView(561×351)에서 상단 중앙·화면 내 배치 확인, 원래 Full HD 설정 복원.
- 사람의 클릭·호스트/참가자 네트워크·공동 테스트는 미실행. 합성/API 검사를 실제 입력 검사로 보고하지 않음.
- 실제 추락 공개 신호가 없어 강조는 View API/샘플만 제공. 야유를 추락으로 해석하지 않음.

## 키트 정리 사전 검증 (10-19)
- 전체 프로젝트 AssetDatabase 전이 의존성 12,188개 및 GUID/문자열 경로 대조: 삭제 후보 33파일 참조 없음. 후보 전부 메인 원본과 바이트 일치.
- 유지 대상은 private PNG8·TTF2·OFL LICENSE와 상위 폴더/.meta. 중복 Editor/Runtime, Docs/Previews/Tools/README, Source의 Fonts 외 내용이 삭제 후보다.
- 최신 씬 Play: 합계155, Ring190×190, Roof(0,38)/Flag(10,76), Roof 좌우 Image 존재, Missing Sprite0. 1920×1080 Overlay 캡처 확인 후 Play 종료.
- 재실행 EditMode18/18·PlayMode3/3 통과. 실제 오프라인 API 환호20→야유15→사망10→서비스 제거/재생성‘—’ 포함. recompile_status up_to_date, errors=[], compilationFailed=false.
- 최초 삭제 호출은 auto 권한 모드에 차단됨. 이후 PM이 현재 권한 모드에서 에셋 삭제를 명시적으로 허용해 Unity DeleteAsset로 33파일·동반 .meta 정리 완료. 필수11파일 유지, 프리팹·씬/유지 에셋/.meta의 32개 해시 불변.
- 정리 후 전이 의존성10개(PNG8·TTF2) 정상, Missing Dependency/Script/Sprite0, 숫자·호응 글리프 정상. 컴파일 up_to_date/errors=[]/compilationFailed=false.
- 정리 후 재실행 EditMode18/18(0.51초)·PlayMode3/3(0.91초) 통과. 확인 씬 Play의 155·사용자 수정 계층·1920×1080 실제 Overlay도 정상, 종료 후 프리팹/씬 해시 불변. 구역/fast/knowledge 재검사 PASS(기존 WARN7), private 무관220파일 diff 통계 불변.
- Harness Project/Facts/c1-cheer-gauge.md·index 및 기존 third-party-assets 결정 갱신. verify-knowledge PASS. 구역 개별47파일 PASS, fast PASS(기존 WARN7), 표준 check-work는 원격 통합 브랜치 부재로 동일 FAIL/후속 SKIPPED.
- 임시 조사 스크립트 첫 컴파일은 Unity에서 Convert.ToHexString 미지원으로 실패했으며 BitConverter.ToString으로 수정 후 통과. 프로젝트 C# 컴파일 실패와 구분.

## 환경·보존
- 시작 doctor FAIL은 원본 private의 미추적 C1 키트. 원본 보존, 전용 worktree 복사본만 import.
- private HEAD b92b37a4, 키트와 신규 .meta 원격 미저장. 다른 PC 재현은 private 저장이 선행돼야 함.
- 최초 import에서 HDRP SubG_HeightLerp KeyNotFoundException 관찰. C# 컴파일 오류와 별개, 해당 에셋 수정 안 함.
- 전용 private의 기존 LeartesStudios 머티리얼/바이너리 씬도 변경 상태. M_Red diff의 URP AssetVersion10/키워드 자동 변환 확인.
- 최초 도구의 전역 SaveAssets가 import 중 dirty인 자산까지 저장할 수 있어 생성 폰트만 SaveAssetIfDirty하도록 제한함. 자동 변환 차이는 미커밋·보존, 승인 없이 reset/discard하지 않음.
- CLI beta6 `status`는 인스턴스0 오보고, `--until-ready` 미지원. 명시적 project-path의 editor_status/recompile_status로 확인.
- 동기 PlayMode HTTP는 도메인 재로드로 거부돼 async_tests 사용. CLI screenshot은 Camera.Render라 Overlay를 누락, 실제 ScreenCapture로 확인.
- 기존 개발 배지가 상단 HUD를 가림. 비교 때만 런타임에서 일시 숨김, 저장하지 않음. Play 종료로 복원.
- 메인·locomotion 작업 폴더와 기존 게임씬/Reward/ProjectSettings/Packages 코드 변경 없음.
