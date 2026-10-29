# 대기실 타이머 v2.8 검증

- 기준: 2026-10-29 사용자 디자인 패키지·D-3→D-0 유지 확정. 브랜치 fix/waiting-room-resttimer.
- 프리팹: Assets/Resources/Prefabs/UIs/RestTimerV28/W1_RestTimer_v2_8.prefab
- 씬: Assets/Scenes/UI/CommonWaitingRoomUI.unity
- 연결: CommonWaitingRoomUI.RestTimerDisplay → 공개 RestTimerView → private RestTimerV28.RestTimerView.

## 완료
- 프리팹 생성·컴파일·직렬화 참조·기존 시계 2종 비활성화·입력 투과·MissingScript0.
- 제한 시간 비율, 0초/무제한/미수신 구분, D-3~D-0, 아침/낮/밤, 정지·재개/새 구간, 공연·수면 숨김.
- EditMode 20/20: 타이머14 + 공용 배치3 + 알림 텍스트 배치3.
- PlayMode 3/3: 대기실 전체 HUD·재등록/알림2 + 실제 소집 방송1. 실제 LAN 2클라이언트 확인은 아님.
- 실제 대기실 낮/밤 캡처 확인. 타이머는 기존 좌상단으로 복원했고 중앙 배너와 겹치지 않음.
- 1920×1080·1920×1200에서 정상/30/10/0초/무제한/일시정지/미수신의 14 상세 캡처 확인. 격리 PreviewScene+ScreenSpaceCamera+CanvasScaler(.5), 게임 배경/입력 검증과 구분.
- 기존 공유 폰트를 수정하지 않고 글리프를 확인해 재사용. 사용자 별도 TMP Fallback/CheerGauge Bold 변경은 보존.

## 로컬 근거
- .harness/local/resttimer-edit-results.json
- .harness/local/resttimer-layout-edit-results.json
- .harness/local/resttimer-notification-edit-results.json
- .harness/local/resttimer-play-results.json
- .harness/local/resttimer-warning-play-results.json
- .harness/local/resttimer-prefab-audit.json
- .harness/local/resttimer-day.png / resttimer-night.png
- .harness/local/resttimer-1920x1080-*-detail.png / resttimer-1920x1200-*-detail.png

## 남은 확인
- 사람 최종 외형·실제 두 PC에서 동일 버전/설정의 표시 동기화.
- 사용자 저장 요청 전이므로 commit/push 없음. 기존 private main 선반영 push 훅은 우회하지 않는다.

## PR 제출 전 최신 dev 확인
- origin/dev를 b47035ab로 작업 브랜치에 병합 후 컴파일 통과.
- 대기실 HUD PlayMode 2/2와 외줄/인간 대포 UI·정산 PlayMode 1/1 재확인.
- private 에셋 PR #8 검사 SUCCESS 및 사용자 승인 Merge commit으로 main 반영 완료. gitlink 80ff0ad7 ancestry 검사 통과.
- 폴더 정리의 이동/삭제는 아직 실행하지 않았으며 dev PR에서 제외한다.
