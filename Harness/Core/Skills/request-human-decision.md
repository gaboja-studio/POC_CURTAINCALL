---
name: request-human-decision
description: 플레이어가 체감하거나 영향이 큰 결정을 사람에게 요청하고 결과를 기록한다.
---

# Request Human Decision

`Harness/Core/Policies/human-decision.md`를 따른다.

1. 막힌 Task 부분을 정확히 적는다.
2. 근거와 아직 모르는 것을 요약한다.
3. 추천안 1개를 먼저 제시하고 영향을 설명한다.
4. 실질적으로 다른 대안만 제시한다.
5. 각 선택이 바꾸는 파일/에셋을 적는다.
6. 묻기 전에 `meta.md`의 `Open Decisions`에 기록한다.
7. 답을 받으면 `Decisions`에 날짜·결정자와 함께 옮긴다.
8. 프로젝트 전체에 오래 유효한 결정이면 `record-knowledge`로 `Decisions/` 폴더에 파일로 남긴다.
9. 안전한 다른 작업은 계속한다. 침묵이나 자동화 출력은 승인이 아니다.
