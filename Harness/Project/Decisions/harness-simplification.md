---
summary: Task 3문서, 작업 구역·PM 인계·브랜치 계층 폐지, 묶음 완료 시 필요한 검사
status: active
updated: 2026-10-27
source: human (Confluence 하네스 구조 수정, 계획 보고 후 진행 승인)
---

# 하네스 간소화

## 언제 읽나
작업 흐름·권한·도구·기존 Task를 바꿀 때. 이 결정은 이전 구역·순차 인계·부모 자식 제한보다 우선한다.

## 결정
- Task는 plan/todo/log만 필수. plan에 목적·담당·Branch·선택 Base를 적고 계획은 작업자가 갱신한다.
- Active/Done으로 진행 여부 구분. Status·setup·handoff·배정/실제 인계·Jira·공동 테스트 계획을 착수 조건으로 삼지 않는다.
- 작업 구역·예약 구역·수정 금지 경로·PM 전용 하네스·스킬/MCP 제한 폐지. 목적 안의 기능 연결·버그 수정 가능.
- 작업별 브랜치와 역할 이름 유지. integration 필수 경유와 부모 자식 공유 제한 폐지. dev는 PR 1회와 지정 팀원 검토 유지.
- 겹치는 수정은 알리되 차단하지 않는다. AI의 외부 메시지는 사용자 요청 때만 보낸다.
- 개발·수정 묶음 완료나 사용자 요청 시 필요한 검사만. 재미·방향·최종 플레이 판단은 사람에게 맡긴다.
- 에셋 추가·활용은 자유. 큰 수정·삭제는 PM·에셋 관리자가 관리. 외부 에셋 private 보관·비밀값·meta 보존 유지.
- 기존 Active Task의 원문은 History/Harness-v1에 보존하며 현재 목적·남은 일·주의점으로 연결한다. Done 기록은 보존한다.

## 근거
- [하네스 구조 수정](https://hrjoo122770.atlassian.net/wiki/spaces/bYmkRlnpusng/pages/26673155)
- 2026-10-27 사용자: 수정 계획에 “좋아 진행해”.
