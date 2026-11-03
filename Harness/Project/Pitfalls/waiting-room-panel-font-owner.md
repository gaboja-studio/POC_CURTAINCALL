---
summary: 씬 소유 임시 폰트를 쓰는 영속 퀘스트 패널은 파괴된 Unity 소유자와 null을 구분해 다시 만들어야 한다
status: active
updated: 2026-11-03
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
