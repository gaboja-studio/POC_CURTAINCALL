# 인간대포 UI 적용

2026-10-20. Start/Lobby보다 인간대포 후속 적용을 먼저 진행한다.

## 구성과 정본
- `CommonPerformanceUI.unity/UI`: 기존 C1/C2/C3, 추가 `CommonGuideCanvas`(C7), `CommonTooltipCanvas`(C15).
- `HumanCannon.unity/UI`: `PerformanceSceneBinding`, `HumanCannonUiBinding`, `HumanCannonSectionCanvas/CurrentSection`.
- 사용한 Assets 정본: `Resources/Prefabs/UIs/Guides/Guides.prefab`, `Tooltips/Tooltips.prefab`. 원본 이미지·프리팹·9-slice 변경 없음, 인계 ZIP 사용 없음.
- C7은 정본의 하단 배치와 크기를 유지한다. 구간 문자는 하단48 UI, 너비544에 표시한다. C15는 장치 위치를 화면 좌표로 변환하고 가장자리24 UI 안으로 보정한다. 카메라 뒤 대상은 숨긴다.
- 정본 전용 정적 폰트의 누락 글자만 런타임 복사본의 fallback으로 보충한다. Assets에 이미 있는 Tooltips의 Pretendard-Medium/SemiBold TTF로 실행 중 보조 폰트를 만들며 원래 굵기를 유지한다. 정적 게임 폰트에도 없는 ‘램·펄’까지 자동 검사한다. 원본 폰트 에셋은 수정하지 않는다.

## 데이터와 입력
| 표시 | 읽는 기존 게임 데이터 |
| --- | --- |
| 대포 접근 `[F] 타기` | `HumanCannonSystem.FindBoardableStation`, `BoardingPoint` |
| 탑승 `[F] 발사`, 누르는 중 `[F 길게] 내리기` | `IsSeated`, `PlayerInputReader.Current.InteractHoldProgress` |
| 트램펄린 접근/운반 안내 | `CanGrab`, `Carrier`, `Handle` |
| 봉 접근 `[F] 잡기` / 잡은 후 `[Space] 봉 놓기` | `FindGrabbable`, `HangingPosition`, `HumanCannonPilot.IsHoldingPole` |
| 현재 구간 | 기존 `HumanCannonCourseSections.SectionAt`, 씬의 동일 경계값 복사 |
| 표시 차단 | `NetworkPlayer.State`, `IsSpawned`, `InputGate.IsBlocked`, 운송 중 `IsRiding` |

- 안내는 동시에 하나만 표시한다. 봉→운송→운반→탑승 순으로 게임의 입력 우선순위를 따른다. 기존 `HumanCannonCourseSections`는 어댑터 활성 중 비활성화하고 해제 시 원래 enabled 상태로 복원한다.
- 안내는 상황별로 반복 표시한다. 화면 문서의 ‘첫 공연에서만’ 튜토리얼 완료 저장은 이번 단계에서 구현하지 않았다.
- UI가 키를 직접 처리하거나 게임 요청/성공/실패를 호출하지 않는다. C2는 기존 활성 불벽의 `EndTime - route.Clock` 중 가장 이른 도달 시간을 표시한다.
- C1은 실제 보상 명부, C3는 기존 진행도 연결을 유지한다. C3 정본은2~4인 지원이므로1인은 `—`; 인간대포 불 마커/구간별 그래픽 트랙은 미적용이다. P1 없음.

## Unity 사용자 확인
1. 기존 Start 흐름으로 인간대포에 진입한다. 빠른 확인은 HumanCannon 씬 Play → LAN 호스트(단독 연습)로 가능하다. 단독 연습은 C7/C15/구간 표시만, 실제 공연 상태에서 C1/C2/C3가 나타난다.
2. 대포에 접근해 툴팁을 확인하고 F 탑승→F 발사 또는 F 길게 하차한다. 트램펄린 F 잡기/길게 놓기, 비행 중 봉 F 잡기/Space 놓기를 확인한다.
3. 메뉴·사망·관전·씬 전환 때 안내 잔상이 없는지 확인한다. Hierarchy에는 공간 씬과 CommonPerformanceUI 각1개, Canvas는 각각 UI 아래에 있어야 한다.

## 검증 범위
- 자동/호스트 검증 결과는 Task handoff에 기록한다. API를 통한 탑승·보상·불벽 활성은 합성 상태 검증이며 실제 키 입력/전체 공연 완주와 구분한다.
- 실제 다인·재접속·키보드 조작·불 접촉·여러 해상도·Start부터 인간대포 전체 진행은 미실행.
- C11 관전/C12 결과/C13 ESC 전용 정본은 확인되지 않았다. 이 단계는 해당 화면의 새 그래픽 적용을 포함하지 않으며 기존 시스템 기능을 유지한다.

## 실제 수정 파일
- 씬: `Assets/Scenes/UI/CommonPerformanceUI.unity`, `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity`.
- 연결: `Assets/Scripts/UI/SceneIntegration/CommonPerformanceUI.cs`, 신규 `HumanCannonUiBinding.cs`, `UiRuntimeFontFallback.cs`.
- 설치: 동일 폴더 `Editor/HumanCannonUiInstaller.cs`(일회 설치, 재실행 중복 차단).
- 테스트: 동일 폴더 `Tests/PlayMode/PerformanceSceneBindingPlayTests.cs`; 신규 C#3개의 `.meta`.
- 문서: 본 가이드·next-plan 및 Task handoff/todo/plan/meta.
