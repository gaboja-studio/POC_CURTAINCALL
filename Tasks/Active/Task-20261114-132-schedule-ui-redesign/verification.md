# 스케줄표 v2 검증 — 2026-11-14

## 결과
- Unity 6000.3.25f1 / Pipeline 0.8 / 명시 경로 D:/github/POC_CURTAINCALL. 실제 CLI로 프리팹 조립·씬 교체·PlayMode·캡처 실행.
- C# 컴파일 PASS. 최종 console_status groundTruth.consoleErrors=0, compilationFailed=false. 기존 코드 obsolete/미사용 경고19건은 남아 있다.
- `ScheduleTicket_RealPointerArrowsSlotsOcclusionAndRestore`: PASS 1/1 (19.45초). 파일 `playmode-input-results.json`.
- `W3_TierButtons_NightChoiceAndRestTimeAdjust`: PASS 1/1 (17.02초). 파일 `playmode-night-rest-results.json`.
- `DifficultySelectionTests`: PASS 5/5. 파일 `difficulty-results.json`.
- 합계 관련 자동 검사 7/7. 프로젝트 전체 테스트/실제 다중 클라이언트 네트워크 세션/사람 조작감 평가는 이 숫자에 포함하지 않는다.
- 최종 check-work: 안전성 PASS, 문서/규칙 PASS, Unity 컴파일 PASS(up_to_date), Common settings PASS. 해당 스크립트의 자동 테스트는 SKIPPED이며 위 개별 실행 결과와 구분한다. verify-knowledge와 git diff --check도 PASS.

## 실제 입력/렌더 검증
- 기존 대기실 실게임 Launch 경로로 시작, Time.frameCount 증가 확인.
- InputSystem 가상 Mouse에 위치/down/up 상태 이벤트를 넣고 EventSystem 첫 hit의 클릭 담당이 예상 버튼인지 검사. onClick 직접 호출만으로 판정하지 않는다.
- 화살표 클릭은 공연 ID를 보존하고 단계/보상을 갱신. 슬롯 클릭은 실제 공연을 변경하고 해금된 난이도를 유지.
- 1단계 하한·3단계 상한·미해금3단계·1일차 고정·성공 표시 검사.
- 실제 플레이어 SkinnedMeshRenderer를 BakeMesh로 복제한 렌더 전용 캐릭터와 BoxCollider, 카메라 PhysicsRaycaster를 배치. Overlay가 캐릭터 앞에 그려지고 UI hit가 콜라이더보다 우선함을 확인.
- 닫기 시 WorldSpace/원래 부모/레이캐스터 비활성/다른 화면 hold 해제, 재열기 후 다시 실제 화살표 입력 정상.
- 읽기 전용은 동일 카드 컴포넌트의 host=false 표시 계약 검사다. 실제 원격 참가자 접속 테스트가 아니다.

## 로컬 캡처
- `Captures/focused-final-layout.png`: 최종 실제 Game View. 카메라 앞 가림 캐릭터가 있는 상태.
- `Captures/focused-tier-applied-with-character.png`: 난이도 변경 직후 적용됨 표시.
- `Captures/occluder-world-camera.png`: 동일 테스트의 UI를 제외한 월드 카메라. 실제 가림 모델 배치 증거이며 수정 전 게임 화면으로 표기하지 않는다.
- `Captures/readonly-client-presentation.png`: 카드 하나의 읽기 전용 표시 검사. 실제 클라이언트 화면으로 표기하지 않는다.

## 중간 실패와 수정
- 프로젝트에 Sprite Editor 패키지가 없어 DataProvider 네임스페이스 컴파일 실패 → 기본 TextureImporter SpriteMetaData 사용.
- 동명 CurtainCallButton 네임스페이스 충돌 → ScheduleV1 타입 명시.
- 테스트의 이전 API 이름 UnlockTrick → 현재 CampaignRun.DiscoverTrick으로 수정.
- 초기 공연이 기본 편성에 따라 인간 대포였음 → 테스트 준비에서 명시적으로 줄타기 선택 후 슬롯 변경 기대값 검사.
- 첫 캡처의 공연 순번 텍스트 높이 부족 → 42px로 확보하고 isTextTruncated 검사 추가. 포커스 중 음성 개발 IMGUI 겹침도 억제.
- 기존 난이도 EditMode 5건이 첫날 튜토리얼 완료 신호 누락으로 실패 → 테스트 준비에 현재 가이드 완료 API 호출을 추가, 게임 규칙 변경 없이 5/5 통과. 최초 결과는 difficulty-before-fixture-update.json 보존.

## 산출물/환경
- private 키트: Assets/_ThirdParty/curtaincall_schedule_v2 (GPT Image 원본5개, sliced Sprite15개, 승인 원화, 프롬프트, 카탈로그). Unity 패키지는 `export-kit.cs`로 로컬 재생성한다.
- 새 프리팹: Assets/Resources/Prefabs/UIs/Roles/Retained/ScheduleUi/SchedulePanelV2.prefab. 공용 대기실 씬·툴팁 참조 교체. v1 보존.
- 전용 폰트 사용. 기존 미저장 폰트/escape-labels/private EscapeResultsKit 폰트 변경은 커밋/되돌림하지 않았다.
- PlayMode 종료 후 원래 Start 씬(미저장 변경 없음)으로 복귀. 기존 auto-tick은 이미 enabled였으며 그대로 유지.
- 사용자 요청 없는 commit/push/PR 없음.
