---
summary: C16 장소 전환 커튼 독립 프리팹·표시 API·차폐 렌더 장벽과 원본 보존
status: active
updated: 2026-10-20
source: Task-20261019-061
---

# C16 장소 전환 커튼

## 언제 읽나
- 커튼을 배치·호출하거나 실제 전환 관리자 연결을 계획할 때.

## 경로·소유
- private 원본 `Assets/_ThirdParty/curtaincall_curtain_v1/`, Runtime `CurtainCall.UI.TransitionCurtainView` 원위치 사용. PNG/자료/GUID 보존, 신규 천/술6 importer 및 제공 Builder의 안전 조립 진입점만 보정.
- 정본 `Assets/Resources/Prefabs/UIs/TransitionCurtain/TransitionCurtain.prefab`(Canvas 전체), 확인 씬 `Assets/Scenes/Tests/TransitionCurtain/TransitionCurtain.unity`.
- 공개 코드/검증 `Assets/Scripts/Systems/TransitionCurtain/`. `TransitionCurtainProjectBuilder.Build()`는 기존 출력 덮어쓰기 거절·dirty 씬/Play/import/PrefabStage 거절·additive 격리 조립. 기존 정본 재생성용이 아니다.
- 공용 Icons 별과 C11 Spectator Static Medium 읽기 전용 참조. 이 의존성 없이 독립 배포되지 않는다.
- 실행 결과·실패 이력 `Tasks/Active/Task-20261019-061-cheer-gauge-ui/curtain-verification.md`, 개별 경로·SHA 전수 목록은 로컬 비공개 검증자료로 보존하고 공개 저장에서 제외한다.

## 제공 표시 계약
- Image8/TMP1, 1920×1080/Match .5/PPU200. 천·술6 PNG Simple/FullRect·비압축4096·9-slice 없음. 원본 텍스처 픽셀 약90.72MiB는 산술치이며 Profiler 실측이 아니다.
- BodyStage 등비 cover, 좌우 leaf1040×1200·닫힘 x=-32/912,y=32. 원본 body 불투명 보장2016×2240px·닫힘64UI 불투명 겹침. Valance는 viewport 바로 아래 폭 기준 등비 확대해 ultrawide 상단 유지.
- 하단 로딩240×80/(0,112), 별32×32·그림자1UI 아래·동일 회전120°/s, Static Medium24·NoWrap/Ellipsis·literal text. 커튼은 입력을 차단하지 않는다.
- `Close()`/`Open()` 수락 여부 bool, `OpenImmediate()`는 이벤트 없는 정리. 반복 같은 목표는 재시작하지 않으며 현재 위치 반전, 450ms×남은 비율/unscaled/SmoothStep.
- `Covered`는 닫힘1 이후 WaitForEndOfFrame→다음 프레임 장벽 뒤 1회. 이 콜백에서 로드를 시작하고 외부 준비 완료 후 Open. 헤드리스/배치 Close 거절, 실패 시 이벤트 무기한 대기 금지.
- `IsFullyCovered`는 렌더 조건·opaque body가 유지될 때만 true. canvas/alpha/비활성 변경은 대기 취소·Open 정리. Opened/Covered 재진입 후 기존 상태 덮어쓰기 없음.

## 경계·검증 주의
- 실제 장소 로드·씬 유지·네트워크 준비·입력/커서/카메라/오디오·timeScale 변경·자동 영속화는 게임 관리자 책임. Preview 시간정지는 데모 한정이며 disable 시 원래 값 복구.
- RectMask2D 화면 끝 감쇠로 명암 배경이 가장자리1px에 섞였으므로 clip padding -2UI 적용. viewport/천의 좌표·크기는 바꾸지 않으며 실제 렌더 명암 픽셀 비교로 검증한다.
- TMP 가로 Ellipsis는 isTextOverflowing 플래그 대신 실제 visible U+2026/원문 보존 검사. `GetPreferredValues`는 characterInfo를 바꾸므로 메시 검사 후 측정하거나 ForceMeshUpdate로 다시 생성한다.
- 자동 검사/화면 캡처는 사람 최종 외형·손맛·공동/실기기·실게임 검증을 대체하지 않는다. reload both OFF 지원은 주장하지 않는다.
