# 추가 조사: 남은 공통 규칙 후보

> 2026-11-10 후속: 이 문서는 적용 전 조사다. 사용자 승인한 7항목의 현재 반영은 [변경 전후](followup-before-after.md)를 따른다. 운반 기물 회수 정책은 유지.
2026-11-10 · 사용자 요청은 추가 항목 조사. UI 제외, 게임 코드·씬·설정 변경 없음.
현재 작업 브랜치의 구현을 읽었다. 실제 플레이에서 증상을 재현한 결과와 구분한다.
추천은 확정 규칙이 아니며, 이미 통일한 이동·점프·봉·트램펄린 수치를 다시 미완료로 분류하지 않는다.

## 우선 통일 후보

### 1. 연속으로 다치는 간격
- 외줄 톱은 접촉 시작과 플레이어별 1초 간격을 모두 검사한다. 서로 다른 톱에 이어 닿아도 적용한다.
- 일반 대포 장애물은 호스트 접촉에서 프레임당 한 번, 원격 접촉 보고에서 0.1초를 사용한다. 계속 닿은 상태는 각 경로의 접촉 기록으로 막는다.
- 마지막 톱·관문 톱도 별도 접촉 기록을 사용하며, NetworkPlayer의 공통 절단 함수에는 피해 유예 시간이 없다.
- 추천: 같은 절단 위험물의 재피해 간격과 여러 위험물 동시 접촉 규칙을 플레이어 공통 판정으로 결정한다. 즉사 위험물의 취급은 따로 결정한다.
- 근거: `TightropeSaws.ServerTouch`, `Tricks/TightropeSettings.asset: hitCooldown=1`, `HumanCannonHazardCourse.Update/Contacts`, `HumanCannonFinalSaws.Contacts`, `HumanCannonSawGate`, `NetworkPlayer.ServerCanTakeDamage`.

### 2. 상호작용의 벽·층·거리 계산
- 봉은 잡는 경로의 벽을 검사하고, 탐색은 가림과 대상 높이를 검사한다.
- 탈출로 버튼·트램펄린 잡기는 몸 중심에서 대상까지 구형 거리만 검사하는 경로가 남아 있다. 실제 배치에서 벽 너머 동작을 재현한 것은 아니다.
- 탐색 기본 거리는 발 위치 기준 수평 거리와 별도 높이 제한이다. 버튼·기물은 몸 중심 기준 입체 거리이므로 같은 2.5m여도 범위 모양이 다르다.
- 추천: 일반 근접 행동의 거리 기준점·높이·벽 차단을 공통화한다. 선반 안의 물건, 원거리 UV 등 필요한 행동 차이는 명시한다.
- 근거: `WaitingRoom/Exploration/ExplorationReach.cs:16`, `Systems/Player/PlayerInteraction.cs:22`, `HumanCannonPole.cs:171`, `EscapeSwitchInteraction.cs:19`, `HumanCannonTrampoline.cs:196`.

### 3. F 한 번으로 선택되는 대상
- 플랫폼/대포 조작은 봉·목마·운반 기물 순서를 자체 처리한다. ShopReach는 자기 대상 중 가장 가까운 것을 별도로 고른다.
- 탈출로 협동 버튼은 별도 Update에서 같은 입력을 읽고 성공 후 ClearPendingInput을 호출한다. 모든 시스템이 공유하는 대상 우선순위는 없다.
- 따라서 입력 처리 순서와 대상 겹침을 검증해야 한다. 실제 한 번 입력에 두 행동이 발생했다고 단정하지 않는다.
- 추천: 잡은 대상 놓기, 동료, 기물, 버튼 등에서 한 번의 입력을 받을 대상 하나를 공통으로 선택한다. 구체 우선순위는 미정이다.
- 근거: `PlatformControlScheme.Tick`, `HumanCannonControlScheme.Tick`, `HumanCannonPilot.ReadInput`, `ShopReach.Pump`, `EscapeCooperativeLocks.Update`, `PlayerInputReader.ClearPendingInput`.

### 4. 외줄에서 떨어질 때 붙잡는 줄
- 독립 배치 줄은 현재 줄만 후보로 찾는다. 생성 코스는 공통 설정 laneReach=1을 사용하여 옆 한 칸도 후보로 찾는다.
- 가까운 곳에 이미 매달린 사람이 있거나 사용 가능한 줄이 없으면 실패한다. 실패 시 현재 코드는 사망 처리한다.
- 추천: 코스 제작 방식과 무관하게 같은 거리·방향·빈자리 기준으로 붙잡을 줄을 선택한다. 보행 중 옆줄 이동 3m를 구조 규칙으로 자동 전용하지 않는다.
- 근거: `TightropeHangSystem.ServerTryHang:134`, `TightropeHangBook.PickLane:55`, `TightropeSettings.HangRules`, 탈출로 `independentSceneRopes=1`.

