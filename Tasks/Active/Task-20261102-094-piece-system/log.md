# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-02 에셋 저장(사용자 요청): DOTween(Pro) 업데이트·All In 1 Sprite Shader를 private main(80ef93ad)에 push, 공개 포인터·DOTween 설정 커밋 11d90316 push. 가져오기가 만든 빈 `Assets/Plugins` 폴더는 외부 에셋 검사에 걸려 지움. `escape-labels.asset`(TMP 동적 아틀라스 변화)은 무관해 제외.
- 2026-11-02 구현: 쿨타임 0·스프링 신발 2, 소지 칸 1.5배·내구도 바·사용/파괴 연출(`PropCarry.LastUse` 동기화). 머티리얼 `_ThirdParty/curtaincall/InventorySlotFx/InventorySlotFx.mat`(private 미저장).
- 검사: 컴파일 PASS. PlayMode W2_ 13/13 PASS(새 W2_InventoryDurabilityBarAndUseEffect 포함), Performance_ 3/3 PASS. 같은 필터에 걸린 `WaitingRoomPropCollisionPlayTests` 의자(ExpertRoom WoodenChair) 1건 FAIL — 이번 변경과 무관.
- 테스트 중 ScreenCapture가 저장되지 않아(에디터 비포커스 추정) 임시 카메라 렌더로 칸 상태 3종을 확인함.
- 테스트 실행 뒤 Pretendard SDF 폰트 에셋 2개가 동적 아틀라스로 바뀜(작업 무관, 되돌리지 않음).

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-02 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/262 (fix/piece-system → dev)
