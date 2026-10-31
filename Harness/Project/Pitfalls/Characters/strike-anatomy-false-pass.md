---
summary: 타격 궤적 검사만으로 팔꿈치와 손목 자세를 합격시키지 않는다
status: active
updated: 2026-11-01
source: Task-20261101-088
---

## 타격 관절 검토
- 도구 끝 높이 차·손잡이 관통·바닥·접촉 시점 검사가 통과해도 관절 자세는 비정상일 수 있다. 사용자 후면 화면에서 이전 타격의 팔꿈치/손목 꺾임이 발견되었다.
- 도구 축을 고정하려고 손목을 전완과 독립적으로 회전시키지 않는다. 이번 타격은 4방향 원화 후 팔꿈치 단일축 FK와 거의 중립인 손목으로 재작성했다.
- 정면만 보지 않고 후면·양 측면, 준비/정점/가속/접촉/복귀를 비교한다. 실제 캐릭터 리타게팅 후 팔꿈치 굴곡 방향과 손목-전완 각도도 검사한다.
- 다리가 없는 자세에서 손을 낮게 내리면 RabbitTrialGrounding의 KeepHandAboveFloor가 팔을 다시 조정한다. 클립만 정상이어도 보정 뒤 손목이 꺾일 수 있으므로 최종 LateUpdate 이후를 검사한다. 이번 Cut2/3은 타격/뒤따름 높이를 올려 보정 개입을 피했다.
- Blender 우측 굴곡 법선은 Unity 좌표 변환에서 부호가 바뀐다. 원화/측면과 대조해 부호를 정하고, 절댓값 검사만으로 역굴곡을 허용하지 않는다.
- 한 Editor eval에서 여러 포즈를 Camera.Render하면 SkinnedMeshRenderer의 이전 프레임 버퍼가 남을 수 있다. 정적 비교판은 매 포즈 BakeMesh를 렌더하고, 실제 Player의 연속 재생 화면도 따로 확인한다.
- 원화: Task-088/Art/strike-four-view-concept-v2.png. 최신 재생성 스크립트는 build-anatomical-strike.py이며, build-overhead-strike.py는 폐기된 시도다.
