# 후속 작업 검증 · 2026-11-16

- Unity 6000.3.25f1의 실제 Start 씬 Play Mode에서 확인. 전체 Unity 재컴파일 오류 0.
- `verify-interactions.cs`: 크레딧 창 중 타이틀 입력 차단. 좌측 5회는 유지, 6회에 좌측 줄 늘어남. 반대편 30회 추가 입력 뒤에도 최초 측면/한 번 낙하 상태 유지.
- `verify-right.cs`: 새 Play 진입에서 우측 5회 유지/6회 우측 낙하 확인.
- `verify-clearance.cs`: 실제 낙하 25.2px, 메뉴 안전 간격 34.85px, 로프 메시 6304 vertices. 회전 후 매 프레임 24px 간격 보정.
- `exercise-chair.cs`/`verify-chair.cs`: 클릭 최대치보다 큰 90 impulse로 밀어 기울어진 의자의 전체 충돌체가 커튼 안전 영역 안에 유지됨. chair back z=5.9505, boundary z=6.62.
- `verify-credits.cs`: 임시 설정 복제로 작업자 추가/삭제 반영, 라이선스 목록 변경, 스크롤, Pretendard 전문 표시 확인. 실제 저장 설정은 바꾸지 않음.
- `verify-raycast.cs` → 렌더 프레임 → `verify-modal-raycast.cs`: 실제 GraphicRaycaster가 타이틀을 선택하고 모달에서는 Inset/Programme이 뒤의 SignHitArea보다 먼저 잡히는 것 확인. 생성과 같은 프레임의 depth 갱신 전 검사는 분리했다.
- 최종 check-work: 안전성/문서/컴파일/공통 설정 PASS. NUnit 자동 테스트는 선택하지 않았으며 위 실제 Play Mode 검사와 구분한다.
- 초기 데이터: MoHoDu, gyumin9048-ctrl, RagSpirin, lakuase2. Pretendard/OFL1.1, Quaternius/CC0, DOTween, DOTween Pro, Synty. 구매 권한/좌석 보유를 추정하거나 보증하지 않음. 모든 외부 에셋의 완전한 라이선스 감사는 아님.
- 라이선스 근거: 저장소 Pretendard-License.txt, Quaternius license.txt, Demigiant readme와 [공식 라이선스](https://dotween.demigiant.com/license.php), [Synty 라이선스 안내](https://syntystore.com/en-gb/pages/licences-overview).
- 크레딧은 `Assets/Resources/Settings/StartMenu/StartMenuCredits.asset` 또는 Project Settings > Curtain Call > Development and Licenses에서 수정한다. 이름/역할/순서/라이선스/저작권 고지/URL/원문 TextAsset을 편집할 수 있다.
- 캡처는 Captures/ 참조. Game View 1920×1080에서 확인. 사람의 최종 움직임 취향 판단은 별도.
- 버전 0.8.1: Start/빈 씬 직접 Play에서 0.8.1_TEST 표시, 단일 영속 HUD, 마이크 아래 좌표, 입력 비차단 확인. 런타임 활성 씬 전환 후 같은 인스턴스 유지. 실제 LIVE/TEST Player 빌드는 사용자가 추후 확인하기로 했다.
