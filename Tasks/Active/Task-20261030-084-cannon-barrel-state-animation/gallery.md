# 대포 포신 확인

씬: `Assets/Scenes/Tests/MainStage/HumanCannon/CannonBarrelGallery.unity`.
메뉴: `CurtainCall > Validation > Open cannon barrel gallery`.

Play에서 Active / Inactive / Fire + lower / Recover (4 nods) / Full cycle을 고른다.
Pause, Restart, 타임라인 드래그, 0.25배속과 1배속으로 동작을 확인한다.
Front / muzzle, Side, 3/4 view, Rear로 앞·옆·뒤 모두 볼 수 있다. 기본은 포구가 보이는 3/4 방향.

실제 게임에서는 발사 순간 짧은 후퇴·덜컹임 → 1.2초까지 내려앉기 → 비활성 대기 → 2.8초간 네 번 들어 올리기 → 재탑승 가능.
`Assets/Resources/Settings/GameSettings/Tricks/HumanCannonSettings.asset`의 대포 쿨타임에서 전체 시간을 4~20초로 조절한다. 기본 5초는 1초 대기를 포함한다. 탈출로 ON은 대기를 건너뛰고 회복을 시작한다.
갤러리는 포즈 확인용이며 실제 탑승/발사는 HumanCannon 씬의 대포를 사용한다.

참고 그림은 먼저 기존 Blender 모델의 정적 포즈를 렌더링해 제작했다. imagegen은 출력 단계 violence 분류로 2회 실패했다.
실제 원본: `Assets/Resources/Models/Objects/HumanCannonProps/Source~/cannon-barrel-motion.blend`.
4개 Blender NLA take와 Unity AnimationClip이 있다. 바퀴/차체는 고정, 포신만 움직인다.
