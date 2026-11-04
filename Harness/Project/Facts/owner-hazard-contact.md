---
summary: 일반 대포 장애물의 피해자 표시 접촉 보고와 호스트 검증을 바꿀 때
status: active
updated: 2026-11-04
source: Task-20261103-099, 코드 및 단독 호스트 회귀
---

# 일반 대포 장애물 접촉

- HumanCannonHazardCourse의 원격 피해는 소유자가 자신의 이동 경로와 표시 장애물 시각으로 접촉을 보고해야 시작한다. 호스트 본인은 기존 sweep 판정이다.
- 41B 신뢰성 순서 패킷: 시작/해제, sequence, 장애물 index, RestartVersion, 코스 epoch, 접촉 elapsed, feet. 피해자 ID는 NGO sender로만 결정한다.
- 호스트는 연결·생존·코스/재시작·순서·최대1.25초의 시각·유한 좌표·이동 오차와 해당 시각의 실제 장애물 형상을 확인한다. 접촉 해제와 시작을 구분해 계속 닿은 동안 재절단하지 않으며 동시 접촉은100ms 단위로 피해를 제한한다.
- 부상/사망과 기존 ContactDetected/HitConfirmed/ContactResolved 이벤트는 호스트에서 확정한다. 클라이언트에서 미리 부상을 적용하지 않는다. owner 이동 권한을 전제로 한 타당성 검사이며 완전한 서버 되감기/부정행위 방지는 아니다.
- Hazards Request/State/Contact는 v2. 모든 참가자가 같은 빌드여야 한다. 코스 초기화·난이도 변경·재시작·연결 해제 때 접촉 기록을 정리한다.
- 적용 범위는 HumanCannonHazardCourse뿐이다. 외줄 톱/불꽃·HumanCannonFinalSaws·SawGate·Route의 별도 피해 판정은 이 변경에 포함되지 않는다.
- 결과: 이동 장애물의300ms 지연·회피/중복/이전epoch/재시작 거부, 원격 소유자 host-only 절단 차단을 단독 호스트에서 확인했다. 실제4인/Relay·전후 FPS/유실 패킷 실측은 아직 없다.
