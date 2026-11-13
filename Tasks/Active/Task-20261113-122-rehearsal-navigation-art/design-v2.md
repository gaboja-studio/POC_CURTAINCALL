# Guiding Pulse — 단색 순차 안내

## 반영한 피드백
황동/직물은 갈색 바닥과 비슷하고 물리적 소품으로 보였다. v2는 재질 묘사와 받침판을 제거하고 청록 단색의 굵은 화살표, 목적지 링, 작은 공연 문양으로 구성했다. 어두운 외곽과 좁은 발광을 추가해 밝고 어두운 바닥에서 윤곽을 유지한다.

## 제작물
- imagegen 내장 도구 원화: `Assets/_ThirdParty/curtaincall_navigation_v2/Source/navigation-pulse-concept.png`.
- 최종 프롬프트: 같은 Source의 `generation-prompts.md`.
- 투명 원본 4종: `Sprites/chevron-mask.png`, `aura-mask.png`, `ring-mask.png`, `spark-mask.png`. 1254×1254, 흰색 마스크에 Unity tint 적용.
- 조립 프리팹: `Assets/Resources/Prefabs/UIs/Navigation/V2/`의 `PulseArrow`, `PulseDestination`, `RehearsalNavigationPulse`.
- 검토 씬: `Assets/Scenes/Tests/Navigation/NavigationPulseReview.unity`.
- v1 원화·프리팹·검토 씬은 비교용으로 유지한다. 후속 요청으로 RehearsalPathGuide를 v2 API에 연결했다. 호출 규격은 [navigation-api.md](navigation-api.md)를 참고한다.

## 루프 기본값
| 항목 | 시간 |
|---|---|
| 전체 반복 주기 | 4.2초 |
| 출발점에서 마지막 화살표까지 전파 | 2.35초 |
| 각 화살표 등장 | 0.12초 |
| 유지 | 0.16초 |
| 소멸 | 0.48초 |
| 도착 확산 링 | 0.8초 |

약 3개 화살표가 이동하는 파형으로 켜진다. 목적지는 약한 숨쉬기 피드백을 유지하고 도착 시 링을 크게 퍼뜨린다. 마지막 약 1초에는 화살표가 모두 꺼진다. 색과 각 시간은 프리팹 인스펙터에서 조절한다. 설정된 주기가 전파·소멸보다 짧으면 휴지 구간을 확보하도록 유효 주기를 늘린다.

## 편집 및 연결
`NavigationPulseSequence`는 Play Mode에서 unscaled time, 편집 모드에서는 editor time을 사용한다. Preview In Editor/Automatic Playback으로 미리보기를 제어한다. 셰이더 property block으로 alpha/scale을 조절하므로 프리팹 Transform과 SpriteRenderer.color를 매 프레임 변경하지 않는다. 메뉴 `CurtainCall > UI > Navigation > V2 Build pulse prefabs`는 v2 결과를 재조립한다. 수동 조정 후 재조립은 제작 기본값으로 덮어쓰므로 조정 내용을 먼저 보존한다.

경로 풀은 목적지부터 저장되지만 애니메이션은 역순으로 읽어 플레이어에서 출발한다. SetPath 재호출은 같은 루프 시간을 유지하고, 숨긴 뒤 새 경로를 표시하면 새 주기로 시작한다. 입력 경로 위 위치를 유지해 벽 모서리를 곡선으로 잘라 지나가지 않는다. 공통 GameSettings/게임 규칙 변경 없음.

## 검증과 증거
- 에디터 검사: 출발→도착 순서, 휴지 구간 alpha=0, 다음 주기 반복, 경로 갱신 후 연결, 직렬화된 11개 표식, 참조/셰이더, LineRenderer 0개 통과.
- Play Mode 자동 시간 36.91초→111.38초 증가 및 automatic=true 확인. 검사 시 컴파일 실패=false, 콘솔 오류/경고=0.
- 실제 Unity 프리팹을 24fps 고정 시간으로 평가해 101개 프레임을 렌더, 모두 서로 다른 SHA256 확인. 이는 라이브 플레이 화면 녹화가 아닌 실제 프리팹의 한 주기 렌더다.
- `Captures/V2/navigation-pulse-loop.mp4`와 `.gif`, `phase-01..04.png`, `layers.png`로 검토한다. FFmpeg는 OS 임시 폴더에만 준비했으며 프로젝트 패키지를 변경하지 않았다.
- Play Mode 검증 후 종료. 실게임 리허설 이벤트·플레이어 이동 추종·경사/계단 투영·멀티플레이 회귀는 후속 연결 단계다.
- 최종 check-work는 다른 Task-115 로그의154줄(hard130) 때문에 문서 단계 FAIL, 컴파일/자동 테스트 단계 SKIPPED. 최신 dev에서 Unity 컴파일 실패 없음과 v2 표시 검증은 별도 재확인했다.
