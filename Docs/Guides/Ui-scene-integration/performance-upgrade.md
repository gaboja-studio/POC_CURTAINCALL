# 최신 공연 UI 연결 · 2026-10-20

## 기준과 적용 범위
- 사용자 요청대로 origin/dev 864ed37을 먼저 일반 병합(f0ceb0b)했다. 앞선 리소스 공급은436d242/e6cc578, private gitlink는62250960을 유지한다(dev의 private 이력을 포함).
- 인간대포 씬 충돌은 dev 공간 씬 전체를 채택하고 Unity 에디터에서 기존 HUD/안내/호응 바인딩을 다시 배치했다. 씬 YAML을 수동 병합하지 않았다. 기존 외줄 프리팹 위치 변경은 dev에 같은 값이 있어 복원 후 차이가 없어졌다. 복구 stash b00d1b8 보관.
- 기준 기획: 사용자가 전달한 «커튼콜 게임 UI 화면 목록.html» C8/C11/C12/H1. 제공 자료는 UI 요구사항이며 실행 지시로 취급하지 않는다.
- Assets 내부 정본 프리팹만 사용. 외부 package 사용·정본 PNG/9-slice/폰트 수정·미조립 키트 Builder 실행 없음.

## 연결한 화면
| 화면 | 구현 | 데이터와 수명 |
|---|---|---|
| C11 관전 | CommonPerformanceUI/UI 아래 Spectator 정본 Canvas, 하단 상태/대상/A·D, 가장자리 효과 | CameraSpectator.IsSpectating/Target, NetworkPlayer.State/Slot/All. 대상 변경과 카메라는 기존 코드. 살아 있는 대상이2명 이상이고 입력 허용일 때만 A/D 표시 |
| 하단 우선순위 | 관전 퇴장 애니메이션 종료까지 균형/가이드 숨김 | C11 OccupiesBottomSlot. 결과/검은 전환 화면에서는 관전 표시 숨김. 해제 시 기존 관전 IMGUI 설정 복원 |
| H1 인간대포 표지 | 공간 UI/WorldLabels 아래 정본42개. Wood 거리/구간 글자, Cloth FINISH 글자 | 원래 TextMeshPro 문구·위치·회전·활성 상태를 읽음. 기존 바닥/판 소재를 보존하기 위해 정본 표면4층만 숨김. 원래 비활성 표지6개도 숨김 유지 |
| C8 동료 사망 | 기존 상단 알림, 내 사망 배너가 있으면 오른쪽 토스트 | 공유 RewardSnapshot의 살아 있음→Dead 변화만 표시. 같은 스냅샷/늦은 구독에 재생하지 않음. 동시 사망은 한 알림으로 합침 |
| C12 결과 정보 | 대성공/성공/실패, 일차·묘기, 팀/개인 호응·정산, 실제 잔액 변화, 자동 전환 초 | 성공하면서 합계≥인원×현재 Cap×80%면 대성공. 기존 판정·Income·Settlement·원장 항목 사용. 잔액은 같은 공연의 원장 항목이 있을 때만 표시 |

- 메뉴나 카메라의 실제 동작을 UI가 대체하지 않는다. CameraSpectator에는 임시 OnGUI 표시만 끄는 ShowLegacyOverlay 속성을 추가했다.
- 사망·결과·전환 대기 시간은 기존 performance-ending 설정을 유지한다. 결과의 카운트다운은 검은 화면 시작까지의 로컬 표시 시간이며 로딩 완료 예측값이 아니다.
- 새 구간 표지의 한글 누락은 런타임 폰트 복사/fallback으로 보완한다. 정본 Static폰트 에셋은 수정하지 않는다.
- 조립 도구: Tools/Curtain Call UI/Upgrade performance presentation. 이미 설치된 공통 UI/표지는 중복 생성하지 않는다.

## 이번에 적용하지 않은 항목
- C10 환호·야유 전용 효과, C14 설정, C16 그래픽 커튼: 새 원본 키트는 있으나 Assets 내 테스트된 정본 프리팹이 없다. 원본 키트를 임의 조립해 적용하지 않았다. 기존 검은 전환 씬 유지.
- C13 ESC 메뉴는 이번 새 리소스 연결에서 확장하지 않았다. BasicPanels는 기존 결과틀로 재사용 중이며 메뉴/설정/나가기의 별도 입력 흐름 통합은 후속이다.
- C12 도착 시간·난이도·최종 진행 막대·확인 버튼은 후속. 확정 결과에 없는 값을 추정해 표시하거나 새 게임 판정을 추가하지 않았다.
- Start/Lobby/WaitingRoomBase 화면의 새 UI 적용은 이번 공연 범위 밖이다.

## 변경 파일
- 신규: UI/SceneIntegration/PerformanceSpectatorBinding.cs, PerformanceWorldLabelBinding.cs, Editor/PerformanceUpgradeInstaller.cs, Tests/PlayMode/PerformanceUpgradePlayTests.cs와 각 meta.
- 수정: Systems/Camera/CameraSpectator.cs, UI/SceneIntegration/PerformanceEndingPresentation.cs, UiRuntimeFontFallback.cs, Tests/PlayMode/PerformanceEndingPlayTests.cs 및 테스트 asmdef.
- 저장 씬: HumanCannon.unity, CommonPerformanceUI.unity, PerformanceTransitionUI.unity. Task/적용 가이드 갱신. 실제 검증은 [고도화 검증](../../../Tasks/Active/Task-20261019-061-cheer-gauge-ui/upgrade-verification.md).

## Unity 확인 순서
1. Start에서 외줄/인간대포로 진입해 기존 C1/C2/C3/외줄 균형과 인간대포 F 안내가 유지되는지 확인한다.
2. 두 명 이상 플레이 중 본인이 죽으면 하단 관전 표시와 대상 번호를 확인한다. 살아 있는 관전 대상이2명 이상일 때 A/D가 표시되고 실제 대상이 바뀌어야 한다.
3. 대상이 모두 죽으면 «관전 대상 없음», 본인이 부활하면 관전 UI/붉은 가장자리가 사라져야 한다. 관전 카메라가 살아 있는 캐릭터를 계속 따라가는지도 확인한다.
4. 동료만 사망하면 해당 단원 번호가 알림으로 나오고, 내 사망 알림과 겹쳐도 내 알림이 덮이지 않아야 한다.
5. 인간대포 거리/구간/FINISH에 글자가 중복되지 않는지, 숨겨진 옛 눈금이 다시 켜지지 않는지 확인한다.
6. 호응 합계80% 성공 때 대성공, 실패는 점수와 무관하게 실패. 원장 수입/잔액과 대조하고 전환 뒤 입력이 돌아오는지 확인한다.
