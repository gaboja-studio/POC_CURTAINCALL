# Integration-document-project

- **Feature:** 프로젝트 개요와 Unity Assets 폴더 규칙 문서화
- **Status:** preparing
- **Branch:** integration/document-project
- **Base:** dev
- **PM:** @MoHoDu
- **Merge Time:** 두 Task 검증 및 공동 리뷰 완료 후 PM 결정
- **Updated:** 2026-09-30

## 목표

- 확정된 프로젝트 정보와 미정 사항을 구분한 개요 문서를 제공한다.
- 현재 Assets 폴더 구조의 규칙과 재점검·갱신 절차를 제공한다.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20260930-001 | feat | @MoHoDu | feat/project-overview | - | scaffolded |
| Task-20260930-002 | feat | @MoHoDu | feat/assets-folder-rules | - | scaffolded |

## 공용 파일 소유 표

| 파일 | 소유 Task | 이유 |
|---|---|---|
| 미배정 | 없음 | setup.md 작업 공간 입력 시 PM이 확정. 두 Task의 수정 경로를 분리한다. |

## Decisions

- Open: 각 Task의 작업 공간 경로는 PM 입력 대기.
- Decided: 문서 Task 두 개로 분리하며 선행 의존성 없이 병렬 구현 가능. 준비는 연속 수행.
- Decided: 2026-09-30 @MoHoDu 요청으로 날짜·번호 없는 작업 브랜치로 변경하고 두 브랜치의 origin 공개를 준비한다. scaffolded 유지, PR·병합은 별도 지시 후 수행.
