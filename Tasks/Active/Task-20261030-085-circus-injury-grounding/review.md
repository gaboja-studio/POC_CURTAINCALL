# 절단 지면 동작 확인

## 계획과 적용
전체 메시 최저점만으로는 손끝이 닿고 골반이 뜬 자세를 구별할 수 없었다. 남은 발·골반을 지지점으로 측정하고, Blender 동작을 수정한 뒤 네 실제 아바타와 게임에서 확인한다.

| 상태 | 수정 내용 |
|---|---|
| 외발(Cut1) | 남은 발 위 체중 이동, 무릎 충격 흡수, 몸통 반대 기울기 |
| 양다리 없음(Cut2) | 낮은 골반 지지, 굽힌 팔꿈치로 착지 충격 흡수, 끌어올릴 때 몸통 뒤로 젖힘 |
| 오른팔만 남음(Cut3) | 골반 지지를 유지하며 남은 팔 쪽 어깨·몸통의 비대칭 반응 |

대상은 FlightLand / TrampolineCarry / RopePull의 Cut1·2·3, 총 9클립이다. 비행·매달림은 기존 공중 동작이며 바닥에 고정하지 않는다. 실제 운반에는 손잡이 정렬을 접지 이후 적용한다.

## 에디터에서 보기
`Assets/Scenes/Tests/MainStage/HumanCannon/CircusMotionGallery.unity`를 열고 Play한다. 착지 동작부터 시작한다. `Pause`와 타임라인으로 접촉→압축→회복을 확인하고, `Next injury stage`와 `Focus selected actor`로 확대한다. 카메라 회전/높이 슬라이더로 앞·옆을 확인할 수 있다.

네 캐릭터를 모두 비교하고 FlightLand → TrampolineCarry → RopePull을 선택한다. 상단의 Ground contact / Airborne / Rope-supported 표시로 지면 동작을 구분한다.

## 자료
- 편집 원본: `Assets/Resources/Models/Characters/CannonMotion/Source~/cannon-motion.blend` (44액션, 수정9개)
- 동작 생성 규칙: 같은 폴더 `build-variants.py`
- 전후 렌더: 이 Task `Previews/before-*.png`, `Previews/latest-*.png`
- 실제 외줄 구조: `Previews/play-rope-*.png`
- 검사 결과: `Source/*tests.json`, `Source/transitions.json`
