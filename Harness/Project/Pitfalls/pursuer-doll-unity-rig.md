---
summary: 추격자의 비인간형 소켓·신장 손 크기·숙임 통로·플레이어 목 시선을 연결할 때
status: active
updated: 2026-11-16
source: Tasks/Active/Task-20261114-130-escape-pursuer-followup/log.md
---

# 추격 인형 Unity 리그 연결

추격자 FBX나 동작을 교체할 때 읽는다. 현재 연결은 Revision7이며 아래 Revision5 항목은 이전 리그 유지보수용이다.

## 현재 Revision7

- 무거운 Run은 `CircusGolemHeavyGait/Golem_RunHeavy.anim`과 별도 manifest의8.58m 주기를 한 쌍으로 쓴다. 기존 R7 원본 Run5.94m와 혼용하면 접지 중 발이 미끄러진다. Revision7Builder.Configure는 HeavyGaitBuilder.Configure도 호출한다.
- mesh 없이 armature만 FBX로 내보내면 Unity가 `PursuerDollRig` 컨테이너를 생략한다. 독립 클립의 빈 루트/Root 경로를 모델의 `PursuerDollRig`/`PursuerDollRig/Root`로 복원하고 양발 접지를 검사한다.
- 발소리는 현재 gait phase의0/.5 통과를 세며 정지 포즈 이동과 별도 누적 거리를 섞지 않는다. 숨은20m/s 이동은0.45초 최소 간격으로 합치고 OneShot 잔향을 보존한다. 충격음 pitch는 독립 source에 적용한다.
- 보스 도약 마지막 프레임은 상태가 Landing으로 바뀌어도 이동 전 Leaping이었다. grounded gait 거리는 이동 전 Battle/Dashing에만 누적하고 착지는 별도 큐로 한 번 재생한다.
- `circus-golem-look.fbx`의207본/9스킨과 `circus-golem-motions.fbx`에서 추출한25개 .anim을 사용한다. motions FBX의 내장 메시에는 최신 목 가중치가 없으므로 외형으로 사용하지 않는다. Roar enum 슬롯은 ChargeWarning을 참조해 기존 직렬화 인덱스를 유지한다.
- 팔은 Forearm.R의 회전과 Hand.R·Sleeve00~05.R·Fold00~144.R의 이동으로 펼친다. 모든 본 스케일은1이다. Revision5의 Hand stretch isolation이나 클립 경로 치환을 적용하면 안 된다. 실제 타격/포획 손 위치를 StretchTo로 반영하고 손 크기·손가락·소켓 접합을 검사한다.
- 보폭은 Walk/CarryWalk3.24m, Run5.94m, CrouchWalk3.6m다. 실제 이동 거리로 주기를 진행한다. Run의 명목 접지 끝0.36은30fps 키 사이이므로 접지 검사는 마지막 접지 키까지 하고 다음 이륙 보간을 포함하지 않는다.
- 포획은 CaptureTear→GrabHold/CarryWalk, 실패는 GrabMiss, 거리는 GrabStretch/ReachLong/ReachFullRange로 대응한다. 돌진·좌우 피격·점화·사망·절단 후 이동은 별도 원본 클립을 쓴다. Turn 클립의 Root 회전과 Death의 넘어짐을 게임 루트에서 다시 더하지 않는다.
- 두 다리가 없으면 MaimedCrawl을 기본으로 쓴다. 공격·피격에도 원본 Idle→MaimedCrawl 골반/척추 차이를 적용하고, Death 종료의 접지 포즈로 이행한다. 기존 rig의1.55m 하강을 중복 적용하지 않는다. 소켓 계산에도 동일한 자세 보정을 적용한다.
- `EscapePursuerRevision7Builder`로 공용 프리팹과 운영/복사/기존 테스트 씬을 함께 갱신한다. 이전 빌더는 Revision7을 Revision5로 덮어쓰지 않게 보호한다. 기존 에셋 GUID는 보존하고 씬/프리팹은 Unity API로 저장한다.

## 이전 Revision5

