# Task-20261030-082

- **Title:** 인간 대포 트램펄린·대포 모델 제작 및 더미 교체
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/modeling/main-stage-objects
- **Base:** dev

## Context
첨부 트램펄린·대포 컨셉을 Blender에서 제작하여 인간대포 더미 외형을 교체한다. 사용자 요청 담당 @MoHoDu, 실행 Codex. 별도 애니메이션·리깅은 만들지 않는다. 이미지의 UI/기능 문구는 구현 지시가 아니라 시각 참고다.

## 계획
1. 실제 씬/프리팹의 치수, 발사·탑승·손잡이·반동·충돌 계약과 배경/캐릭터 톤 조사.
2. Blender 별도 씬에서 버건디 패드·남색 천·아이보리 별·황동 프레임 트램펄린, 줄무늬 통·패딩 포구·목제 스포크 바퀴·낮은 계단 대포 제작. 둥근 윤곽에 제한된 면 분할과 봉제/패치 디테일로 봉제인형 캐릭터에 맞춘다.
3. 원본 blend·재현 스크립트·FBX·공유 재질 보존. UV와 Unity용 표면 텍스처를 만들어 Blender 전용 절차 재질 의존 방지. 정적 MeshRenderer 사용.
4. 새 시각 프리팹을 만들고 실제 HumanCannon 씬에 적용. 기존 기능 Transform, Collider, MonoBehaviour 참조 보존. 대기실은 관련 소품 사용 위치를 확인하여 기능 없는 외형 교체만 수행.
5. Blender/Unity 실제 미리보기와 메시/재질/기능 계약 검사, check-work 1회. 최종 미술 감상·온라인 실기기 플레이는 사람 확인 항목으로 기록.

## 미정·주의
기존 캐릭터 Blender 파일과 미저장 씬을 덮어쓰지 않는다. 현 브랜치는 전용 feat/modeling/main-stage-objects. 새 자체제작 모델은 Assets/Resources/Models/Objects/HumanCannonProps, Blender 원본은 Source~ 하위에 배치한다. 외부 에셋은 추가하지 않는다. 대기실 Task-075/079와 겹칠 수 있는 변경은 log에 경로를 명시한다. 2026-10-30 사용자가 저장·푸시·dev PR 제출을 요청했다. 병합은 별도 검토 후 진행한다.

## 현재 결과
v01 두 모델 제작 및 인간대포·대기실 총16개 소품 외형 교체 완료. 기존 기능/앵커/충돌 컴포넌트와 실제 whole-device 접촉 경계 보존. Editor15/15, PlayMode 반동2/2·대포10대 탑승취소1/1, 최종 check-work 안전성·문서규칙·컴파일 PASS. 미술 최종 검토·4인 온라인/성능은 사람 확인으로 남긴다.
