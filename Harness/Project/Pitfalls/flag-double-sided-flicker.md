---
summary: 앞뒤 면이 이미 있는 공연장 깃발에 Cull Off를 적용하면 같은 위치의 면이 중복 렌더링되어 깜빡인다
status: active
updated: 2026-10-30
source: Task-20261020-067; HangingVelvet 메시의 앞/뒤 좌표·법선과 수정 전후 Scene 렌더 확인
---

# 공연장 깃발 깜빡임

## 언제 읽나

공연장 깃발이 카메라/흔들림에 따라 밝고 어두운 면으로 번갈아 표시될 때.

## 원인과 수정

- `HangingVelvet` 메시에는 같은 위치의 앞/뒤 정점 8개와 삼각형 4개가 있다. 앞뒤 법선은 서로 반대다.
- 원본 `Velvet_FadedCrimson`과 `OldCircusVelvet` 재질은 URP Lit `_Cull=0`(Off)였다. 명시적 앞/뒤 면을 각각 양면 렌더링하여 깊이가 겹친다.
- 두 재질을 `_Cull=2`(Back)로 설정하면 카메라를 향한 면만 렌더링한다. 명시적 뒷면 기하가 있어 깃발은 여전히 양쪽에서 보인다.
- 원본 메시·흔들림·조명 세기를 수정할 필요가 없다. 앞뒤 삼각형의 대각선이 다르면 좌표 3개가 완전히 일치하는 중복 삼각형 검사만으로는 겹침을 찾지 못한다.
- 단면 메시에는 같은 처방을 무조건 적용하지 않는다. 먼저 실제 뒷면 기하가 있는지 확인한다.

## 대상

- `Assets/_ThirdParty/curtaincall/PerformanceBackground/Materials/Velvet_FadedCrimson.mat`
- `Assets/_ThirdParty/curtaincall/PerformanceBackground/Undercity/OldCircusVelvet.mat`
