# Log

- 2026-11-14: scaffold-task로 현재 fix/ui/schedule 브랜치에 Task 생성. commit/push 없음.
- doctor 1회 FAIL: private submodule 접근/미초기화 판정, 기존 Task ID 중복 13건, Unity 설치 미검출. 추가 권한으로 읽기 확인 결과 실제 6000.3.25f1 에디터가 POC_CURTAINCALL/Start를 열고 있고 private 파일도 존재한다. 에디터 미설치 및 submodule 미초기화는 확정 사실이 아니며 sandbox 접근 차이를 먼저 확인해야 한다. gh 로그인 경고도 기록. 실행 테스트는 아직 하지 않았다.
- origin/dev fetch 성공, HEAD...origin/dev=0/0. 열린 Unity를 종료하거나 작업 트리를 병합/체크아웃하지 않았다. 기존 폰트 3개 및 escape-labels.asset 변경 보존.
- 구조: Assets/Resources/Prefabs/UIs/Roles/Retained/ScheduleUi/SchedulePanel.prefab → WaitingRoomScheduleInstaller → WaitingRoomScheduleUI → WaitingRoomScheduleUI.Board의 월드 Canvas 복제. 3일×2공연, 공용 버튼을 카드당 56×28 크기 3개로 런타임 생성. 선택 색상 외 카드 수준 피드백 부족, 보상 한 줄 표시.
- 규칙: ScheduleBoard.Set 성공 시 장부/런 적용 및 Publish, 선택 즉시 적용. CanEditTier는 호스트·2일차 이상·CanEdit를 요구하고 MaxTier는 런 해금 상태를 따른다. 최근 코드는 확정 도장 및 확정 버튼을 숨긴다. 첨부 구 화면을 현행 규칙으로 오해하지 않는다.
- 가림 원인 후보: WorldSpace Canvas, ScheduleBoardView.HideLocalModel이 로컬 캐릭터만 숨김. GraphicRaycaster.blockingObjects는 이미 None이므로 클릭 방해 원인은 실제 EventSystem/PhysicsRaycaster·카드 히트 결과를 테스트해 확정해야 한다.
- built-in GPT Image로 A(공연 장부), B(공연 티켓) 생성·표시. 시안 A의 점 개수 일부 불일치와 시안의 경계 화살표 표현은 실제 구현에서 데이터로 교정할 항목. 제작용 텍스트는 TMP로 분리한다. 승인 시안과 제작 프롬프트는 private 키트에 보관한다.
- 다음: 사용자 시안 피드백을 받은 뒤 스프라이트 제작 키트부터 진행. 게임 코드/프리팹 변경·Unity 테스트·테스트 캡처는 미실시.
- 사용자 피드백: 공연명이 가장 크게 유지되어야 함. 화살표는 난이도 변경, 화살표 외 슬롯은 공연 변경이라는 기존 조작 유지. B 기반 공연 중심 수정안을 GPT Image로 생성해 표시했다. 이전 A/B는 비교 이력이며 승인된 제작 방향이 아니다. 수정안은 공연명 대형/난이도 소형, 슬롯 hover 외곽선과 화살표 hover 분리, 적용됨 인라인 표시. 실제 구현 시 부모 슬롯과 화살표 이벤트 중복 실행 방지 검증을 포함한다.
- 사용자 최종 수정안 승인 후 GPT Image 원본5개·Sprite15개 제작, private curtaincall_schedule_v2 키트/프롬프트/카탈로그/Unity 패키지(9,394,858bytes) 생성. SchedulePanelV2 프리팹과 전용 폰트를 Unity CLI로 조립, CommonWaitingRoomUI 씬 및 툴팁 참조 교체.
- 공연명62pt/난이도30pt, 슬롯·화살표 클릭 독립, 성공 시1.5초 피드백. 포커스는 동일 Canvas를 Overlay110으로 전환하고 닫으면 월드 부모/위치/크기로 복귀. 다른 캐릭터 Renderer/Collider/네트워크는 변경하지 않음. 개발 음성 IMGUI도 focus hold에 맞춰 숨김.
- 실제 포인터·가림 모델/콜라이더·난이도 경계·닫기/재열기 PlayMode 1/1 PASS, 밤→아침→쉬는 시간 기존 PlayMode 1/1 PASS. 난이도 EditMode는 최신 튜토리얼 완료 신호를 fixture에 보완 후5/5 PASS. 초기 실패와 해결·결과 JSON 및 캡처는 verification.md 참조.
- verify-knowledge PASS. 최종 check-work 안전성/문서/컴파일/common settings PASS. 이 명령의 자동 테스트는 선택하지 않았으며 별도 실행7/7 결과와 구분. 현재 콘솔 오류0/컴파일 실패false, 기존 경고19.
- 최종 origin/dev fetch 후 HEAD 차이0/0. Unity Start 씬·dirty0·PlayMode stopped로 복귀. auto-tick은 작업 전부터 enabled였고 유지. commit/push/PR/다른 사람에게 메시지 전송 없음. 기존 미저장 에셋 변경은 되돌리지 않음.
- 구현·로컬 검증 완료. 실제 2PC/다중 클라이언트 네트워크 세션 및 사람의 최종 조작감 검증은 미실시이며 자동 검사 PASS로 포장하지 않음.
- 2인 후속 검증: 동일 PC의 독립 Unity 인스턴스 2개를 LAN 연결해 공연·난이도 수신과 참가자 읽기 전용을 확인했다. 실제 접속 캐릭터의 판 앞 교차 시 호스트 화살표 입력과 2단계 적용 PASS. 세부 기록은 multiplayer-verification.md. 별도 PC·Relay 미실시.

- 2026-11-14 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-14 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-14 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)
