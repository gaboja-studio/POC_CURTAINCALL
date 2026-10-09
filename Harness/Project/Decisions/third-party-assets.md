---
summary: 외부(서드파티) 에셋은 무료·유료 구분 없이 private 레포 CURTAINCALL_PaidAssets를 Assets/_ThirdParty submodule로 받아 쓴다. 공개 레포에는 직접 만든 것만
status: active
updated: 2026-10-09
source: human (2026-10-09 PM @MoHoDu)
---

# 외부 에셋 보관

## 언제 읽나

Asset Store 등 외부에서 받은 에셋을 넣거나 옮길 때, 팀원이 `Assets/_ThirdParty`를 받지 못할 때, CI에서 Unity 컴파일을 돌릴 때.

## 결정

- 2026-10-09 / PM(@MoHoDu):
  - 공개 레포(POC_CURTAINCALL)는 GitHub Actions·ruleset 때문에 public을 유지한다. 그래서 **외부에서 받은 에셋은 무료·유료 구분 없이** private 레포 `gaboja-studio/CURTAINCALL_PaidAssets`에 두고 `Assets/_ThirdParty` submodule(HTTPS)로 받는다. Asset Store 무료 에셋도 EULA상 공개 재배포가 안 된다.
  - 공개 레포에는 직접 만든 코드·아트·씬·사운드, Unity 기본 리소스(`TextMesh Pro/`), 공개 라이선스 폰트(Pretendard, OFL), UPM 패키지(`Packages/manifest.json`)만 둔다.
  - 외부 에셋 설정 파일(예: `Assets/Resources/DOTweenSettings.asset`)은 공개 레포에 남긴다.
  - 첫 이관: DOTween·DOTween Pro·DemiLib(`Assets/Plugins/Demigiant` → `Assets/_ThirdParty/Demigiant`). Pro 예제는 삭제.
  - 팀원 전원에게 private 레포 읽기 권한을 준다. 게임 코드가 DOTween을 쓰므로 없으면 컴파일이 깨진다.

## 방법

- 받기: `start-work`·`worktree.ps1 -Add`가 `git submodule update --init`을 자동 실행. 직접 clone은 `git clone --recurse-submodules`. 이관 전 경로(`Assets/Plugins/Demigiant`)에 남은 무시 파일은 같은 단계에서 자동 정리(`doctor`가 경고). 디버그 심볼(`.mdb`·`.pdb`)은 private 레포에도 저장하지 않는다.
- 새 외부 에셋: import 후 `.meta`와 함께 `Assets/_ThirdParty/<퍼블리셔>/<에셋>/`로 옮기고 → private 레포에서 커밋·push → 공개 레포에서 포인터 변경을 커밋. private 레포 README 에셋 표는 main push 때 Actions가 자동 갱신(비고는 에셋 폴더 `.note` 첫 줄).
- 막기(`Scripts/verify-third-party.ps1`): `Assets/` 최상위 허용 목록 밖 폴더, 알려진 외부 에셋 경로, 라이브러리 파일(`.dll`·`.unitypackage` 등)이 공개 레포에 있으면 실패. 직접 만든 콘텐츠용 최상위 폴더가 새로 필요하면 PM이 허용 목록을 고친다.
  - 커밋 전 `verify-fast`, push 전 `.githooks/pre-push`(submodule 포인터가 private 레포 main에 있는지도 확인), PR의 `verify-third-party` 검사. 브랜치 룰셋에서 이 검사를 필수로 지정한다.
  - PR 검사의 포인터 확인에는 Secret `THIRD_PARTY_TOKEN`(private 레포 Contents: Read-only)이 필요하다. 없으면 경로 검사만 한다.
- CI에서 Unity 컴파일이 필요하면 `actions/checkout`에 `submodules: recursive`, `lfs: true`, 같은 토큰을 준다.

## 이유

유료 에셋을 공개 레포에 두면 라이선스 위반·유출 위험이 있고, 가격으로 나누면 팀원이 매번 판단해야 한다. 출처 하나로 나누면 규칙이 단순하다.

## 바꾸려면

`.gitmodules`, `.gitignore`, `Scripts/verify-third-party.ps1`, `Harness/Engine/Unity/Policies/code-folders.md`, `Docs/Guides/assets-folder-rules.md`를 함께 고친다.
