# Cannon Motion

Blender에서 제작한 30fps 제자리 Humanoid 애니메이션. Unity의 기존 물리·네트워크 이동을 사용하며 root motion은 꺼져 있다.

- CannonLoaded 2.0s loop: 25도 포신에 엎드림, 목을 들어 앞을 봄.
- CannonRise 1.2s loop: 몸을 편 비행, 작은 무릎·몸 움직임.
- FlightApex 0.6s: 추진력이 사라지고 팔다리가 벌어지는 전환.
- PanicFall 1.2s loop: 양팔·다리 위상이 다른 당황한 낙하.
- TrampolineRise 1.2s loop: 팔을 위로 뻗는 반동 상승.
- FlightLand 0.8s: 압축·반동·회복. 실제 착지 판정 후 재생.
- TrampolineCarry 2.0s loop: 상체 운반 레이어, 실제 손바닥 접점은 런타임 2관절 정렬.
- CarryJump 1.2s loop: 상체 잡기 유지, 하체 점프 압축.
- RopeHang 2.0s loop: 외줄 추락 후 버티기.
- RopePull / RopeLift 각 2.0s: 구조자 끌어올리기 / 피구조자 올라오기. 공유 구조 진행률로 동기화.

11종 × 생존 4단계 = 44클립. 기본 이름은 온전한 몸, _Cut1은 왼다리 손실, _Cut2는 양다리 손실, _Cut3은 양다리·왼팔 손실이다. 기존 절단 표면이 없는 팔다리를 숨기며 남은 팔/다리·몸통의 지지·균형·당황 동작을 따로 제작했다. 사망은 기존 래그돌을 사용한다.

## 수정·재현

cannon-motion.blend에는 한 리그와 44개 CC_ 액션, 자체 제작 편집용 마네킹이 있다. Action Editor에서 액션을 선택한다. 게임 캐릭터 메시·텍스처는 포함하지 않는다. MotionBindMarker는 FBX 바인드 자세 보존용 작은 자체 제작 메시이며 게임에 생성하지 않는다. 마네킹은 FBX 내보내기 대상에서 제외한다.

원본 작성 스크립트 build-motion.py는 프로젝트의 private Rabbit v14 리그를 읽는다. 별도 Blender 백그라운드 프로세스에서 실행한 뒤 같은 Python 세션에서 build-variants.py를 실행한다. FBX는 원본과 동일한 축(-Z forward/Y up), FBX_SCALE_UNITS, 변형 본, bind marker를 사용한다. bake_anim all_actions/NLA는 끈다. Unity는 RabbitV14의 검증된 sourceAvatar를 참조해 humanoid curves로 임포트한 뒤 네 캐릭터에 리타깃한다.

save-motion-source.py를 별도 Blender 프로세스에서 실행하면 FBX의 편집 가능한 리그·액션을 모은 원본을 저장한다. 라이브 Blender의 대형 연결 데이터에 libraries.write를 실행하면 이번 환경에서 종료되어 이 방식을 사용했다.

연결 및 임포트 재현 스크립트는 Task-20261030-083의 Source/import-motion.cs, apply-motion.cs에 있다. 기존 private Player.controller를 보존하고 CircusPlayer.controller를 네 캐릭터 프리팹에 연결한다. 기존 클립/마스크는 참조를 유지한다.
