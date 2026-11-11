---
summary: 공통 GameSettings 정본과 기본 점프·외줄·봉·트램펄린·부상의 우회 적용 경로
status: active
updated: 2026-11-10
source: origin/dev 2d591802 조사 및 refactor/common-game-settings 작업 중 변경, Task-20261109-114
---

# 공통 설정 적용 경로

## 언제 읽나

GameSettings 값을 변경해도 특정 씬에 반영되지 않거나 씬 공통 동작을 통일할 때.

## 확인된 사실

- 정본은 `Assets/Resources/Settings/GameSettings/GameSettings.asset`, 접근 코드는 `Assets/Scripts/Systems/Settings/GameSettings.cs`다. Base·Tightrope·HumanCannon·Poles·Trampoline·WaitingRoom·Exploration·EscapeRoute를 연결한다.
- 사진의 `Tricks/TightropeSettings.asset`은 전체 설정의 외줄 묶음이다. 기본 이동·점프의 원본은 `BaseGameSettings.asset`이다.
- 일반 이동은 `PlayerMover`에서 Base의 FreeMoveSpeed를 읽는다. 저장값은 걷기 3, 달리기 배율 2, 앉기 배율 0.5다.
- Task-114 반영: 일반 바닥 점프 getter는 Base 저장값(현재 0.8)을 직접 반환한다. 이전에는 활성 씬의 `HumanCannonSystem`이 등록한 HumanCannon.JumpHeight 1.2가 Base getter를 덮어썼다.
- 후속 외줄 통일 요청으로 `TightropeSettings.RopeMovement.JumpHeight`는 독립 공통값 1.5m다. `CharacterJumpHeightOverrides`와 HumanCannonSettings.jumpHeight는 제거했다. 부상·목마 배율은 기존 정책을 유지한다.
- `TightropeCourse.Settings`는 씬 배치에 sceneShape를 사용하지만, 줄 위 판정 폭 0.55m와 발 높이 허용 0.3m는 GameSettings.Tightrope.RopeMovement에서 직접 읽는다. Course/sceneShape에는 해당 저장 필드를 두지 않는다. 생성 줄과 씬 줄 모두 같은 모서리 접촉 판정을 사용한다.
- 탈출로 A의 저장된 줄 충돌체 폭은 0.55~1.85m지만 런타임에는 보행 폭만 공통값으로 맞추고 비활성화 시 복원한다. 메시·길이·위치는 유지한다. 저장된 YAML 폭만 보고 실제 보행 폭으로 해석하지 않는다.
- 모든 옆줄 이동은 같은 높이에서 수평 최대 3m. 1mm 수치 오차만 허용하며, 발의 높이 허용 0.3m를 줄 사이 높이 허용값으로 사용하지 않는다. 입력 방향의 최근접 사용 가능한 줄로 착지하고 궤적을 고정한다. 비활성·트리거·불탄/끊긴 줄은 이동 대상이 아니다.
- `Common/TurnAround` 기본키 T로 외줄 진행 방향을 180도 전환한다. 시점 회전은 외줄 입력 방향을 반전하지 않는다. 일반 바닥의 시점 기준 이동은 유지한다.
- 외줄 이동은 W/S 양방향 입력을 처리하고 양끝에서 진입할 수 있다. legacy `bidirectionalSceneTraversal` 필드는 제거했다. 2026-11-10 보완: 대기실의 도착 벽과 공중그네 학습 완료 시 도착대 복귀는 튜토리얼만 적용한다. 재연습에서는 도착 후에도 왕복하며 다음 구간은 기존 과반 '다음' 투표로 이동한다. 씬별 이동 수치나 별도 GameSettings 예외 옵션을 추가하지 않았다.
- 목마는 코스가 없는 씬에도 서비스가 생성되고 코스 비활성/재활성에도 연결된다. F 탑승/F 홀드 하차, 일반 바닥은 균형 없음, 외줄에서 바닥에 내려온 스택만 자동 안전 하차한다. 상세 기준은 [외줄 비교와 반영](../../../../Tasks/Active/Task-20261109-114-common-game-settings/tightrope-unification-plan.md).
- 2026-11-10 봉 통일: GameSettings.Pole 및 HumanCannonPoleSystem으로 운동·점유·비행을 공유한다. 외줄 Trapeze/Swing/Relay는 코스 연결 어댑터다. 공통값을 덮던 옛 씬 필드·제작 도구 참조·저장값은 제거했다. 모든 봉 2인 위아래 연결·상단 입력·30초·80°·180°/초.
- 트램펄린은 GameSettings.Trampoline의 상승 12m/s·운반 4.5m/s·공중 조절 3m/s를 사용한다. 모든 씬에서 운반자 없이 바닥 반동이 가능하다.
- 부상 이동은 Base의 1/.8/.5, 팔 손실 접근은 .8이다. 인간대포의 최소 이동 80% 및 별도 상호작용 지연을 제거했다. 일반 근접 거리 2.5m·점프 버퍼 .15초도 Base를 사용한다.
- 일반 지지면 착지는 WalkableSupport를 사용한다. 2026-11-11 사용자 변경으로 5m 미만 무피해, 5m 이상 15m 미만 2개 절단, 15m 이상 사망이다(BaseGameSettings.BodyDamage). 모든 씬에서 캐릭터가 월드 Y −15m(BodyDamage.WorldDeathY) 이하로 내려가면 착지와 관계없이 사망한다(NetworkPlayer.CheckWorldDeath, CommonDamageRules.IsBelowWorldDeathY). 실제 사용 가능한 외줄 지지면 착지는 Base.BodyDamage.SafeRopeLanding으로 높이 피해를 면제한다. 외줄을 놓쳐 바닥에 닿으면 실제 낙하 높이를 쓰며 스프링 신발은 피해 착지에서 먼저 반동한다.
- 기존 `Decisions/game-settings.md`의 2026-10-30 결정은 기존 공통 조정값의 씬별 우회 배율·중복 설정을 만들지 않도록 명시한다.
- 추격자의 시야·이동·연출은 WaitingRoomSettings 안에 연결되어 있다. 봉·트램펄린의 새 공통 에셋은 GameSettings가 참조한다.
- ExplorationSettings는 시험 항목 1개, EscapeRouteSettings는 시험 항목·출구 문 각도·시간만 제공한다. 실제 탐색/탈출 기능의 별도 값은 Task-114의 [전체 목록](../../../../Tasks/Active/Task-20261109-114-common-game-settings/settings-inventory.md)에 정리했다.

