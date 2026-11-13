# Render Graph 검토

## 1. Validation Summary
Unity 6000.3.25f1, 설치된 URP 패키지 소스 기준. 상호작용/네비의 자식 Renderer를 하나의 화면 마스크로 합치고 외부 픽셀만 합성한다. 실제 Camera.Render 픽셀 검사에서 내부선 없음·벽 가림·카메라 제한·스킨 포즈 추적을 확인했다.

## 2. Confirmed Issues
최초 구현의 전역 텍스처 설정을 제거했다. 현재 합성은 MaterialPropertyBlock에 mask/info/sceneDepth를 명시적으로 바인딩한다. 새 전역 상태를 공개하지 않는다. 기존 내부 셸 생성/메시 복사/LOD 변형은 제거했다.

## 3. Likely Issues / Risky Assumptions
- PC/Mobile URP Renderer 두 개에 설치했다. 다른 커스텀 Renderer를 추가하면 같은 Feature를 설치해야 한다. Compatibility Mode는 대상이 아니며 Render Graph를 사용한다.
- 기존 상호작용처럼 투명/depth-write-off 재질은 제외한다. 알파 컷아웃 텍스처 구멍은 현재 메시 실루엣으로 처리한다.
- LOD는 카메라 거리·LOD bias로 단일 레벨을 선택한다. 게임 외부에서 ForceLOD를 강제하는 특수 표시와 XR 기기는 별도 검증 대상이다.
- 네트워크 동적 대상은 NetworkObject ID+자식 경로, 씬 대상은 씬 경로+인덱스+이름 검증으로 찾는다. 독립적으로 생성되어 서로 대응할 수 없는 비네트워크 오브젝트는 원격 아웃라인을 만들 수 없다.

## 4. Recommended Fixes
현재 요청 범위의 필수 수정은 반영했다. 마스크 텍스처는 현재 카메라 attachment descriptor에서 파생하고, MSAA=1/clear/포맷을 지정한다. 데이터 길이 검증과 대상 이름 검증으로 잘못된 원격 타깃 선택을 피한다. 프러스텀 필터와 보수적 화면 경계로 외곽 탐색 비용을 제한한다.

## 5. Corrected Snippets
`SilhouetteOutlineFeature.RecordRenderGraph`: mask/info를 RenderAttachment Write, 합성 입력을 UseTexture Read, 카메라 컬러를 ReadWrite(blend 보존)로 선언한다. 카메라 컬러를 샘플하지 않으므로 feedback loop가 없다. 개인 depth attachment는 씬 깊이를 오염시키지 않는다. ConfigureInput(Depth)와 cameraDepthTexture로 벽 가림을 유지한다. 두 render function은 static이며 PassData의 모든 필드를 매 기록 시 채운다. 합성은 3개 입력+알파 블렌딩이므로 별도 raster pass가 적합하다.

## 6. Missing Information
다중 PC 지연·XR·실기기 Mobile GPU는 검증하지 않았다. 기존 플레이어 카메라/게임 거리/상호작용 규칙은 변경하지 않았다.
