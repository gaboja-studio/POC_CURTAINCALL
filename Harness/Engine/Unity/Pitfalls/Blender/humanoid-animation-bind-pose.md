---
summary: 리그만 내보낸 Humanoid FBX는 유효한 Avatar여도 첫 동작이 기준 자세가 되거나 축이 틀어질 수 있다
status: active
updated: 2026-10-30
source: Task-20261030-083
---

# Humanoid 동작의 바인드 자세와 축

## 언제 읽나
Blender에서는 엎드린 동작인데 Unity에서는 서 있거나 90도 돌아가고, Avatar 검사 자체는 통과할 때.

## 원인과 대처
Rabbit v14에서 리그만 FBX로 내보내 CreateFromThisModel로 임포트하면 첫 애니메이션 포즈가 기준 자세가 되어 골반 기울기가 사라졌다. CopyFromOther만 바꾼 리그 단독 FBX도 축이 어긋났다.

원본과 동일한 리그 이름·단위·축을 유지하고, Hips에 가중치를 준 작은 자체 제작 bind marker 메시를 함께 내보내 바인드 행렬을 보존한다. 검증 설정은 RabbitRig_v14, -Z forward/Y up, FBX_SCALE_UNITS, ARMATURE+MESH, deform bones, no leaf bones, all-actions/NLA off다. Unity는 검증된 RabbitV14 sourceAvatar를 CopyFromOther로 사용하고 root motion은 끈다. marker는 게임 프리팹에 생성하지 않는다.

## 확인
CopyFromOther는 FBX 내부 Avatar가 없을 수 있으므로 importer.sourceAvatar를 검사한다. Avatar.isValid만으로 완료하지 않는다. 실제 네 캐릭터에서 머리-골반 방향, 손·발 이동량, 루프 경계, 손잡이·포구 접점, 절단 표면의 접지를 검사한다. 최종 LateUpdate 정렬은 더 늦은 실행 순서의 probe로 측정한다. Update 코루틴에서 재면 지난 프레임 보정이 해제된 중간 자세를 읽을 수 있다.
