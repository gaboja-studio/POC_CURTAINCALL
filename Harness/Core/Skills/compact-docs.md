---
name: compact-docs
description: 문서가 크기 한도를 넘었을 때 줄인다. 기록형은 History/로 이동, 안내형은 압축·분리. 중요한 내용은 지우지 않는다.
---

# Compact Docs

기준과 원칙: `Harness/Core/Policies/context-budget.md`

1. `pwsh -File Scripts/verify-context.ps1`을 실행한다. 출력의 `[WARN]`/`[HARD]` 줄에 파일, 종류, 처리 방법이 나온다.
2. **기록형** (`task/*`, `integration/*`):
   1. 지금은 필요 없는 과거 내용(끝난 단계, 교체된 계획, 지난 검증 회차)을 고른다.
   2. 같은 폴더에 `History/NNN-주제.md`를 만들어 **원문 그대로** 옮기고, 파일이 생겼는지 확인한다.
   3. 원래 문서에는 현재 유효한 내용과 `History/NNN-주제.md` 링크 1줄만 남긴다.
   4. `handoff.md`는 옮기지 않고 3칸 양식으로 새로 덮어쓴다(스냅샷 문서).
3. **안내형** (정책, Skill, 안내서, AGENTS.md, Domain Map, 지식):
   1. 압축한다: 중복 문장 삭제, 긴 설명은 표로, 예시는 1개만 남기기, 다른 문서에 있는 내용은 링크로 바꾸기.
   2. 그래도 길면 주제별 하위 문서로 쪼개고 원래 문서에서 링크한다(`index.md`가 있으면 갱신).
   3. 옛 버전은 git이 보관하므로 `History/`에 복사하지 않는다.
4. **템플릿**: 설명 문장을 줄인다. 템플릿이 길면 복사된 모든 문서가 길어진다.
5. 지우면 안 되는 것(해결 안 된 문제, 현재 결정, 남은 확인 항목, 디버깅 증거, 확정된 값)이 그대로 있는지 확인한다. 배운 교훈은 `record-knowledge`로 옮긴다.
6. `verify-context.ps1`을 다시 실행해 `OK`인지 확인한다.

권한: `Harness/`, `AGENTS.md`는 PM만 줄일 수 있다. 작업자는 자기 Task 문서만 줄인다.
