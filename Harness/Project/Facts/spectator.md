---
summary: C11 관전·사망 표시 API·좌우 비네트·하단 상태판·독립 확인 씬과 원본 보존
status: active
updated: 2026-10-20
source: Task-20261019-061
---

# C11 관전·사망 UI

## 언제 읽나
- C11 표시를 배치·호출·검증하거나 하단 슬롯 연결을 계획할 때.

## 경로·소유
- 제공 원본: private `Assets/_ThirdParty/curtaincall_spectator_v1/`. Runtime/Builder 원위치 재사용·namespace 격리, PNG/제작 자료/라이선스/GUID 보존.
- 현재 독립 조립 구역: `Assets/Scripts/Systems/Spectator/`, `Assets/Scenes/Tests/Spectator/`, `Assets/Resources/Prefabs/UIs/Spectator/`, `Assets/Resources/Fonts/Spectator/`.
- 정본 `Assets/Resources/Prefabs/UIs/Spectator/Spectator.prefab`(Canvas 전체), 확인 씬 `Assets/Scenes/Tests/Spectator/Spectator.unity`, 전용 Static Medium8192. 2026-10-20 Schedule 타입 충돌 해소·폰트 재사용 안전복구 후 생성, 비활성 Preview 콜백 시간 재정지 방지 가드까지 보강 후 신규 PlayMode26/26 통과. 사람 최종 외형/실게임 검증과 구분한다.
- 조립 API `CurtainCall.Spectator.Editor.SpectatorProjectBuilder.Build()`는 기존 출력 보호. `ResumeAfterFont()`는 온전한 전용 Static폰트/원본 GUID·글리프·빈 출력만 확인해 재사용하고 idle update 틱에서 일회 조립한다. 기존 정본 재생성/덮어쓰기 용도가 아니다.
- 진행/실행 결과: `Tasks/Active/Task-20261019-061-cheer-gauge-ui/spectator-verification.md`. 제공 문서의 `not_run`은 배포 시 정적 검토 기록이며 프로젝트의 실행 검증과 구분한다.

## 제공 계약
- 신규 비네트2 RGBA3840×2160. 중앙60%/반대쪽 alpha0, 전체 Stretch/Simple/PreserveAspect false·PPU200/max4096/비압축. 픽셀만 약63.3MiB이며 실제 플랫폼 메모리 측정값은 아니다.
- 기존 smallplate4/키캡2(PerformanceHud), 눈/해골2(CommonIcons) PNG는 정본 읽기 전용 참조. 신규 importer만 보정한다.
- Image11/TMP4. 비네트는 `00_EdgeEffects`, 하단 상태판은 `20_ContextHUD`. cross reference를 포함해 Canvas 전체를 함께 보관한다.
- 판360×64·하단 중앙(0,160), 키40×40/문자영역32×32, 상태18/대상24/키24. Medium Static, 한 줄 Ellipsis/원문 보존·RichText/제어문자 파싱 끔.
- Hidden/Dead/Spectating/NoTarget, 비네트강도0/.70/.40/.40은 제공 검토값. alpha-only unscaled120/100ms, 반복 요청 비재시작·현재값에서 반전한다.
- 관전/전환가능/양쪽 키명이 존재하며 실제 TMP 폭32 이내일 때만 키 둘 표시. 긴 키는 둘 다 억제하고 flag로 알린다. Q/E는 데모이며 게임 바인딩이 아니다.
- `OccupiesBottomSlot`은 퇴장 완료까지 true. C11>P1>W6>C7 우선순위는 외부 호출자 계약이며 C9는 별도 슬롯이다.

## 경계
- 사망 판정·관전 대상/카메라·네트워크·입력 바인딩·자동 HUD 중재·timeScale 변경은 C11 런타임 역할이 아니다.
- 사람 최종 외형/손맛·공동/실기기·플랫폼/Atlas·실게임 연결은 독립 자동 검증으로 대체하지 않는다.
- Preview 버튼 콜백은 비활성 컴포넌트에도 호출될 수 있으므로 Run의 isActiveAndEnabled 가드를 유지한다. OnDisable 시간 복구 뒤 다시 정지되는 경로를 전용 회귀로 막는다.
- 실행 UI 합성 입력 검증은 별도 가상 Mouse/Keyboard로 물리 장치 이벤트와 분리한다. InputManager는 HideAndDontSave 임시 설정 교체 시 기존 객체를 파괴하므로 InstanceID만으로 원복을 보장할 수 없다. 설정 값 보존·원래 정책 복구·QA 장치 정리를 검증하며 ProjectSettings 저장은 하지 않는다.
