# Log

- 작업 시작에 origin/dev를 fetch하고 8b0fa5db에서 별도 브랜치를 시작했다. 직전 12건만 DEV 대상으로 PR #477에 제출했다.
- 출발 씬은 EditorBuildSettings와 SpaceRoute를 대조했다. 정식 대상은 HumanCannon.unity, Tightrope.unity, escape_route.unity다.
- 보관한 11건 중 일반 인간대포 함정 접촉 2건과 정식 외줄 불 프리팹 3건을 반영했다. 실제 씬·프리팹에 배치되지 않은 HumanCannonFinalSaws 3건, 정식 탈출로 A와 실패 엔딩에서 참조 누락이 확인되지 않은 3건은 제외했다. 환풍·B·C·튜토리얼 씬은 포함하지 않았다.
- 묘기 도착 판정에는 최신 생존/접속 상태를 쓴다. 실제 도착자가 전부 사망하거나 이탈하면 중간 진행 기록으로 결과를 확정하지 않고 유예를 끝낸다. 도착 위치로 옮기기 전에 매달림·기물 운반을 기존 해제 경로로 정리한다.
- 외줄 화염은 접촉 송신에 아바타와 부활 번호를 포함한다. 이전 생명의 접촉은 버리고, 재접촉에서는 서버의 실제 사망 상태를 기준으로 판정한다.
- 탈출로 A 절단기 지연 잡기는 요청 당시 아바타·재시작·부활 번호와 일치해야 한다. 게스트의 잡기 위치·지면, 발사 방향·높이, 장치 내려놓기 높이에는 호스트가 받은 최신 소유자 자세를 사용한다.
- A 화염 통로의 호스트는 최신 발 위치와 캠페인 참가 자격으로 피해를 판정한다. 출구 목표 안내는 절단기 외형 활성 여부와 분리했다.
- Doctor의 private 에셋 HEAD 불일치 및 기존 Task ID 중복은 유지했다. 기존 사용자 작업이 들어 있는 Assets/_ThirdParty는 수정 범위에서 제외했다.
- 신규 EditMode 최종 7건 통과. 탈출로 A 실제 씬을 열고 별도 테스트 기물·배선으로 참가자 잡기/조준/놓기 및 높은 위치에서 발사 후 절단을 검증했다. 실제 Start 방과 다중 클라이언트 플레이는 실행하지 않았다. 보고서 .harness/local/official-finish-audit-final.xml, official-finish-additional.xml(배선 통과), official-finish-fire-revive.xml(재검사 통과).
- check-work 정적 안전성·문서/규칙 PASS. Unity 단계는 별도 EditMode로 확인하여 정적 명령에서는 SkipUnity를 사용했다. git diff --check 통과. 이 작업 묶음은 로컬 브랜치에 보관 중이다.
- 제출 전 DEV 8f95c4e5 및 병합된 PR #477 반영. 화염 복도의 최신 위치·참가 자격 판정은 DEV에 별도로 반영돼 있어 DEV 구현을 유지했다. 통합 기준 EditMode 7/7 및 컴파일 통과.
