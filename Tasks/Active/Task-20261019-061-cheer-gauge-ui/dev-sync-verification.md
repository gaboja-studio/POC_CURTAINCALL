# 2026-10-20 dev 수신 검증

- 사용자 확인: Start 씬에서 시작해 Tightrope HUD를 확인했으며 전체적으로 잘 적용된다고 보고했다. 다인·재접속 등 개별 항목 검증까지 확대 해석하지 않는다.
- 사용자 요청으로 origin/dev 최신3a2bc88을 feat/ui-scene-integration에 일반 merge. 수신용 merge commit a474bac, 작성자 MoHoDu. 구현 커밋/push 없음.
- merge-tree 사전 점검과 실제 merge 충돌 없음. 기존 수정·신규62파일 임시 stash 후 복원. Decisions/index.md는 dev의 추가 항목과 기존 로컬 설명을 자동 병합, 나머지61파일은 저장된 Git blob과 일치.
- 백업 정보: .harness/local/dev-sync-backup.json. stash는 복원 후에도 복구용으로 유지한다.
- private gitlink/HEAD는2b67e36d 유지(수신 dev의6030fe1 포함). 수신 전부터 있던 서드파티 import 변경은 그대로 보존했다.
- 주요 유입: 대기실 물리 이동/부활 보호, Vivox 음성 채팅, 플레이어 휴머노이드·이동·입력 개선. HUD 구현 자체의 충돌 없음.
- 첫 컴파일: FAIL(Vivox namespace 없음). 유입된 manifest의 com.unity.services.vivox16.12.1을 에디터가 아직 로드하지 않아 PackageManager.Client.Resolve로 재해석.
- 최종 컴파일 PASS(up_to_date), HUD PlayMode9/9 PASS. 패키지 manifest/lock 버전 변경 없음. 실제 Start→공연 흐름·다인·음성 통화는 병합 후 재실행하지 않았다.
