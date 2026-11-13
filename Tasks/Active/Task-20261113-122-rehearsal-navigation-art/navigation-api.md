# 네비 호출 API

```csharp
using CurtainCall.UI.Wayfinding;

// 출발점 생략: 호출 화면의 로컬 플레이어. 목적지는 Transform/GameObject/Vector3/NetworkPlayer.
NavigationHandle guide = WorldNavigation.Show(
    destination: targetObject,
    synchronize: false,
    animationSpeed: 1.5f,
    emphasis: .8f,
    color: Color.cyan);

// 출발점도 같은 타입을 받으며, 오브젝트/플레이어는 움직임을 추적한다.
guide.Update(destination: targetPlayer, start: startTransform,
    synchronize: true, animationSpeed: 2, emphasis: 1, color: Color.magenta);

// 목표·표시 범위는 유지한 채 외형 조정.
guide.SetAppearance(Color.yellow, animationSpeed: .75f, emphasis: .6f);
guide.Hide(); // Dispose()와 동일. 호출자가 소유한 안내 하나만 해제.
```

## 의미와 수명
- Play Mode에서 호출한다. 핸들은 여러 개를 동시에 유지할 수 있다.
- `NavigationTarget.LocalPlayer`, `.Player(player)`, `.Object(transform/gameObject)`, `.Position(vector)` 명시형도 제공한다. 출발점 기본값은 로컬 플레이어다.
- 목적지는 필수다. 목표 오브젝트 파괴/비활성·플레이어 부재·비정상 좌표는 숨김 처리한다. 다시 유효해지면 재탐색한다.
- 호출 씬이 언로드되면 핸들을 자동 해제한다. 컴포넌트 비활성 시에는 소유자가 Dispose한다. 해제한 핸들을 Update하면 ObjectDisposedException이다.
- 속도는 0–10배, 0은 정지. 1배는 4.2초 반복/2.35초 전파. 강조도는 0–1 불투명도 배율이며 0은 완전 숨김이다. 색상 알파도 곱한다.
- `IsVisible`/`VisibilityChanged`는 유효한 안내가 표시 중인지 뜻한다. 개별 화살표의 루프 휴지 구간마다 이벤트를 반복하지 않는다.
- 완전한 NavMesh 경로만 표시하고 계산 실패 시 최대 1초 유예한다. .4초마다 목표를 추적한다. 최초 계산은 문이 열리는 시간을 고려해 3초 대기한다.
- 지형 변경 시 `NavigationService.EnsureExists().InvalidatePaths()`로 재베이크할 수 있다. 씬 로드/언로드는 자동 무효화한다. 기본 경로 에이전트와 분리하여 추격 NavMesh를 건드리지 않는다.

## 공유 안내
- `synchronize: false`: 네트워크 메시지 없이 각 화면만 표시한다. 리허설 완료 안내는 이 모드다.
- `true`: 요청자의 출발점/목적지를 호스트에 보내고 호스트가 계산한 동일 코너를 전원에게 전달한다. 출발점을 생략하면 **요청자의** 로컬 플레이어가 기준이다.
- 호스트 서버 시간과 누적 위상/속도로 연출을 맞춘다. 새 참가자는 다음 스냅샷에서 경로를 받는다. 네트워크 지연에 따른 프레임 차이는 있을 수 있다.
- 공유 안내는 한 소유자가 한 번 호출한다. 모든 참가자가 각각 호출하면 소유자별 독립 안내가 여러 개 생성된다.
- 연결 전 공유 요청은 세션 준비를 기다린다. 해제·씬 언로드·소유자 이탈 또는 3초 갱신 중단 시 제거한다. 세션 종료 시 원격 안내는 숨긴다.
- 동적 목표의 좌표는 요청자가 해결한다. 경로/파동은 네트워크 ID 없이도 공유한다. 대상 아웃라인까지 공유하려면 아래 끝점 식별 조건이 필요하다. 경로 계산은 호스트만 수행한다.
- 수신 한도: 공유 경로32개, 경로당 코너256개, 스냅샷256KiB. 매우 큰 스냅샷은 전송하지 않는다. 일반 집결 경로는 수 개의 코너를 사용한다.

## 실게임 연결
`RehearsalPathGuide.SetTarget(Vector3?)` 계약과 VisibilityChanged를 유지하는 래퍼가 WorldNavigation을 호출한다. `WaitingRoomRehearsal.TickCompleted()`가 기존 집결 판정에 따라 생성/해제한다. 구 LineRenderer/화살표 메시 생성 코드는 제거했다. SpriteRenderer 마스크/검은 대비 레이어/독립 MaterialPropertyBlock을 사용한다.

## 오브젝트/플레이어 끝점
- 메서드 사용법은 동일하다. 명시적 Object/Player 목적지는 고정 도착 문양을 숨기고 대상의 화면 외곽 아웃라인을 표시한다. 도착 확산 링은 유지한다.
- 명시적 Object/Player 출발지도 아웃라인과 출발 확산 링을 표시한다. 생략한 기본 로컬 출발점은 자기 몸을 추가 강조하지 않는다.
- Position 목적지는 기존 링/문양을 유지한다. Update로 종류를 바꾸면 이전 아웃라인을 해제하고 맞는 표시를 복원한다.
- 아웃라인은 네비 색상/강조도를 따른다. 같은 대상에 상호작용 강조가 있으면 상호작용 색상이 우선한다. 로컬 MainCamera에만 적용하며 원본 메시/재질/LOD는 수정하지 않는다.
- 공유 모드도 대상 식별 정보를 보낸다. 동적 오브젝트/플레이어는 spawned NetworkObject, 정적 오브젝트는 각 화면에서 같은 씬 하이어라키를 사용한다. 원격에서 대응 대상을 찾지 못하면 경로/파동을 유지하고 잘못된 오브젝트를 강조하지 않는다.
- 공용 기능은 PC_Renderer와 Mobile_Renderer의 SilhouetteOutlineFeature이다. 대상 전체의 합친 마스크 바깥에만 선을 그리므로 부품끼리 겹치는 내부선이 없다. 불투명한 벽에 가려진 외곽은 표시하지 않는다.
