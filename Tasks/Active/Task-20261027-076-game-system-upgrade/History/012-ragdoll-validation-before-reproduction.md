# Hanging 잔류 재제보 전 검사 기록 원문

아래는 2026-10-29 재제보 전 기록이다. 이전 PASS는 충분한 관절 운동이나 절단 조각의 스킨 변형을 보장하지 못했다.

- 검사: Compile PASS. Humanoid13/13(매달림 사망 즉시 중지·관절 운동·낙하·멱등·절단/복구), 실제 외줄/대포 DeathRagdoll2/2, CameraViewsPlayTests4/4(고정·Brain 출력·관전 지연·복귀·씬/disable 정리) 통과. 최초 새 매달림 검사에서는 자유 낙하 시 팔 상대 변화가1cm라 실패하여 작은 초기 회전 속도를 보완한 뒤13/13 재통과. 다른 기준을 완화하지 않았다. 10-29 Humanoid14/14, 실제 외줄 호스트의7초 만료→줄 이탈·손 접점 검사 통과. 접점 검사의 최초26cm 실패는 LateUpdate 이전 측정 탓으로 EndOfFrame 측정으로 수정(한계값 유지), 최종12샘플 손 오차2cm 이내. 기존 외줄/대포 사망2/2도 통과했다.
- 최종 check-work(-NoRecord): 안전성·문서/규칙·컴파일 PASS(up_to_date), hard0·길이 경고16·dirty 씬0. 전체 테스트 SKIPPED, 관련19건 개별 통과. 최초에는 Task log/todo 길이 hard2로 문서 FAIL·컴파일 SKIPPED였으며 완료 원문을 History/011로 보관 후 재통과했다. diff-check 오류0. 10-29 후속 최종 check-work도 안전성·문서/규칙·컴파일PASS(hard0·경고16·dirty0), 전체 테스트SKIPPED·관련17건(Humanoid14+실제 사망3) 개별 통과.
