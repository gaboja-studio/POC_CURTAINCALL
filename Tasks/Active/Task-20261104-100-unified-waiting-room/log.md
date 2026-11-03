# Log

## 2026-11-04
- 최신 origin/dev 4be086c6 확인, 현재 브랜치에 이미 포함. 미저장 씬 없음/정상 종료 후 feat/waiting-room/unified-scene 분기. 기존 Task-099 커밋을 보존했다.
- doctor: 기존 Task 번호 중복 8개 및 설치 Unity 탐지 실패. 실제 Unity 6000.3.25f1/Pipeline 연결·CLI 최신 beta.15는 확인됨. 이 기존 문서/설치 탐지 문제는 씬 구현을 막지 않는다.
- 원본 YAML 읽기 비교: 공통 119,948개 오브젝트, Base 전용 0개, Pursuit 추가 786개. 공통 차이는 추격 문 장애물 컴포넌트, 모니터 Transform 4개, 씬별 NetworkObject 해시다. 리허설은 공통 런타임으로 생성된다.
- Unity AssetDatabase.CopyAsset로 Pursuit를 새 WaitingRoom 씬으로 복제하고 Base의 PracticeMonitor와 자식 3개 Transform을 이식, 에디터 저장·빌드 씬 등록. 원본 참조/GUID 보존.
- Pipeline eval은 큰 씬 처리 중 5초 HTTP main-thread timeout을 반환했으나 작업은 완료되었다. 완료 파일·생성 씬을 확인했으며 중복 실행하지 않았다.
- WaitingRoomScene 필터 EditMode 최종 7/7 PASS. 첫 6/7은 새 테스트의 빈 SceneManagerSetup 복구 처리 실패로 수정했다. 세 가지 씬 라우팅/음성 호환/빌드 등록, 배치 위치·회전·타입·활성 단계·순찰 경로, 방 조명·스폰 참조, 누락 스크립트와 단일 카메라/UI 바인딩을 검사했다.
- UnifiedWaitingRoom 필터 PlayMode 최종 2/2 PASS. 첫 0/2는 공통 테스트 도우미가 두 기존 이름만 대기실로 구분한 실패로 세 번째 이름을 추가했다. 최종: 실제 Dev 시작/호스트, 리허설 5단계 완료와 리허설 중 추격 금지, Base 최신 모니터 크기, 카메라 mouse-look, 메뉴 열기/닫기·인벤토리 표시, 추격 단계/무적/인식/타이머 초기화 확인. 결과는 editmode-results.json, playmode-results.json.
- 통합 씬에서 추격 배치 창의 Director가 현재 씬인 것을 확인했다. 실제 Authoring.Add → SaveScene → 재열기 → room.Placements 등록 유지 → Remove → SaveScene 성공. 검사용 배치는 제거했다. 원본 두 씬은 변경되지 않았다.
- verify-knowledge PASS. 실제 다인 네트워크, 사람의 최종 조작감/시각 QA는 이 단일 호스트 자동 회귀의 검증 범위 밖이다. Task-099 실측을 완료했다고 간주하지 않는다.
- 최종 기본 설정 waitingRoomVariant=2/Resolved WaitingRoom, 추격 Enabled=true. 통합 씬과 추격 배치 창을 에디터에 열어 두었다. 이번 요청은 구현·연결 범위로 로컬 저장했으며 별도 커밋/푸시하지 않았다. 검증 중 생성된 글꼴 캐시와 두 재질의 미세 부동소수점 변화는 기능 변경에 포함하지 않는다.

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 제출: 최신 dev 4be086c6 기준 통합 변경만 분리, 커밋 9cd4422f push 완료. PR https://github.com/gaboja-studio/POC_CURTAINCALL/pull/280 (base dev). 기존 관련 없는 변경 보존. GitHub 커넥터 403으로 gh CLI 사용.
