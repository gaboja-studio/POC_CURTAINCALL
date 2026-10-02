---
summary: Pipeline 패키지/Unity CLI 버전이 바뀌면 검증 스크립트가 쓰던 명령이 사라지거나 결과 형식이 바뀜
status: active
updated: 2026-10-02
source: Octoplug 회고 (verify-unity.ps1이 새 Pipeline과 불일치, 2회)
---

# Pipeline 명령 이름·형식 변경

## 언제 읽나

`com.unity.pipeline` 또는 Unity CLI(`unity`)를 업그레이드한 뒤, 또는 `verify-unity.ps1`이 "명령 없음"·파싱 오류로 실패할 때.

## 증상

`unity command <name>`이 알 수 없는 명령이라며 실패하거나, JSON 결과의 필드명이 달라 스크립트가 잘못 판단한다.

## 원인

Pipeline은 실험(exp) 버전이라 명령 이름과 결과 형식이 버전마다 바뀐다.

## 대처

1. 명령 이름을 가정하지 않는다. `unity command --project-path <p> --query <키워드>`로 현재 목록을 확인한다. CLI beta.12부터 이름 없는 `unity command`는 태그 목록만 보여 주므로 전체 목록이 필요하면 `--detail full`을 붙인다.
2. `Scripts/verify-unity.ps1` 상단의 명령 이름 변수를 현재 버전에 맞게 고친다.
3. 버전을 바꿨다면 `Harness/Engine/Unity/Facts/unity-cli.md`와 `Decisions/version.md`를 갱신한다.

## 확인 방법

`unity --version`, `Packages/manifest.json`의 `com.unity.pipeline` 버전이 `Facts/unity-cli.md`와 같은지 본다(`doctor.ps1`이 일부 검사).
