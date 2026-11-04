# 추격자 눈 표시 조절

`Tools > Curtain Call > Pursuit > Mannequin Designer`의 전역 단계 설정에서 **머리 위 눈 아이콘 (로컬 전용)**을 펼친다. 또는 `Assets/Resources/Settings/GameSettings/WaitingRoomSettings.asset`의 Pursuit > Eyes를 수정한다.

| 설정 | 기본값 / 역할 |
|---|---|
| Enabled | 표시 켜기/끄기 |
| Width | 실제 눈 그림 너비 0.18m |
| Head Offset | Head 뼈에서 위로 0.42m |
| Fade Seconds | 범위 진입/이탈 페이드 0.5초 |
| Transition Seconds | 눈 열림·닫힘, 색/발광 전환 0.5초 |
| Normal / Alert / Chase Color | 흰색 / 노랑 / 혈색 빨강. HDR 색 선택 가능 |
| Normal / Alert / Chase Emission | 0 / 0 / 0.35. 빛을 내는 정도 |
| Glow Opacity | 0.12. 눈 주변의 약한 번짐, 0이면 끔 |

색상/발광/높이/크기는 매 프레임 현재 설정을 읽는다. 원본 PNG는 흰색이며 텍스처를 다시 만들 필요가 없다. 카메라 Bloom을 켜지 않아도 약한 번짐이 보이고, HDR 값을 크게 설정하면 프로젝트 Bloom 설정에도 영향을 받는다.

## 표시 규칙

- 활성 추격자와 자기 조작 플레이어의 3D 거리를 사용한다. 현재 단계의 SightRadius/WalkHearingRadius/RunHearingRadius 중 최댓값이 표시 반경이다. 방 이동 제한과 순찰 이탈 제한도 따른다. AlwaysNearest 단계에서도 화면 전체의 마네킹을 드러내지 않도록 이 유한 반경을 유지한다.
- 일반/순찰/복귀: 감은 흰 눈. 경계(Listen/Investigate/Search): 노란 뜬 눈. 자신에 대한 공격 준비(Prepare): 0.5초간 노란 눈으로 열림. 자신을 Chase/Capture: 혈색 빨강·약한 발광.
- 다른 플레이어를 준비/추격하는 마네킹은 자신이 발견된 것으로 표시하지 않으며 일반 눈을 사용한다. 대상 없는 경계/조사/수색은 범위 안에서 그 마네킹의 경계 상태를 보여준다.
- 범위에서 나가면 0.5초 페이드아웃. 추격 종료→수색이면 노랑, 순찰/복귀이면 0.5초 닫히며 흰색.
- 관전 대상 대신 NetworkPlayer.Local만 사용한다. 오프라인은 활성 PlayerInputReader를 사용한다. 포획·사망·씬 전환·기능 끄기에는 즉시 숨긴다.
- 눈은 카메라를 향하며 Head를 따라간다. 일반 깊이 검사로 벽 뒤에서는 가려진다. 눈 오브젝트·알파·프레임은 네트워크로 전송하지 않는다.

## 연결과 검증

PursuitDirector가 런타임에 PursuitEyeFeedback을 자동 생성하므로 개별 마네킹 프리팹이나 씬에 수동 추가하지 않는다. 스프라이트는 `Assets/Scripts/WaitingRoom/Pursuit/Resources/PursuitEye/Frames`의 8개 PNG다. 프레임 재생은 코드로 처리해 Animator 없이 도중 반전·추격 전환을 지원한다.

EditMode 5개 통과. 실제 대기실을 여는 LAN 호스트 PlayMode 테스트 1개에서 활성화 전 숨김, 순찰→준비→추격→수색, 다른 대상 추격, 거리 이탈/재진입, 비활성 정리를 확인했다. 실제 마네킹·최종 셰이더의 3상태 렌더는 `Artifacts/eye-unity-state-review.png`(왼쪽 일반/중앙 경계/오른쪽 추격). 2026-11-04 사용자가 설정 조정과 전체 검증을 완료했다고 확인했으며 저장/제출 단계의 추가 검증은 요청에 따라 생략했다. 에이전트가 별도로 다인 클라이언트 실기를 실행한 것은 아니다.

사용자 최종 저장값은 WaitingRoomSettings.asset에 보존한다. 예: Width .2m, AlertColor HDR (2,1.56,.24), GlowOpacity .3, 단계 시작 0/30/60/90초, 추격 속도 6.5/7/7.5/8m/s. 위 표는 제작 기본값 안내다.
