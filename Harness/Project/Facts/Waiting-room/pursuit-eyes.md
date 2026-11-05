---
summary: 추격자 머리 위 로컬 눈의 상태·범위·색/발광·리소스·검증 범위를 확인할 때
status: active
updated: 2026-11-05
source: Task-20261104-101 실제 렌더/검증, Task-20261103-099 사용자 직접 추격 표시 요청 및 코드 수정(Unity 재검증 대기)
---

# 추격자 로컬 눈

## 언제 읽나
추격자 인식 상태 눈, 색/발광, 자기 플레이어 표시 범위 또는 머리 위치를 수정할 때.

## 연결
- `PursuitDirector.Awake`가 Play 중 `PursuitEyeFeedback`을 자동 연결. 개별 프리팹/씬 수정 불필요. 눈은 Director 자식의 로컬 SpriteRenderer이며 네트워크 메시지 추가 없음.
- `PursuitFeedback.LocalObserver`를 화면 테두리와 공유. NetworkPlayer.Local의 Normal 상태만 사용하고 관전 대상은 사용하지 않음. 오프라인은 PlayerInputReader. 포획·씬 전환·비활성 시 즉시 숨김.
- 반경은 현재 단계 SightRadius/WalkHearingRadius/RunHearingRadius 최댓값, 3D 거리. 방 제한은 적용하되 순찰 이탈 제한으로 숨기지 않음(정상 시야는 복귀를 중단할 수 있음). AlwaysNearest여도 눈 반경은 유한함. 범위 이탈은 0.5초 페이드.
- Patrol/Return은 흰 감은 눈. Listen/Investigate/Search는 노란 뜬 눈. 자신을 대상으로 한 Prepare/Chase/Capture는 빨강. 다른 단원 대상의 Prepare/Chase는 일반으로 표시.
- `PursuitEyeTransition`이 8프레임 열림/역순 닫힘·색/발광 전환과 중간 반전을 처리. 프레임/알파/색은 동기화하지 않음.
- 자기 대상 Prepare/Chase/Capture는 이전 흰색/숨김/노랑 상태와 관계없이 첫 Tick(delta=0 포함)에 Alpha/Openness=1, ChaseColor/ChaseEmission 즉시 적용. 바닥 CueState도 동일 Resolve를 사용하며 색은10Hz 지면/벽 투영을 기다리지 않고 즉시 갱신. 일반↔경계/닫힘/이탈은 기존 시간 유지. 2026-11-05 후속 독립 눈/상태9조건 통과, Unity 회귀·실제 화면은 미검증.

## 에셋·설정
- `Assets/Scripts/WaitingRoom/Pursuit/Resources/PursuitEye/Frames/Eye00..07.png`: 순백 RGB+알파, 256×256, Single/FullRect, PPU256, 비압축, mipmap 없음. Resources의 전용 URP unlit 셰이더는 깊이 검사·premultiplied alpha·HDR 밝기와 약한 번짐을 사용.
- WaitingRoomSettings > Pursuit > Eyes, 또는 Tools/Curtain Call/Pursuit/Mannequin Designer > 전역 단계 설정 > 머리 위 인식 상태.
- 설정12개는 InspectorName/Tooltip으로 한글 표시. FadeSeconds=표시·숨김 시간(전체 투명도), TransitionSeconds=눈 열림·상태 전환 시간(일반/경계 진입 시 모양·색·발광). 빨간 공격 진입은 두 시간을 무시하고 즉시 적용. Normal/Alert/ChaseEmission=일반/경계/공격 추가 밝기, 0도 기본 색상으로 보임. 셰이더의 눈 중심 밝기는 색×(1+추가 밝기)이며 주변 조명은 아님. GlowOpacity=눈 주변 번짐 강도(추가 밝기0이면 번짐도0). 이름만 번역하므로 저장 필드/기존 값은 유지.
- 기본 실제 그림 너비 .18m, Head 뼈 offset .42m, fade/transition .5초. 초기 .28m는 머리에 가려져 실제 렌더 후 보정. 색/발광/번짐/위치/크기는 실행 중 설정값을 읽음.
- 원본 GPT/알파 후처리 자료와 안내: `Tasks/Active/Task-20261104-101-pursuit-eye-art/Artifacts/`, `usage.md`. 재임포트는 Tools/Curtain Call/Pursuit/Import Eye Sprites. 원본은 Task 폴더에 보존.

## 검증과 한계
- EditMode 5/5: 로컬 거리, 대상, 전환/중단, 동적 설정, 실제 sprite/shader 임포트.
- 실제 대기실 LAN 호스트 PlayMode 1/1: 비활성 숨김·일반/준비/추격/수색·타인 대상·이탈/재진입·정리. Game View 미활성 시 기존 커튼 렌더 장벽에서 실패하므로 Game View 활성 후 실행.
- 실제 프리팹·셰이더의 일반/경계/추격 렌더 `eye-unity-state-review.png` 확인. 2026-11-04 사용자가 최종 설정 조정·전체 검증 완료를 확인했고 추가 검증 없이 저장/제출을 요청함. 에이전트의 다인 원격 실기는 별도로 수행하지 않음.
