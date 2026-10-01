---
summary: Console의 ObjectDisposedException(Socket, BasePipelineServer.ProcessRequestDetached)은 AI 에디터 브리지의 로그이고 게임 코드 오류가 아님
status: active
updated: 2026-10-01
source: Task-20260930-003
---

# Pipeline 서버의 ObjectDisposedException 로그

## 언제 읽나

Unity Console에 `System.ObjectDisposedException: Cannot access a disposed object. Object name: 'System.Net.Sockets.Socket'`이 보이고, 스택에 `Unity.Pipeline.BasePipelineServer.ProcessRequestDetached`가 있을 때.

## 증상

```
at System.Net.HttpListenerRequest.get_RemoteEndPoint ()
at Unity.Pipeline.BasePipelineServer.ProcessRequestDetached (...) in
   ./Library/PackageCache/com.unity.pipeline@.../Runtime/Common/BasePipelineServer.cs:531
```

게임 동작·컴파일·테스트 결과에는 영향이 없다.

## 원인

`com.unity.pipeline`(AI가 에디터를 조작할 때 쓰는 HTTP 서버)이 요청을 처리하기 전에 연결이 이미 닫혀, 상대 주소를 읽다가 예외가 난다. AI의 컴파일 검사 같은 요청 직후, 도메인 리로드나 Play 진입 때 보였다. MPPM 가상 플레이어와의 관계는 확인하지 않았다.

## 대처

1. 게임 코드의 결함으로 조사하지 않는다. 스택에 우리 스크립트가 없으면 무시한다.
2. 패키지는 `Packages/` 아래라 고치지 않는다(작업자 수정 금지).
3. 에디터 명령이 실제로 실패하면 이 로그가 아니라 `editor-not-visible.md`, `pipeline-command-drift.md`를 본다.

## 확인 방법

스택 맨 아래가 `com.unity.pipeline` 경로인지 본다. 같은 시각에 `[Session]` 같은 게임 로그 오류가 없으면 무관하다.
