---
summary: 씬 소유 임시 폰트를 쓰는 영속 퀘스트 패널은 파괴된 Unity 소유자와 null을 구분해 다시 만들어야 한다
status: active
updated: 2026-11-05
source: Task-20261102-097-rehearsal-flow
---

# 씬 전환 뒤 퀘스트 폰트 삭제

## 언제 읽나
공연에서 대기실로 복귀한 뒤 퀘스트가 비거나 Visible과 실제 화면이 다를 때.

## 원인
- WaitingRoomRightPanel은 DontDestroyOnLoad 소유자 아래 있고, 폰트는 공용 UI 씬의 UiRuntimeFontFallback 복제본을 쓴다.
- 공용 UI를 해제하면 복제 폰트도 삭제된다. 파괴된 Unity 오브젝트와 null은 `==` 비교가 같으므로 `current != fontOwner`가 변경을 놓칠 수 있다.
- 실제 재현: 루트·TooltipView 활성, 캐릭터 Normal, 커튼 해제, 씬 전환 종료인데 두 TMP label.font가 NULL이고 ShowPanel이 실패.

## 대처·검증
- 소유자 교체는 ReferenceEquals로 관리 참조를 비교하고 패널을 재생성한다.
- 대기실 퀘스트는 대기실 UI의 폰트 소유자만 사용한다. 공연 퀘스트를 추가하는 해결책은 사용자 정정으로 철회했다.
- 같은 문구의 Visible만 믿지 않고 전환 뒤 실제 TooltipView.IsVisible과 TMP font를 확인한다.
- 커튼이 아직 시작되기 전의 IsActive=false를 완료로 오인하지 않는다. SpaceLink.IsTransitioning 종료와 해당 퀘스트 표시까지 기다린다.
- 수정 후 공연 패널 실제 폰트/표시 확인, 공연 사이→둘째 공연→밤→침대→Day 2 연결 1/1 PASS(87.2초). 이전 커튼 대기의 조기 false도 상태 추적으로 재현했다.

## 월드 스케줄표 수명·패널 쌓임 (2026-11-04)
- UI 원본을 씬 해제 중 다른 부모로 옮기면 Unity가 SetParent를 거절한다. 스케줄표는 구성된 뷰/버튼 사본만 판 아래에 만들고 원본 어댑터는 공용 UI 수명을 따른다.
- 월드 판은 닫아도 보여야 하므로 View.IsVisible을 모달 열림 판정에 쓰면 다음 날/데모 전환을 영구 차단한다. 실제 ScheduleBoardView.IsOpen/IsEditing을 읽는다.
- 퀘스트 패널은 실제 PresentationSizeUi와 8 UI 단위 간격으로 쌓는다. 항상 느낌표를 붙이지 않고 둘 이상일 때 마지막 추가 항목만 표시한다.
- `[tutorial]` 제목은 조건 달성 전까지 유지되는 퀘스트 패널의 식별자다. 지정 시간 후 사라지는 설명 패널에는 붙이지 않는다.

## 원본 캔버스 밖의 폰트 보완 (2026-11-05)
- 공연 C7/C15 캔버스를 그대로 복사해도 CommonPerformanceUI 루트의 `UiRuntimeFontFallback`이 빠지면 한글 일부가 네모로 나온다. 뷰의 HasRuntimePresentation=true와 정적 폰트 참조 일치만으로 실제 글리프 표시를 보장하지 않는다.
- 원본 폰트 보완 컴포넌트와 fallback/mediumSource/semiBoldSource 참조도 같은 연습 코스 수명 안에 복사한다. 새 정적 폰트 에셋을 만드는 방식으로 덮지 않는다.
- 실제 문구의 Hangul HasCharacter(searchFallbacks/tryAddCharacter), TMP textElement의 원래 Unicode, UI 포함 플레이어 캡처로 확인한다. 이 폰트 소유자는 코스 종료와 함께 정리하고 영속 퀘스트는 기존 대기실 폰트 소유자를 계속 쓴다.
