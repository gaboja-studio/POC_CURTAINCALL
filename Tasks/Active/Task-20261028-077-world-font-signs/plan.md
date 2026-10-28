# Task-20261028-077

- **Title:** 월드 간판 폰트 아틀라스 및 비교 프리팹
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/add-world-fonts
- **Base:** dev

## Context
BM_Fonts에 추가된 TTF 11종으로 월드용 간판 글씨를 만들고 빠르게 비교한다. 사용자 참고: https://mayquartet.tistory.com/100.

## 계획
1. 각 폰트를 4096×4096 SDFAA, 40pt, padding 5로 생성한다. ASCII·한글 음절 전체·호환 자모·참고 글의 기호 범위를 굽고 Static으로 고정한다. 필요 시 multi-atlas를 허용한다.
2. 기존 Synty 간판 모델을 중첩한 신규 프리팹 3종(벽면 간판·입간판·칠판)을 만든다. 원본 모델은 수정하지 않는다.
3. 별도 비교 씬에 동일 문구의 11종 표본과 폰트를 교체하는 간판 3개를 배치한다. 전용 EditorWindow·Inspector의 폰트 선택/이전/다음/문구 변경으로 즉시 비교한다.
4. 글자 누락·폰트 참조·저장 후 재로드·전환을 확인하고 check-work를 실행한다.

## 미정·주의
- 폰트 파생 에셋은 비공개 Assets/_ThirdParty/BM_Fonts/Generated에 보관한다. 메인 저장소와 서브모듈을 함께 저장해야 한다.
- 기존 게임 씬과 UI 폰트는 수정하지 않는다. 사용자 요청 없는 커밋·푸시는 하지 않는다.
- Static 아틀라스는 원본에 없는 기호를 추가할 수 없다. 누락 수와 실제 샘플 표시를 별도로 확인한다.