## 한계

- 최종 구현 검사·남은 실패는 Task-114 [후속 검증 기록](../../../../Tasks/Active/Task-20261109-114-common-game-settings/followup-verification.md)을 따른다. 실제 두 PC·모든 씬 수동 완주는 미검증이다.
- 바닥 기준 높이 통일은 기존 1.2m를 전제로 한 장애물 통과에 영향을 줄 수 있다. 일반 대포 코스 지상 톱 상단 0.42m는 두 다리 손실 시 공통 점프 0.4m보다 높다. 기물 수치·배치는 이 변경 범위에 포함하지 않았다.
- 레벨 배치와 기믹 연결은 씬별로 달라도 된다. 공통 행동 수치와 판정이 분산된 부분을 우선 정리한다.

## 후속 공통화 (2026-11-10)
- 위험물 접촉 시작 시 공통 1초 피해 간격. 즉사형 화염/압사는 이 절단 유예로 막지 않는다.
- 근접 상호작용은 실제 몸 중심에서 접점까지 3차원 2.5m와 팔 부상 배율. 고체 벽·층 차단, 대상 자체·플레이어 몸은 가림에서 제외한다.
- F는 사용 중인 대상 → 구조 → 거리순 사용 가능 대상 중 하나로 고정한다. 같은 누름의 처리 상태를 공유한다.
- 추락 매달리기는 발에서 실제 사용 가능한 줄까지 1.5m, 가림 없음·빈자리. 줄 번호/생성 방식과 무관하다. 실패/만료는 일반 낙하. 같은 높이 수평 3m 옆줄 이동과 별개다.
- 일반 카메라 벽 충돌은 모든 씬에서 사용한다. 일반 불기둥/분사 화염은 절단, 추격 화염벽은 즉사로 타입을 구분한다.
- `CommonSettingsValidation`: 폐기 필드 재도입, 제작 도구 참조, 씬/프리팹의 옛 저장값, GameSettings 8개 연결·봉 외형 2개 누락을 검사한다. 빌드와 check-work 컴파일 단계에 연결했다.
- 상세 제거 항목은 Task-114 `Inventory/retired-settings.json`. 공통 규칙은 GameSettings에서 바꾸고 씬 어댑터에는 배치·장치 연결만 둔다.
- Inspector 확장: PoleSettings의 capacity(1~4), 상단 단독 반동·아래 사람을 둔 놓기 제한, Base의 위험물/낙하 절단 개수·구조 우선 선택, Tightrope의 점프 중력 배율. 기본값은 기존 규칙과 같으며 capacity는 봉 생성 시 적용한다.
- 공통 설정 Inspector의 한글 이름과 Tooltip은 기존 KoreanRewardSettingsEditor 표시 경로 및 중첩 설정 전용 Drawer를 사용한다. InspectorName만 붙여서는 일반 필드 이름이 한글로 바뀌지 않는다.
- 최초 튜토리얼 바닥 복귀·실내 인원에 따른 문 재개방·복귀 대기·도착 벽은 WaitingRoomSettings.Tutorial에서 조정한다. 최초 교육 활성 여부로 제한하며 첫 공연 후 외줄·인간대포 재연습에는 무료 복구를 적용하지 않는다. 재연습 종료는 사망·사지 상태를 유지하고 위치만 반환한다.
- 봉의 종류별 표시 크기는 PoleSettings의 regularBarWidth/switchBarWidth(각1.8m). 외형 루트를 배치 부모와 분리해 같은 씬에 두며, 케이블 높이·물리 반동은 너비 조정과 독립이다.
- BaseGameSettings.BodyDamage.SafeLandingZones는 활성 HumanCannonLandingZone의 실제 윗면 착지 높이 피해를 면제한다. 일반 낙하·봉·대포 공통, 기존 부상 유지. 일반 바닥은 5m 부상·15m 사망이며 최초 튜토리얼 아래 바닥 복귀는 우선한다.
- 운반 기물 회수 정책·톱 배치 높이·UI 외형은 이 후속 공통화 범위 밖이다.
