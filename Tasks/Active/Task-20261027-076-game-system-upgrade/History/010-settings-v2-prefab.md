# 설정 v2 프리팹 교체 기록

## 2026-10-28 설정 v2 프리팹 교체
- 사용자 제공 private `curtaincall_settings_v2`를 Editor API로 제품 `SettingsUiV2/C14_Settings_v2.prefab`으로 조립, CommonSettingsUI의 실제 메뉴 표시 경로 교체. 944×552/5탭/840×240 스크롤·고정 푸터·전체 딤 없음. 기존 씬·v1 프리팹은 유지, 데모 데이터는 사용하지 않는다.
- 공용5클래스 v2 namespace 격리·Builder 경로/호출 API 보완, private Sprite43(기존36+신규7)·Medium/SemiBold 재사용. 새 SettingsUi Bold Static512/제목6문자 생성, 기존 WorldLabels Bold의 런·포 누락 보완. 기존 공용 폰트/PNG 변경 없음.
- 실제 화면/FPS·키GUID·전체/BGM/SFX·음성/기기/팀원·런 포기·크레딧 연결. 토큰/세션·탭간 draft·Apply/Cancel·기본키 복원·ESC 단일 전달·게임 탭 비활성·확인 시점 런 상태/실행 권한 재검사 유지. 기존 SettingsTabsView/ExtensionPanel은 실행 경로에서 제외.
- doctor는 제공 패키지 미저장 private dirty로 FAIL1·skill-links WARN1, 에디터/도구 정상. 원본 초기화·임의 폐기 없음. 첫 컴파일의 v1/v2 SettingsValue 충돌은 V2Value 별칭으로 해결.
- 생성 CanvasGroup 누락: GetComponent fake-null(==null true/ReferenceEquals false)에 ??가 작동하지 않음 확인, 명시적 null 검사로 수정·기존 프리팹 참조만 보완. ToggleGroup 무통지 전환의 강조 잔상은 대상 탭 먼저 선택 후 전체 visual 갱신. 런 포기 wrapper 같은 프레임 권한 갱신 누락도 재현·보완.
- 테스트 초기 실패는 UDP7777 타 Unity 점유로 테스트만 OS 임시 포트 분리(제품/타 에디터 불변). 도중 Pipeline `editor status` 미등록 로그1건으로 실패(발신 원인 미확정), 복합 | 필터1회는 NO_PROJECT_TESTS. 수정 후 최종 Compile PASS·SettingsUpgrade3/3·CommonMenu3/3(포인터/시작/로비/대기실/외줄/대포) 통과.
- 제품 참조 null0/MissingScript0·theme검증/고유 Sprite43개·1920×1080 실행 캡처 확인. 제목 누락은 전용 Bold 적용 후 glyph 검사/캡처로 재확인. 실제2인/음성기기/PC빌드/게임패드·사람 디자인 승인은 미완료, 기존 피날레/대포/Telemetry 실패도 해결로 간주하지 않음.
- 최종 check-work(-NoRecord): 안전성·문서/규칙·컴파일PASS(up_to_date), hard0·길이 경고16, 전체 자동 테스트SKIPPED(관련6건 개별 통과). Knowledge OK·diff-check 오류0, 씬 clean. 이번 교체는 공개/비공개 모두 미커밋·미푸시, PR229에는 아직 반영되지 않음. private 신규 패키지도 추후 함께 저장해야 재현 가능.