- 원본은56본/9스킨이고 손가락이 이미 구워져 있다. Humanoid 자동 매핑이나 기존 회색 리그 경로에 덮어씌우지 않는다. 모델 FBX와9클립을 별개로 가져오고 클립을 독립 .anim으로 추출한다. .blend는 원본 작업트리에 보존한다.
- manifest 프레임은1부터, Unity 클립 시간은0부터 시작한다. 타격/손 놓기/착지는 `(frame - 1) / fps`로 환산하고 게임의 준비·충돌·이동 시간에 구간별로 대응한다. 원격도 같은 스냅샷 시계를 사용한다.
- Blender의 Hand.R `inherit_scale=NONE`은 Unity Transform 설정으로 직접 옮겨지지 않는다. 원본 FBX에 구워진 보정을 유지한 채 추가 Forearm.R 신장을 해야 한다. 손목 위치만 옮기면 팔과 손이 벌어지고, 회전된 손목에 역스케일만 걸면 손가락이 찌그러질 수 있다.
- `Hand stretch isolation`을 Forearm.R와 Hand.R 사이에 추가하고 네이티브 클립 경로도 함께 바꾼다. 추가 배율만 손목 회전 전에 상쇄하며 보정 원점도 손목 접합부로 옮긴다. 매 샘플 전에 보정을 초기화한다. 4/12/36m 표본에서 소켓 도달, 팔 끝 접합, 손 메시 내부 거리 불변을 EditMode로 검사한다.
- 원본 Duck은 제자리 동작이며 높이 약4.4m다. 기존3.35m 통로를 통과한다고 볼 수 없다. 파생 CrouchWalk는 원본 Walk의 발 위치/회전을 유지하고 무릎과 척추를 굽힌다. 스케일1, 전61프레임 발 위치 오차3mm 미만/전체 메시 높이3.35m 이하를 검사한다.
- 새 모델의 폭은 회색 캡슐보다 넓다. 몸 캡슐 검사 뒤 넓은 외형 후보와 실제 BakeMesh 정점을 검사한다. 큰 구형 외피가 대포 차대 AABB에 닿는 것과 실제 메시 침범은 다르다. 기물은 옮기지 않고 추격자 착지점과 숨은 벽 여유를 조정한다.
- 손·발0~3, 다리4~5, 팔6~7 매핑을 유지한다. 오른손3 또는 오른팔7을 잃으면 보이지 않는 소켓으로 새 공격을 시작하면 안 된다. 이미 날아간 투사체는 별도로 처리한다.
- 편집 샘플과 자동 검사는 원격 재생·실제 입력·완주·재미를 증명하지 않는다. 플레이 검증은 사용자가 한다.

## 라운드·동작 전환
- 타이머·추격자·보스 메시지는 별도로 도착한다. 추격자 포즈/늘어난 손과 보스 사망/절단 상태를 각각 현재 라운드와 대조한다. 한 메시지만 최신이라고 이전 보스의 Dead/Mask를 적용하면 새 몸도 눕거나 사라진다.
- Restart는 다음 등장 시계보다 먼저 부위 렌더러·히트박스·경고·자기 절단 파편을 복구한다. 비활성 중 Restarted를 놓친 경우에는 라운드 불일치 표시 갱신에서도 정리한다.
- 보스 피격 등으로 Throw 준비 모션을 덮을 때 아직 발사하지 않은 DebrisWarning을 함께 취소한다. 오른손이 남은 왼쪽 부위 피격도 해당하며 이미 발사된 DebrisFlight/Impact는 보존한다.

## 플레이어 목 시선
- Blender Track To는 Unity 런타임 동작으로 이전되지 않는다. `EscapePursuerDoll.LateUpdate`에서 기존 모션 샘플/팔 신장 뒤 `ApplyLook`를 적용한다. 매번 원본 포즈에서 작은 회전만 보간하면 목표에 도달하지 못하므로 목표 방향 자체를 프레임 간 보간한다. 다음 샘플 전 이전 목 회전을 복원해 누적 회전을 막는다.
- 현재 Revision7은 최신 look FBX의 목/머리 가중치를 그대로 사용한다. 검은 얼굴은 Head에 고정하고 주변 천은 Neck/Chest로 이어진다. 이전 Revision5만 `EscapePursuerLookBuilder`의 별도 `BodyPlayerLook.asset`을 사용한다. 두 리그의 Body 메시를 교환하지 않는다.
- 본의 로컬 정면은 FBX 기본 자세에서 구한다. Head/Neck에 Transform.forward를 그대로 쓰거나 애니메이션 포즈에서 정면을 계산하면 축이 틀어진다.
- 시선 대상은 호스트의 포획 대상→투척/파편 대상→경로/보스 추격 대상 순으로 결정한다. 스냅샷 v8의 LookActor를 참가자에게 보내고 각 피어에서 실제 플레이어 위치·BodyHeight를 다시 읽는다. Camera.main/NetworkPlayer.Local로 대체하지 않는다. 사망·미생성/접속 해제·라운드 불일치에는 원본 포즈로 돌아간다.
- 좌우80도/위50도/아래65도 제한을 넘는 뒤쪽 대상은 기존 이동 루트 회전으로 다시 시야에 들어온다. 시선 처리에서 이동/충돌 루트를 강제로 회전시키지 않는다.
- Unity 공식 API 확인: [LateUpdate](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.LateUpdate.html), [Quaternion.LookRotation](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Quaternion.LookRotation.html).
