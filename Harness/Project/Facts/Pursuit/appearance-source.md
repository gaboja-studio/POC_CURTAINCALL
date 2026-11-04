---
summary: 추격자 체형·봉제·혈흔·원화 기준과 구현 상태를 확인할 때
status: active
updated: 2026-11-04
source: Task-20261104-102, 현재 FBX10종 직접 import 및 셰이더/프리팹 조사
---

# 추격자 외형 원본

## 언제 읽나
추격자의 체형·관절 봉제·의상·혈흔을 제작하거나 원화를 모델에 반영하기 전에.

## 사실
- private `Assets/_ThirdParty/curtaincall/Pursuit/Prefabs`는 남성01~04, 여성01~06의10종이다.
- `Models/Pursuit_*.fbx`가 기존 비율·결손 기준이다. Male01 실제 import 높이1.7783m/정점560. 값은 전시 포즈 월드 경계이며 게임 배치 배율 전이다.
- 원본 리그의 왼/오른쪽 이름을 따른다. Male01 왼발, Male03 왼손, Male04/Female04/Female06 머리, Female05 왼팔 전체, Female06 오른전완/손의 결손을 보존한다.
- `Reclaimed/ReclaimedShell.shader`가 정규화 좌표의 p.x 중심에 세로 seam/stitches를 생성한다. FBX만 편집하면 이 선이 없어지지 않는다.
- `PursuitAppearanceBuilder.cs`는 미변형 좌표를 UV 채널2에 넣고 목 장식·보수판을 만든다.
- `PursuitAppearance.cs`는 CaptureCount×BloodPerCapture를 _BloodAmount에 넣고 InjuryBloodEffects.IsVisible을 따른다. 현재 기본 얼룩 바리에이션 제작과는 별개다.
- Task102 최신 초안은 `Concepts/JointSeams/fbx-proportions-{male,female}-v5.png`. v3는 의상 참고, v4의 과장된 머리/손/몸 비율은 사용자 피드백으로 제외했다.
- 사용자는 기존 FBX 체형 유지, CharacterArt 실제 모델과 같은 인형 재질, 다른 단원 정체성, 목 리본 제거, 관절별 봉제·혈흔 바리에이션을 요청했다. v5 최종 미술 승인은 아직 없다.
- Task102 단계에서 Blender는 원본 import/render/측정에만 사용했다. 파생 모델 제작·Unity 셰이더/프리팹 적용은 아직 하지 않았다.

## 확인 방법
`Concepts/JointSeams/Source~/fbx-proportion-audit.json`의 치수/본/해시와 `audit-models.py`를 참고한다. `Tasks/Active/Task-20261104-102-pursuit-joint-seams/design-brief.md`의 관절 배치표와 원화 오차 기록을 대조한다. CharacterArt 각 `Source~/README.md`에서 정본과 실제 Unity 캡처를 확인한다.
