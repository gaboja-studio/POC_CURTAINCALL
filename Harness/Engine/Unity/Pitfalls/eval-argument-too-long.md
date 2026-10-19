---
summary: Windows에서 unity command eval에 32KB 넘는 C#을 넘기면 "Argument list too long"으로 실패한다. 긴 코드는 파일로 써서 eval_file --file로 실행
status: active
updated: 2026-10-19
source: Task-20261008-024
---

# eval 인자 길이 초과

## 언제 읽나

Windows에서 `unity command eval`(또는 Pipeline eval)로 긴 C# 코드를 실행하다 "Argument list too long"이 날 때.

## 증상

짧은 코드는 되는데, 배치·씬 구성처럼 긴 코드(약 32KB 초과)만 실행 전에 실패한다.

## 원인

Windows 명령줄 길이 제한(약 32K 문자)에 코드 전체가 인자로 들어간다.

## 대처

코드를 `.cs` 파일로 저장하고 `eval_file --file <경로>`(Pipeline `eval_file`)로 실행한다. 반복 작업이면 `run_script`도 쓸 수 있다.

## 확인 방법

같은 코드를 `eval_file`로 실행해 오류 없이 끝나는지 본다.
