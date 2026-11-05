# Log

## 2026-11-05 구현
- 사용자 캐시 범위 확인: 설문·안내 이력만 지우고 조작·음량·화면 설정, 안내 켜기/끄기, 플레이 로그, 제출된 응답 보존. 설정 기타 탭에서 시작 화면·게임 중 모두 사용, 확인/취소 지원. 런타임과 기존 Editor 메뉴가 같은 marker 삭제 규칙 사용.
- doctor 1회: 샌드박스에서 Unity·외부 에셋 감지 실패와 기존 Task ID 중복으로 FAIL 3. 실제 권한 환경에서 Unity 6000.3.25f1·CLI 연결 및 외부 에셋 gitlink 일치 확인. 최신 origin/dev eb086e19에 새 feat 브랜치, 기존 미커밋 변경 보존.
- 쉬는 시간 구매 기본 허용/설정으로 비활성 가능, 신체 복구 부위당 50/+25. 아침·낮·가이드 300초를 SerializedObject로 저장, 밤 정비 unlimited/120 유지.
- 사망 배너 2곳 개인 호응 절반으로 수정. 성공 정산에서 연결된 전 생존자 완주·보너스 지급, MaximumDistance/FinalDistance는 수정하지 않음. 종전 80% Inspector 항목은 호환 데이터로 숨김.
- 정찰 조사: WaitingRoom 순회 112구간 완전 경로, 링크28개 직선 충돌0. Stop으로 실제 경로가 없어져도 목표 캐시만 남아 재출발하지 않는 결함 보완, 낡은 경로 재계산 및 SetPath 실패 반영. 기존 우회/부분 경로 목표 보정 유지.
- 쉬운 가이드: Docs/Guides/playtest-adjustments-guide.md. CurtainCall → 게임 조정 메뉴 2개로 실제 설정 에셋 바로 열기. 관련 경제·부상·게임 흐름 문서와 정찰 지식 갱신.

## 회귀 검증
- EditMode 94건 통과: 정찰 경로3, 설문 정책21, 대포29, 보상 모델39 및 새 실패 공연 생존자 완주 미지급2. 모델의 종전 보너스 기대값은 새 규칙으로 갱신 후 재검증.
- PlayMode 7건 통과: 실제 대기실 정찰 경로 재개1, 쉬는 시간 UI·서버 구매/설정 비활성 거절1, 복구3, 캐시 버튼 시작 화면·확인/취소1, 완주 대상 확대로 공연 조기 종료 금지1. 복구의 종전 100원 기대값으로 실패한1건은 50원/잔액950으로 갱신 후 통과.
- 신규 정찰 경로 테스트의 첫 fixture는 EditMode에서 Agent.Warp를 호출해 실패. 경로 계산은 EditMode, 실제 Agent 재출발은 PlayMode로 분리해 통과.
- Unity 컴파일 오류0. verify-knowledge OK. 최종 `check-work.ps1 -Base origin/dev` 1회: 안전성·문서/규칙·컴파일 PASS. 자동 테스트 일괄 실행은 선택하지 않았으며 위의 관련 101건을 별도로 실행했다. 기존 문서 길이 WARN 56·ThirdParty 미커밋2건은 보존, 검사 실패 없음.
- Editor 종료 확인 뒤 escape-labels.asset을 시작 전 백업으로 복원. SHA256 D47C06125FDC23D7978DA433677FCEB79BADE30CD972734F26929A1C4B1459DA 일치. 기존 사용자 변경을 보존하고 테스트 중 동적 글리프 갱신만 제거.
- 사람 확인: 2~4인 최종 플레이 경험, 실제 제보 장소의 정찰 동선. 빌드 생성·외부 브라우저 설문 재제출은 실행하지 않음.
- 결과: 구현·관련 자동 검사 완료. 후속 사용자 요청으로 최신 dev 동기화 후 commit/push 및 dev PR 제출 진행.

## 제출 준비
- 사용자 요청: 최신 dev를 받은 뒤 충돌 시 양쪽 최신 구현을 보존해 해결하고 commit/push/dev PR. 미저장 씬0·Play 중지 확인 후 Editor 정상 종료.
- origin/dev fetch 및 작업 브랜치 반영 결과 eb086e19로 동일, 추가 변경·충돌 없음. 소스·설정은 직전 검사와 같아 컴파일·관련101건 결과 재사용.
- 기존 escape-labels.asset SHA256 D47C06125FDC23D7978DA433677FCEB79BADE30CD972734F26929A1C4B1459DA와 ThirdParty Lit/LitTransparent shader 2건은 보존하며 PR에 포함하지 않는다. 관련 파일만 명시적으로 stage한다.
- 제출 전 verify-scope 및 verify-fast PASS. 기존 문서 길이 WARN·ThirdParty 2건 보존 경고 외 실패 없음. 게임 코드·설정은 이전 check-work 및 관련101건 검사 시점과 동일하다.

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