### 5. 카메라의 벽 충돌
- 일반 3인칭 카메라를 벽 앞으로 당기는 처리는 대기실·escape_route 이름의 씬에만 적용한다. 관전 모드는 별도 경로다.
- 기본 시점 설정이 같아도 다른 씬에는 같은 벽 충돌 처리가 적용되지 않는다.
- 추천: 일반 플레이 카메라의 벽 차단 기준을 공통화한다. 연출·관전·기물 시점의 목적은 별도로 유지할 수 있다.
- 근거: `Systems/Camera/CameraViewDirector.cs:347`, `UsesWallCollision:359`, `CameraOcclusionFade.cs:130`.

## 게임 의도를 먼저 정할 후보

### 6. 아이템 효과가 적용되는 행동
- 스프링 신발은 현재 인간대포 비행 착지의 부상 판정에서만 반동한다. 연습 모드·안전 착지면에는 발동하지 않는다.
- 아이템 정의도 인간대포용이다. 일반 낙하에 적용되지 않는 것을 곧바로 누락/버그로 보지 않는다.
- 공통 바닥 안전 착지 적용 후, 걸을 수 있는 바닥은 부상 경로를 타지 않으므로 신발 발동 조건도 함께 검토해야 한다.
- 추천: 일반 낙하·외줄 낙하까지 사용할 공통 장비인지 정하고, 보류된 3m/5m 추락 규칙을 적용할 때 발동/소모 기준을 함께 검증한다.
- 근거: `HumanCannonSystem.Landing.cs:89,164`, `WaitingRoom/Items/ItemCatalog.cs:135`. 다른 장비 전체에 차이가 있다고 일반화하지 않는다.

### 7. 운반 기물을 놓거나 운반자가 사망했을 때
- 트램펄린 자발적 놓기는 현재 위치를 공유한다. 절단기는 바닥에 서 있지 않은 운반자가 놓으면 회수 지점/최초 위치로 복귀한다.
- 트램펄린 운반자 사망은 지면으로 낙하하고, 낭떠러지/지면 없음이면 생존자 근처 또는 최초 위치로 회수한다. 이 동작에는 이전 사용자 지정 이력이 있다.
- 추천: 공중 놓기 허용, 지면 낙하, 맵 밖 회수, 재시작 초기화를 공통 원칙과 기물별 예외로 정리한다. 서로 다른 기물의 기존 의도를 임의 제거하지 않는다.
- 근거: `HumanCannonPilot.cs:64`, `HumanCannonTrampoline.cs:100,318`, `HumanCannonTrampoline.Drop.cs`, `EscapeExitSaw.Network.cs:40`, `EscapeExitSaw.cs:246`.

### 8. 일반 화염과 즉사 화염의 구분
- 탈출로 저장된 FireJet 14개는 fireSeversLimbs=1이다. HumanCannonHazard는 이 값으로 사지 절단/즉사를 구분한다. 14개 전체 활성 동작을 이번에 검증한 것은 아니다.
- 외줄 화염과 탈출로 추격 화염은 ServerKill을 호출한다. 화염 종류에 따른 의도적 차이일 수 있다.
- 추천: 일반 접촉 화염의 결과와 추격 화염벽 같은 즉사형의 예외를 종류별로 명시한다. 모든 불을 같은 결과로 바꾸는 제안은 아니다.
- 근거: `HumanCannonHazard.cs:29,38`, `Assets/Scenes/Levels/escape_route.unity`, `Tightrope/Fire/TightropeFire.cs:270`, `EscapePursuitFire.cs:183`.

## 다음 순서와 한계
- 추천 순서: 연속 피해 → 상호작용 벽/층/거리 → F 대상 선택 → 외줄 추락 매달리기 → 카메라. 나머지는 공통 규칙 의도부터 결정한다.
- 3m/5m 추락 피해와 지상 톱 높이는 이미 보류된 항목이다. 새로 발견한 항목 수에 넣지 않는다.
- 정적 코드·저장 설정 조사다. 실제 두 PC, 겹친 상호작용, 전 씬 플레이 재현은 하지 않았다. 기존 테스트 통과를 이번 후보의 해결 증거로 사용하지 않는다.
