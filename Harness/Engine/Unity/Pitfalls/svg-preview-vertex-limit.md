---
summary: 복잡한 SVG 검토 시트의 VectorImage 임포트에서 65535 정점 한도 예외가 날 때
status: active
updated: 2026-10-29
source: Task-20261029-078 콘솔과 Editor.log 및 CLI 재임포트
---
# SVG 검토 미리보기 정점 한도

Unity 6000.3.25f1의 SVGImporter에서 복잡한 검토용 SVG를 VectorImage(svgType 3)로 가져오면 BuildVectorImageThumbnail → UIElements TempMeshAllocator에서 65535 정점 한도를 넘을 수 있다. 게임 코드 컴파일 오류와 구분한다.

- Editor.log에서 예외 직전 Start importing의 경로를 찾아 대상 파일을 좁힌다.
- 장식/아이콘 등 게임용 에셋의 설정을 일괄 변경하지 않는다. 참조 없는 검토용 시트에 한해 Texture2D(svgType 2) 임포트를 사용하고 GUID와 SVG 원본을 유지한다.
- Task-078에서는 dayclock_v2/Source/PreviewSVG 2개와 settings_v2/Previews 2개의 .meta를 Texture2D, textureSize 512, tessellationMode Triangulation, targetResolution 128로 변경했다.
- SVGImporter.SvgType setter 뒤 SaveAndReimport만 호출했을 때 이번 환경에서는 .meta에 변경이 남지 않았다. 실제 .meta와 재임포트 결과를 확인한다.
- Texture2D로 형식만 바꾸면 내부 Sprite 메시에도 65535 한도가 남는다. 검토 미리보기는 곡선 기하 해상도(targetResolution)를 낮춰 한도 안으로 가져온다. 출력 텍스처 크기와 기하 해상도는 별개다. UISVGImage는 이번 환경에서 svgImage null 임포트 오류가 나므로 사용하지 않았다.
- 큰 SVG 재임포트는 CLI 동기 eval의 메인 스레드 5초 제한을 넘을 수 있다. `unity command eval --detach`의 job 완료와 콘솔을 별도로 확인한다.
