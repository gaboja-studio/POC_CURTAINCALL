# 상호작용 창과 마우스 커서
2026-10-21 · 상점/스케줄 창의 기존 UIInputBlocker → InputGate 요청을 공통 CameraViewDirector가 직접 읽는다.

## 동작
- 창을 열면 커서 잠금이 풀리고 커서가 보이며, 캐릭터 입력과 마우스 시선 회전이 차단된다.
- ESC 또는 닫기 버튼으로 마지막 창을 닫으면 입력 해제 대기 후 마우스 시선 조작과 커서 숨김/잠금이 복귀한다. 창을 닫는 ESC는 수동 커서 해제로 중복 처리하지 않는다.
- 여러 창 중 하나만 닫으면 나머지 창의 요청이 유지되어 커서가 계속 보인다. 패널 비활성화/파괴도 요청을 해제한다.
- 창 밖에서 ESC로 커서를 해제하는 기존 동작과 앱 포커스 상실 시 해제는 유지한다. 고정 카메라에서는 UI를 열 때 커서를 표시하고 기존 시점의 커서 정책을 따른다.

## 향후 Canvas UI 연결
- 열릴 때 활성화하는 패널 오브젝트에 `UIInputBlocker`를 붙이고 `BlockWhileActive`를 켠다. 패널을 비활성화하면 자동 해제된다.
- 오브젝트를 계속 활성화하고 CanvasGroup 등으로 숨긴다면 `BlockWhileActive=false`로 두고 열기/닫기 코드에서 `SetOpen(true/false)`를 호출한다. CanvasGroup의 alpha만 바꾸면 입력 요청은 해제되지 않는다.
- 항상 보이는 HUD 전체 Canvas에는 붙이지 않는다. 창별로 연결하고 별도 코드에서 Cursor를 덮어쓰지 않는다. 버튼의 EventSystem/GraphicRaycaster 연결은 별도로 필요하다.

## 수정 파일과 검증
- 동작: `Assets/Scripts/Systems/Camera/CameraViewDirector.cs`.
- 회귀: `Assets/Scripts/UI/SceneIntegration/Tests/PlayMode/DevStartLauncherPlayTests.cs`의 `WaitingRoomPanels_AutomaticallyReleaseAndRestoreCursor`.
- 수정 전 실제 대기실 LAN 호스트 테스트에서 상점 열기 후 커서 숨김을 재현(1/1 실패). 수정 후 같은 테스트 1/1 통과. 상점/스케줄 각각 열기, 합성 ESC, 닫기 API, 중첩 차단기/파괴, 창 밖 ESC를 검사한다. 실제 버튼 클릭/실물 키보드 검증을 대신하지 않는다.
- UI PlayMode 30/30 통과. 카메라 회귀는 변경 전/후 모두 15/17 통과, 동일 2개 실패: 앉기 모델 크기(현 Player.prefab은 squashWhenCrouched=0), 구형 proto 외줄의 고정 3인칭 기대(현 공급자는 상황에 따라 1인칭). 이번 커서 변경에 따른 신규 실패는 없으며 해당 기존 테스트/기능은 수정하지 않았다.
- 실제 마우스 버튼 클릭, 앱 포커스 왕복, Mac 및 다인 플레이·빌드는 미실행. 전체 관련 검증 기록은 현재 Task handoff 참고.

## Unity 사용자 확인
1. Dev에서 2일차 아침 대기실에 진입한다. 수화기 근처에서 F를 짧게 눌렀다 떼면 ESC 없이 바로 커서가 표시되어야 한다.
2. 창 안에서 마우스를 움직이고 상품 버튼을 누른다. 카메라가 회전하거나 커서가 다시 잠기면 안 된다. ESC/닫기로 종료하면 커서가 숨겨지고 게임 조작이 돌아와야 한다.
3. 스케줄표에서도 열기/수정/ESC를 반복한다. 창을 연 채 다른 앱으로 이동 후 돌아왔을 때도 커서가 보여야 한다.
