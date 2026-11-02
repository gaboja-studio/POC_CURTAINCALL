---
summary: 대포·그네 C6 조작 안내의 한글 누락과 긴 키·본문 잘림을 보수할 때
status: active
updated: 2026-11-02
source: Task-20261027-073 사용자 UI 범위 정정·NotificationTextLayoutTests
---

# 대포·그네 안내 글리프와 텍스트 영역

## 언제 읽나
- 탈출로 대포/그네 앞의 조작 안내에서 한글이 빠지거나 설명/재지정 키가 박스 밖으로 나갈 때.

## 원인과 보수
- 대포·봉은 C6 CommonInteractionUI다. A에는 HumanCannonUiBinding/C7이 없으므로 대기실·장비 알림이나 일반 시스템 배너를 바꾸지 않는다.
- 기존 C6의 최대3줄 말줄임과 고정 키 폭은 긴 기믹 설명·재지정 키를 자를 수 있다. HumanCannonStation/Pole 대상일 때만 SetDeviceText로 전체 본문 높이·키 폭을 맞춘다. 다른 대상일 때 원래3줄/키 폭/기본 박스 치수로 돌린다.
- 표시할 실제 문구를 UiRuntimeFontFallback의 런타임 복제 폰트에 미리 추가한다. 기존 fallback 자체를 쓰는 라벨도 이 대상에는 복제하고, 동적 글리프/추가 atlas를 허용한다. 원본 폰트와 외부 에셋은 수정하지 않는다.
- 회귀는 긴 키/본문→일반 안내 복귀, 한글 글리프 완비/원본 폰트 불변을 포함한다. 프리팹 편집 렌더는 실제 Play와 구분한다.
- 후속 공통화: 대포 탑승/봉 점유 중에도 C6가 발사·내리기·반동·도약을 안내한다. 전원/포신이 준비되지 않으면 발사 대기로 표시하고 내리기는 유지한다. 원본 HumanCannonUiBinding의 C7은 C6가 있을 때 중복 표시하지 않으며 C6가 없으면 기존 안내를 유지한다.
- 활성화 상태 토스트는 C6와 별개인 EscapeMechanismFeedback/ToastView다. a-mechanism-notifications 정적 폰트에는 실제 체크포인트 이름의 ‘부·양·쪽’이 없었다. 실제 sourceName/effectName/prompt/instruction과 기물 이름을 대조한다. A 토스트를 명시적 WarmUpLocalText 대상으로 넘기며, 이 호출만 부모 타입 목록 검사를 건너뛰고 전체 문장 높이를 허용한다.
- 정적 HasCharacters 실패만으로 깨짐을 확정하지 않는다. 동적 sourceFontFile 또는 실제 실행 시 fallback을 적용한 뒤 검사하고 원본 atlas는 오염시키지 않는다. 현재 A 동적 문구529종 문자를 C6/활성화 토스트의 실행 시 폰트로 확인했다.
