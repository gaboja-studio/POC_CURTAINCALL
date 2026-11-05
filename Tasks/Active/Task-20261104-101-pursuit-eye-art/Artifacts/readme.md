# 추격자 눈 아이콘 — 컨펌용

- `eye-white-atlas.png`: 1024×512 투명 RGBA, 4열×2행, 셀 256×256.
- `eye-00.png` ~ `eye-07.png`: 개별 프레임. `eye-closed.png`, `eye-open.png`: 일반/경계·추격 정적 상태.
- `eye-animation-preview.gif`: 흰색 눈 열림 0.5초 → 유지 → 닫힘 0.5초. GIF의 어두운 배경은 검토 전용. 작은 아이콘은 축소 가독성 확인용.
- `eye-contact-preview.png`: 같은 시트를 어두운 배경에서 확인.
- `eye-source-gpt.png`: GPT 생성 원본. 검은 배경이며 임포트 대상 아님.
- `prepare-sprites.py`: 사용자 승인 후처리. 원본 형태를 추출하고 순백 RGB+알파로 변환, 눈 양끝을 기준으로 중심 정렬. Pillow 필요.
- `sprite-manifest.json`: Unity 하단 원점 기준 셀 좌표와 재생 순서.

## 컨펌 후 적용 예정

Sprite (2D and UI), Multiple, 256×256 Grid by Cell Size, 중앙 pivot. 위 행부터 좌→우로 0~7. 첫 시안 기준 Filter Bilinear, Mipmap Off, Compression None으로 확인한다. 16fps의 8프레임 클립은 0.5초이며 마지막 상태를 유지한다. 닫힘은 역순. 색은 SpriteRenderer/UI 또는 머티리얼에서 흰색→노랑→혈색 빨강으로 지정하고 발광은 머티리얼에서 조절한다. 전환 알파도 Unity에서 제어한다.

인식 범위·자기 플레이어·머리 크기·빌보드·상태 분기는 컨펌 이후 구현한다. 현재 Unity 임포트, Animator, 씬/프리팹/게임 코드 연결과 실제 플레이 검증은 수행하지 않았다.

## 제작 근거

기본 GPT 이미지 생성 도구를 사용했다. 사용자 참조 이미지를 보고 형태를 간결화했다. 생성형 투명화의 잔상 때문에 사용자 승인 아래 검은 배경 원본에서 코드로 알파를 추출했다. 최종 생성·추출 프롬프트는 `prompts.md`에 기록한다.
