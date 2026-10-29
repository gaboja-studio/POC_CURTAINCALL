---
name: check-work
description: 개발·수정 묶음 완료나 요청 시 필요한 검사만 한 번 실행하고 log에 결과를 남긴다.
---

# check-work

1. `verification-ladder.md`와 현재 변경을 확인한다.
2. `pwsh -File Scripts/check-work.ps1 [-Base <기준>]`로 안전성·문서와 필요한 Unity 컴파일을 확인한다.
3. 재발 위험에 필요한 자동 테스트는 `-RunTests [-Mode EditMode|PlayMode]`를 선택한다.
4. 문서·도구 작업에는 Unity를 요구하지 않는다. Unity 변경이 있는데 생략했다면 통과로 쓰지 않는다.
5. 실패·환경 제약·미실행·테스트 없음은 그대로 log에 남긴다. 재미·최종 플레이 경험은 사람이 확인한다.
