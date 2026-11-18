# Log

- 2026-11-18: investigate-bug / unity-pipeline 절차 적용. 시작/재개 fetch origin/dev, ff-only 동기화 완료(변경 없음). 작업 브랜치는 계속 dev.
- doctor 1회 실행: 기존 Task ID/상태 중복, 외부 에셋 상태, Unity 설치 탐지 FAIL. 샌드박스 밖에서는 서브모듈 clean, Unity CLI 실행 가능. Task 중복은 이 핫픽스 범위 밖이며 별도 수정하지 않음.
- 원인 확정: structure-a:stairs-door의 hiddenVisuals/hideOnOpen 목록에 양쪽 문짝이 중복. Update가 매 프레임 켰다 꺼서 PursuitDoorObstacle.OnDisable에서 무효화 2회/프레임. Collider 크기 갱신 가설은 LateUpdate 단독 호출로 기각. 사용자 첨부의 348회/450~520ms 연속 Build 자체는 이번 환경에서 재현되지 않았음(초기 Build 186ms).
- 수정 전: 닫힌 문 10.017초/423프레임/무효화 0회, 열린 문 10.030초/417프레임/무효화 834회. frame median 21.37ms, p95 35.76ms. 긴 게임 delta 시뮬레이션에서도 요청은 지속됐지만 연속 Build는 없었음. 요청→Build 실행은 시간·호출 순서·오르는 개체 유무에 좌우됨.
- 수정: 뒤 목록에서 상태를 정할 대상은 앞 목록에서 SetActive하지 않음. showOnOpen > hideOnOpen > hiddenVisuals 기존 우선순위와 슬라이드 완료 조건 보존. 재계산 기능/게임 설정/씬 참조 변경 없음.
- 수정 후: 열린 문 10.011초/395프레임/무효화 0회/Build 0회. frame median 22.54ms, p95 40.06ms. 일반 FPS 향상을 주장하지 않으며 반복 무효화 제거를 확인. 실제 개방에 따른 Build는 샘플 직전 정상 1회(202.8ms).
- Unity 6000.3.25f1 컴파일 오류 0. PlayMode 2/2 PASS: 실제 계단 문 개방·닫기/재개방·개방 상태 보존 재진입(29.93초), 기존 개방 조건/암호/보상(12.59초). Knowledge 검사 PASS.
- check-work 1회: 안전성/문서·규칙/컴파일/공통 설정 PASS. 자동 테스트는 별도 위 2건 실행. 기존 문서 길이 WARN 78건(하드 오류 없음).
- 제출 직전 에디터 정상 종료 후 fetch/ff-only: dev 19b09e68 → c546c695(다른 담당자의 공연 기물 변경). 핫픽스 코드와 충돌 없음. 새 dev 컴파일 오류 0 및 실제 MPPM 추가 검증 완료.
- MPPM: Main Editor + Player 2 별도 프로세스, 실제 LAN 2인 접속. 참가자 빗장 개방 동기화와 공연→대기실 복귀 후 문/플레이어 상태 확인. 양쪽 각 15초씩 총 4개 샘플 모두 무효화/Build/오류 0. 상세 수치 및 별도 치트 공연 배치 누락(미해결)은 verification.md. 전체 2인 플레이 무결성 PASS로 확대 해석하지 않음.
- 검증 종료: 두 에디터 정상 종료/원래 PlayMode 구성 복원. 임시 시나리오/프로브 삭제, 자동 폰트 캐시 4개와 재질 부동소수점 오차 7개 원복. 사용자 씬/설정 diff 없음.
- 최종 origin/dev fetch 동일 c546c695. 최신 dev가 지정한 외부 에셋 701a1bbe로 clean 서브모듈 동기화(기존 25f9cdee 대비 공연 기물 시각 프리팹/재질 추가만, C#/asmdef/DLL 차이 없음). 작성자는 gh 계정 MoHoDu. 요청대로 dev 직접 제출, PR 생성하지 않음.
