---
summary: 리허설 안내 v2 프리팹·WorldNavigation 호출·화면 외곽 아웃라인 계약
status: active
updated: 2026-11-13
source: Task-20261113-122, Unity 에디터 저장·렌더 확인
---

# 리허설 네비 프리팹

## 언제 읽나
새 바닥 길안내의 레이어를 수정하거나 기존 RehearsalPathGuide에 연결할 때.

## 현재 사실
- 현행 v2 원화·투명 마스크4종: `Assets/_ThirdParty/curtaincall_navigation_v2/`. 황동 v1은 비교용 보존.
- v2 프리팹: `Assets/Resources/Prefabs/UIs/Navigation/V2/`의 PulseArrow, PulseDestination, RehearsalNavigationPulse.
- 현행 검토 씬: `Assets/Scenes/Tests/Navigation/NavigationPulseReview.unity`. 실게임 RehearsalPathGuide는 WorldNavigation 래퍼로 교체했다.
- NavigationPulseSequence: 기본 1초 간격(`pulseIntervalSeconds`)으로 새 파동이 6m/s(`waveSpeed`)로 출발→목적지 진행한다. 화살표 위상은 목적지까지 거리로 정해 출발점 이동에 흔들리지 않는다. 핸들 `SetPulseInterval`로 개별 덮어쓴다. 편집 모드 미리보기 및 인스펙터 시간 조절을 지원한다.
- RehearsalNavigationView: 마커 슬롯 i는 목적지에서 i칸 거리다. Play Mode 경로 갱신은 목표 자세로 지수 보간(`followSeconds` .12)하고 새 화살표만 페이드인하고 경로 밖으로 빠진 화살표는 즉시 숨긴다. 벽을 넘거나 `relocateDistance` 이상 바뀌면 즉시 숨긴 뒤 새 위치에서 페이드인한다. 편집 모드 SetPath는 즉시 배치한다. 로컬 안내 갱신 .15초, 공유 .4초.
- 원본 흰색 마스크에 자유 색상 tint를 적용한다. 셰이더 property block으로 색상/강조도/opacity/scale을 바꾼다. 검은 대비 레이어는 보존하며 프리팹 Transform을 매 프레임 직렬화하지 않는다.
- 코드: `Assets/Scripts/UI/Navigation/`. 네임스페이스는 `CurtainCall.UI.Wayfinding`이다. `CurtainCall.UI.Navigation`은 기존 코드의 `UnityEngine.UI.Navigation` 타입 해석과 충돌하므로 쓰지 않는다.
- 표시 API SetPath는 월드 바닥 점을 받고 선택적으로 콜라이더 지면/법선에 투영한다. 경로 계산은 NavigationPathPlanner의 전용 NavMesh 에이전트로 수행한다.
- 공통 연출 값은 `BaseGameSettings` > 네비게이션 안내(색상·밝기·경로 보정 시간·파동 간격·배속)이며 리허설을 포함한 모든 네비의 기본이다. 프리팹에는 파동 간격/보정 시간 칸을 두지 않는다.
- WorldNavigation.Show(destination, start=default, synchronize=false, animationSpeed=null, emphasis=null, color=null)는 NavigationHandle을 반환한다. null은 공통 설정을 실시간으로 따르고 값을 넘기면 해당 안내만 덮어쓴다.
- 플레이어 시작점은 공통 `플레이어 앞 비움 거리`(기본 1.5m)만큼 비우고 이후 1m를 실제 플레이어 거리로 매 프레임 페이드한다. 경로 갱신 주기에 묶인 발밑 화살표 생성/삭제가 버벅임의 원인이었다. 대상 타입은 Vector3/Transform/GameObject/NetworkPlayer이며 start 생략은 호출자 로컬 플레이어다.
- 공유 모드는 요청자가 목표 좌표를 추적하고 호스트가 계산한 경로/서버 시간 위상을 전원에게 배포한다. 로컬 모드는 메시지를 보내지 않는다. 리허설은 플레이어별 독립 로컬 안내다.
- 명시적 Object/Player 끝점은 대상 전체의 카메라 실루엣으로 강조한다. 목적지의 고정 문양은 숨기고 확산 링을 유지하며, 명시적 출발점에도 지면 법선에 맞춘 확산 링을 표시한다. 기본 생략 출발점은 자기 몸을 강조하지 않는다. Position으로 변경하면 고정 문양을 복원한다.
- 상호작용과 네비는 SceneIntegration/SilhouetteOutline 및 SilhouetteOutlineFeature를 공용 사용한다. PC/Mobile Renderer에 설치하며 로컬 MainCamera의 합친 마스크 바깥에만 그린다. 내부 부품선·메시 셸은 없고 불투명 벽 가림을 따른다. 같은 대상의 상호작용 색상이 네비보다 우선한다.
- 공유 아웃라인은 spawned NetworkObject ID+자식 경로 또는 동일 씬의 인덱스/이름 경로로 원격 인스턴스를 찾는다. 대응 대상을 찾지 못하면 경로/파동만 유지한다. 투명 재질은 제외하고 알파 컷아웃 구멍·ForceLOD·XR은 별도 지원 검토 대상이다.
- 핸들 Hide/Dispose 또는 호출 씬 언로드로 해제한다. 속도0은 정지, 강조도0은 숨김. 구 VisibilityChanged 계약은 유효 경로의 표시 여부를 유지한다.
- NavMesh.CreateSettings의 agentTypeID는 음수일 수 있다. 생성 여부를 bool로 추적해야 중복 생성·정리 누락을 피한다. 런타임 지형 변경 시 InvalidatePaths로 재베이크한다.
- SpriteRenderer 레이어와 SortingGroup, 높이 분리, ZWrite Off + LEqual로 구성했다. 미리보기 원화와 Unity 캡처는 Task design.md에서 구분한다.
- 조립기는 새 머티리얼만 SaveAssetIfDirty로 저장한다. 전체 SaveAssets는 무관한 동적 TMP 아틀라스를 저장할 수 있어 사용하지 않는다.

## 검증 범위
에디터 컴파일/참조/셰이더, 경계 입력/색상/강조도 계약3건, 정상 리허설 소구간 완료→프리팹 안내→집결 시 해제 Play Mode 검사 통과. 동일 프로세스의 NGO 호스트/클라이언트 UDP 테스트에서 늦은 참가자·정확한 경로/스타일·서버 위상·클라이언트 요청/삭제와 실제 수신 코드를 검증했다. 실제 렌더와 결과는 Task Captures/V2, API 사용법은 navigation-api.md에 있다. 다중 기기 지연 환경은 별도 검증 대상이다.

화면 실루엣 픽셀/가림/스킨 포즈/경사면 파동 EditMode 3건, 실제 전화기+플레이어와 리허설 PlayMode 2건, 상호작용 회귀1건 통과. UDP 수신자가 자신의 spawned 대상/자식에 아웃라인을 생성하는 검증도 통과했다. Captures/Outline에 실제 게임 캡처와 결과를 보관한다. 광범위 Navigation 필터에 포함된 기존 PursuitNavigation 테스트는 NavMesh 위 비활성 에이전트 SetPath 오류로 실패하여 관련 통과와 구분했다.
