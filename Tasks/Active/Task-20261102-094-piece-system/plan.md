# Task-20261102-094

- **Title:** 기물 시스템 개선: 쿨타임 제거·스프링 신발 내구도 2·소지 칸 내구도 바·소모 연출
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/piece-system
- **Base:** dev

## Context
Task-092(PR #260)로 들어간 기물 시스템의 후속 개선(2026-11-02 사용자 요청). 작업 전 DOTween(Pro) 업데이트·All In 1 Sprite Shader 가져오기를 private 저장소 main과 이 브랜치에 저장·push했다(`SeasideStudios/AllIn1SpriteShader`로 이동).

## 계획
1. 공연 기물 4종 쿨타임 제거: `ItemCatalog.asset`의 `cooldown` 0. 쿨타임 경로 코드는 두고 값만 0(되살리기 쉬움).
2. 스프링 신발 새 제품 내구도 3 → 2.
3. 소지 칸 1.5배(96px, `C5_InventorySlots` Scale). 남은 횟수 숫자 대신 칸 아래 1단위 내구도 바(`InventorySlotDurabilityBar`).
4. 내구도 소모 시 그 칸 연출(`InventorySlotUseEffect`, DOTween + All In 1 Sprite Shader Hit/Fade). 판정 화면이 `PropCarry.LastUse` 번호를 소지 상태와 함께 보내 모든 화면이 같은 칸에 띄운다.

## 미정·주의
- 연출 세기·색·시간은 초안. 사람이 플레이로 확인한다.
- 신규 머티리얼은 `Assets/_ThirdParty/curtaincall/InventorySlotFx/`(private 저장소, 아직 미저장).
