# Task-20261119-167

- **Title:** 탈출로 사운드 에셋 도입과 이벤트 연결
- **Type:** feat
- **Assignee:** @RagSpirin
- **Branch:** feat/escape-route-sound
- **Base:** dev

## Context
다운로드한 Pro Sound Collection을 비공개 Assets/_ThirdParty에 도입하고, 사운드 리소스 시트의 '탈출로 전용' URL 열에서 선별한 사운드를 탈출로 이벤트에 연결한다. 초기 18개 항목·24개 클립에서 사용자 청취 후 반복 공장 배경음을 제거해 현재 17개 항목·23개 클립을 사용한다. 담당은 기존 탈출로 담당 @RagSpirin이다.

## 계획
1. 기존 공개·비공개 변경을 stash로 보존하고 Unity 종료 후 최신 origin/dev 기준 feat/escape-route-sound 및 resource/escape-route-sound를 준비한다.
2. 원본 GUID와 importer 설정을 유지하며 패키지 경로를 Assets/_ThirdParty 아래로 옮겨 Unity로 가져온다.
3. 시트의 지정 클립을 추격자·소실 외줄·화염 경고·접이교·톱 발사·출구·공장 배경음의 기존 상태 표시 흐름에 연결한다.
4. 일반 진입점과 재시작·클라이언트 표시·설정 음량을 확인하고 check-work 결과를 기록한다.
5. 사용자 청취 피드백에 따라 반복 배경음 ESC-028을 제거하고 추격자 발소리는 유지한다. 외줄 전기음은 플레이어 8m 이내의 보이는 외줄에서만 재생하며 재생 중에도 벽 차폐·이탈 시 멈춘다.

2026-11-19 사용자 추가 지시. 로컬 작업에서는 전체 Pro Sound Collection을 유지한다. 제출 요청 시 실제 사용하는 클립과 `.meta`·필요 폴더 메타만 선별해 private 커밋·제출에 포함한다. 전체 라이브러리를 일괄 stage하지 않는다.

## 미정·주의
시트는 읽기 전용이다. URL이 빈 17개 항목은 이번 선별 기준에 포함하지 않는다. 보류 상태인 ESC-015·016·022는 클립과 호출 지점을 준비하고 카탈로그의 재생을 꺼둔다. 사운드 설정에서 활성화할 수 있다. 플레이 감상과 루프 적합성은 사람 확인이 필요하다. 게임 판정·GameSettings 규칙은 기존 구현을 따른다.

기존 변경은 양쪽 저장소의 before-escape-route-sound-20261119 stash로 보관했다. 외부 에셋은 전부 private 서브모듈에만 저장한다. 사용자가 수정 후 저장·제출을 요청했으므로 필요한 클립을 선별해 private main에 저장하고 게임 코드·포인터를 dev 대상 PR로 제출한다.
