---
summary: 대기실 HUD의 키 치환과 투표 버튼의 말줄임을 고칠 때
status: active
updated: 2026-11-13
source: Task-20261028-077 UI 고도화, Task-20261112-119
---

# 역할 HUD 문구 표시

- 대기실 문구 표는 인원·줄 번호 등 호출자가 제공한 값만 채운다. `{forward}` 같은 조작 키는 화면에 표시할 때 로컬 입력 바인딩으로 치환한다. Inspector의 제목과 본문은 같은 키 규칙에 기본 키를 넣어 미리 본다. `{forward}`는 앞·뒤 이동, `{balance}`는 양쪽 자세 조작, `{move}`는 둘을 표시한다.
- Vote의 두 버튼은 공용 Button_General의 중첩 인스턴스다. 투표 버튼은 공통 HUD 줄바꿈 전처리에서 제외하고 자동 크기와 `NoWrap`으로 맞춘다. 키 접두사가 붙은 실제 문구로 TMP의 줄 수와 잘림 여부를 확인한다.
- TMP의 일반 줄바꿈은 한국어 음절 사이에서도 끊길 수 있다. HUD는 표시할 때 어절을 보호하고 `[1/2]` 같은 숫자 현황만 별도 줄로 보낸다. `GetPreferredValues(string, ...)`는 TMP 전처리를 거치지 않으므로 동적 크기는 `HudText.Measure`로 계산한다. `TMP_Text.text`에는 조작 키와 현황을 포함한 원문을 그대로 보존한다.
