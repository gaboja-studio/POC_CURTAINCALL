---
summary: 팀 구성·PM 계정과 현재 공간 담당 — 공연장·탈출로는 @RagSpirin, 대기실·백스테이지는 @gyumin9048-ctrl
status: active
updated: 2026-10-16
source: human (2026-10-16 PM 에셋 관리자 위임; 2026-10-15 PM @MoHoDu 공연장 담당 변경, 게임 흐름 기획 15564804 재확인)
---

# 팀

## 언제 읽나

담당자를 배정하거나, PM 권한을 확인하거나, `CODEOWNERS`·리뷰어를 정할 때.

## 사실

| 역할 | GitHub 계정 | 비고 |
|---|---|---|
| 개발 PM | `@MoHoDu` | 총괄 시스템·PM 전용 영역 수정·병합 담당 |
| 에셋 관리자 | `@gyumin9048-ctrl` | 2026-10-16 PM 위임: _ThirdParty 추가·수정, private 저장·push, 공개 포인터 저장·제출 |
| 작업자 | `@gyumin9048-ctrl` | 대기실·백스테이지 기획·개발 담당(2026-10-08부터). 표시 이름 "mingyu", 배정·멘션에는 계정 아이디 사용 |
| 작업자 | `@RagSpirin` | 공연장 기획·개발 담당(2026-10-15 변경), 탈출로 담당(2026-10-08부터 유지) |
| 작업자 | `@lakuase2` | 기존 공연장 담당. 과거 작업·기록의 작성자는 바꾸지 않음 |

- 현재 기획서 이름 대응: 모호성 = `@MoHoDu`(총괄 시스템), MG seo = `@gyumin9048-ctrl`(대기실·백스테이지), 주형락 = `@RagSpirin`(공연장·탈출로·결정권자).
- 2026-10-13 확인 당시에는 이수 = `@lakuase2`(공연장), 주형락 = `@RagSpirin`(탈출로)였다. 장소·부상 원문의 옛 담당표보다 현재 PM 지시를 따른다.
- 저장소: `gaboja-studio/POC_CURTAINCALL`
- 역할별 권한: `Harness/Core/Policies/team-roles.md`
- 공간별 브랜치·폴더: `Harness/Project/Decisions/space-workspaces.md`
- 저장소 권한(2026-10-10 당시 확인): POC_CURTAINCALL `@gyumin9048-ctrl`·`@lakuase2` write, `@RagSpirin` admin / CURTAINCALL_PaidAssets `@gyumin9048-ctrl`·`@lakuase2` write, `@RagSpirin` maintain. 담당 변경이 권한 변경을 뜻하지 않는다.

## 확인 방법

현재 담당은 PM 지시·게임 흐름 기획 8장, 권한은 GitHub Settings → Collaborators·`.github/CODEOWNERS`로 각각 확인한다.
