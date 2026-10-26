---
name: record-knowledge
description: 함정·확정 결정·사실을 필요한 지식 파일에 직접 기록하고 index를 갱신한다.
---

# record-knowledge

1. knowledge-routing을 따라 함정/결정/사실과 Project/Engine 층을 고른다.
2. index에서 같은 주제를 찾아 기존 파일을 갱신하거나 Knowledge 템플릿으로 파일 하나를 만든다.
3. summary·updated·source를 채우고 언제 읽는지 드러나는 이름과 index 링크를 쓴다.
4. 사람의 확정 결정만 Decisions에 적는다. AI 추천은 plan의 미정 사항이다.
5. 대체된 지식은 superseded와 새 경로를 적는다. PM의 대리 기록을 기다리지 않는다.
6. `pwsh -File Scripts/verify-knowledge.ps1`로 확인한다.
