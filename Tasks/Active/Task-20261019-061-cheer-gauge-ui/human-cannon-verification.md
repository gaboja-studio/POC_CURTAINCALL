# 인간대포 후속 검증
2026-10-20 / Unity6000.3.25f1 / feat/ui-scene-integration.

## 검사
- `verify-unity -Compile -RunTests -Mode PlayMode -Filter CurtainCall.UI.SceneIntegration.Tests`: 최종 PASS12/12, skipped0. `.harness/local/human-cannon-tests.json`.
- 기존9개 + 툴팁 경계3개. 공통 씬 lifecycle 테스트에 C7/C15 존재·입력 비차단·이전 소유자 해제·폰트 전체 문구 검사를 추가했다.
- 최초 테스트11/12: TMP 복사본 파괴 시 공유 atlas/material 삭제 시도. 빌린 참조를 분리하고 정본 C1/C2/C3는 복사 대상에서 제외하여 수정. 최종 unload 테스트와 Play 정지에서 재발 없음.
- 최종 Console groundTruth: compilationFailed false, errors0/warnings0. 예전 실패의 이력 로그는 남아 있으며 최종 실행 오류로 세지 않았다.
- `verify-fast`: PASS, 기존 문서 길이 WARN9·private 변경219건 안내.
- `check-work`: scope279 FAIL(이미 합친 dev 포함), 후속 SKIPPED. `.harness/local/human-cannon-check-work.txt`.
- 별도 `verify-scope -Base HEAD`: 작업 시작 전부터 변경된 Harness 정책/결정3개 때문에 FAIL3. 이를 삭제/예외처리하지 않았다. 이번 수정 경로는 Task 작업 구역과 대조했다.
- `git diff --check`: PASS. 원본 UI 프리팹/9-slice/폰트 diff 없음. 기존 TightropeCourse 변경은 보존.

## 에디터 호스트 실행 / 합성 조작
- HumanCannon 단독 Play, LAN 호스트7791. 실제 프레임 진행 확인(626→2000 등), CommonPerformanceUI additive1개.
- 기존 API로 StartGame→EndIntro→첫 공연 보상 종료→무대 집결을 요청해 HumanCannon 공연 상태 생성. AddCheer 성공 → 호응10, FirstSectionFire.Arm → C2 2:25. C3는1인 —, P1없음.
- 기존 Board/Cancel 요청으로 대포 탑승/하차: C15 대포 `[F] 타기`, 탑승 시 C15숨김·C7 `[F] 발사` 확인.
- InputGate.SetBlocking 시험: guide/tooltip/section 모두 false, 해제 후 guide true 복원.
- 트램펄린 위치로 PlayerMover.SetStartPose 후 RequestGrab 리플렉션 호출: carrier0=local0, `[F 길게] 트램펄린 놓기`, 글자 누락/overflow 없음. 자동 운송 완료 후 안내 표시 확인.
- 런타임 Fallen 상태 관측 시 guide/tooltip false. 별도 추락 요청 eval은 enum namespace 오류로 실패했으므로 그 요청 성공이나 사망 원인을 주장하지 않는다.
- 1920×1080 화면: `.harness/local/human-cannon-approach.png`, `human-cannon-carry.png`. 캡처 때 일부 개발용 패널만 런타임 비활성화, 씬 저장 없음.
- section의 TMP 초기 생성 기본폰트 참조를 명시적 직렬화로 수정하고 화면 재확인. 정본 정적 폰트와 게임폰트의 부족 글자는 Assets 기존 TTF 기반 실행 전용 fallback으로 보완.

## 미실행 / 마무리
- 실제 키보드 F 길게/Space 조작, 봉 접근/놓기 실제 플레이, Start부터 전체 코스, 불 접촉 판정 원인, 다인2~4인·재접속·다른 해상도: 미실행.
- 입력 API/리플렉션 시험은 합성 상태 검증이며 실제 사용자 입력 PASS로 간주하지 않는다.
- Play 정지, HumanCannon 씬 dirty0, runInBackground=false 복원. Time.timeScale 변경 없음.
- 동적 C1 폰트의 실행 캐시가 디스크에 기록돼 백업 후 원복했다. 변경 전 status에는 없던 파일이며 timestamp08:47:08 UTC·숫자0 glyph 추가를 확인. 자동 승인 최초 거부 후 이 증거와 동일 SHA256 백업을 제시해 원복 승인·완료. 재import 뒤 diff0.
