---
summary: Blender 오르기·내려오기 원화/액션과 10종 프리팹 연결을 재작업할 때
status: active
updated: 2026-11-12
source: Task-20261112-117, Assets/_ThirdParty/curtaincall/Pursuit/Climbing/Source~/README.md
---

# 추격자 오르기 애니메이션

## 언제 읽나
오르기·내려오기 동작/키포즈/클립을 고치거나 추격자 프리팹을 다시 만들 때.

## 사실
- 실제 10개 프리팹의 V7 메시/재질/결손과 원본 V2 리그를 사용했다. 베이스는 [appearance-source.md](appearance-source.md).
- private `curtaincall/Pursuit/Climbing/Source~/Concepts/`에 모델별 오르기4방향·6방향/내려오기4방향 키포즈 도해 원화 총30장. 정적 포즈를 먼저 설계하고 manifest 해시 이후 액션 제작. 사후 렌더를 원화로 바꾼 것이 아니다.
- `Source~/pursuit-climbing.blend`에 원본 리그 기반 Climb1.1초/Drop0.5초, 30fps 액션20개. `Review_` 복제본은 게임 루트 이동을 합성한 검토용이며 내보내기 대상이 아니다.
- Unity `PursuitClimbAnimationImporter`가 Blender 월드 본 회전 차이를 원래 bind에 변환해 `Climbing/Clips/*.anim` 생성, 기존 프리팹10종의 Clips[7]/[8]에 연결. 원본 모델/메시/재질/기존7클립은 유지. 대기실 씬·배치·NavMesh는 변경하지 않았다.
- GameSettings ClimbSeconds/DropSeconds에 맞춘 일회 재생. Climb/Drop 동안 포획·Chest 시선 덧회전 금지.
- 오르기 월드 이동은 `PursuitClimbPath`(호스트)가 담당한다(2026-11-12). 실제 콜라이더 가장자리를 찾아, 클립 키포즈(pose-plan.py CLIMB의 접촉 .12·당기기 .32·무릎 넘기기 .60 손 위치)마다 손이 가장자리에 오는 자리에 루트를 둔다. 둥근 옆면은 몸 캡슐이 겹치지 않는 만큼만 붙는다. 손은 .12~.62 동안 가장자리를 짚는다. 가장자리가 없으면 기존 `ClimbPose`(수직 상승 후 넘기), 내려오기는 `ClimbPose` 포물선. 원격은 루트 스냅샷을 따른다.
- `PursuitClimbIk`는 Legacy Animation 뒤 손바닥/발바닥 기준 접점을 실제 콜라이더 표면에 고정하는 시각 전용 2본 IK다. 본 위치·스케일·게임 루트 경로는 바꾸지 않는다. 접점은 콜라이더 로컬 좌표로 보관하며 지형이 사라지거나 도달 거리를 벗어나면 해제한다. 트리거·플레이어·추격자·동적 Rigidbody는 표면에서 제외한다.
- `GameSettings.WaitingRoom.Pursuit`의 ClimbContactIk, ClimbIkSurfaceMask, ClimbIkProbeHeight, ClimbIkSurfaceOffset으로 조정한다. 모든 높이/너비에서 항상 손발이 닿는다는 뜻은 아니다. 접근·해제·도달 한계 구간은 원본 클립과 섞고, 완전 고정 구간만 접점 오차를 검사한다. 손가락별 IK·전신 균형/충돌 보정은 없다.
- 호스트 통과의 from/to/progress/duration을 스냅샷 버전6으로 보낸다. 호스트와 원격은 같은 버전이어야 한다. 원격도 로컬 지형에서 접점을 찾으므로 동일 지형이 필요하다. 기존 루트 보간은 유지하며 실제 2클라이언트 지연 체감 검증은 별도다.

- 직접 오르기(`PursuitDirectClimb`, `PursuitActor.Climb`): 추격 대상이 길로 닿지 않는 소품 윗면에 서 있으면 실제 충돌체로 윗면·가장자리·머리 공간을 찾는다. 가장자리 앞까지 걷고(막히면 다른 방향) agent를 끈 채 오른 뒤 윗면에서 대상을 따라 걸으며 추격(포획 가능)한다. 대상이 내려가거나 놓치면 아래 NavMesh로 뛰어내린다. 1.5m보다 높으면 오르는 시간이 비례해 는다(`TraversalSeconds`).
- 실제 대기실 측정(2026-11-12, 씬 저장 없음): 서 있을 수 있는 소품 윗면 2644지점 중 87% 도달(링크+직접). 상자·침대·책상·사물함·트렁크·의자·작업대·터널 드럼통 1.94m 전부 가능. 실패는 벽·천장·지붕판·문틀·난간 윗면과 천장 근처 배관(머리 공간 없음).

## 재작업 주의
- 기존 BuildModels 메뉴는 프리팹/기존 클립을 재생성하므로 이 작업에는 실행하지 않는다. 전용 Import Blender Climbing 메뉴를 사용한다.
- Blender→Unity 위치는 `(-x,z,-y)`, 회전은 좌표변환 행렬로 conjugate한 월드 차이를 기존 bind에 적용한다. 본 축/로컬 회전을 직접 복사하지 않는다.
- Animation.AddClip의 런타임 별칭만으로 에디터 참조 저장을 보장하지 않는다. AnimationUtility.SetAnimationClips로 저장하고 Initialize에서 별칭을 붙인다.
- updateWhenOffscreen 때문에 읽은 localBounds가 기준 포즈로 갱신될 수 있다. `authored-bounds.json`의 최초 직렬화 bounds를 포함해 합집합으로 넓힌다.
- PreviewScene에서 SampleAnimation→즉시 Camera.Render는 이전 스킨 포즈를 찍을 수 있다. 정적 검토는 BakeMesh 임시 렌더를 사용하고 실제 플레이와 구분한다.
- 클립 손 위치는 루트(발)가 가장자리보다 0.77~1.34m 아래에 있다고 가정한다. 루트를 가장자리 높이까지 먼저 올리면 손이 표면에서 0.6~0.9m 떨어져 허공을 잡는다(이전 ClimbPose, 보일러·상자 공통). pose-plan.py CLIMB 표를 바꾸면 `PursuitClimbPath.Keys/HandForward/HandHeight`도 같이 바꾼다.
- 이 FBX 리그는 본 월드 스케일이 100인 경우가 있다. 2.5cm 같은 접점 보정은 end.TransformVector(localNormal).magnitude로 나눠 로컬 거리로 변환한다. 로컬에 .025를 바로 넣으면 손이 수 m 밀려 IK가 전부 해제된다.
- Female03/04 왼발은 정상 메시지만 Foot_L 최대 가중치가 약 .453이다. 절대 가중치 .5 이상만 쓰면 결손으로 오판한다. 사지별 최대 영향도의 상대 임계값으로 접점 정점을 고른다. 실제 결손 M01 왼발/M03 왼손/F05 왼손/F06 오른손은 해당 본의 가중치가0이다.
- 내려오기 루트는 통과 끝에 지면에 닿는다. 발 접점 가중치를 t=.95부터 미리 낮추면 착지 고정이 사라진다. 도달 가능해진 발을 통과 종료까지 유지하고 FinishClimb/Clear에서 애니메이션 포즈를 복원한다.
