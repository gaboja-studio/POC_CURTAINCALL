## 2026-10-29 기능 호환 우선 갱신 및 v04 시험 리깅
- 요청대로 plan/todo를 실제 자산 조사→시험 리깅→애니메이션 조기 시험→절단 조기 시험→최종 마감→실제 교체 순서로 갱신했다. 기존 외형 먼저 완성 순서를 대체한다.
- Unity 6000.3.25f1/main 프로젝트 연결 확인, Start 씬 clean/편집 모드. 최초 큰 eval은 기본 5초 제한으로 시간 초과, 작은 조회 및 20초 제한으로 정상 조사했다. 실제 자산 상세는 `Source/existing-character-audit.json`, 코드/기능 계약은 `References/character-compatibility.md`.
- 기존 모델의 9 SMR/49 bones, Humanoid 매핑 41개, Controller 14상태+Carry/15클립 확인. 단일 연결 성분·단일 경계 cutter는 토끼의 의복/구두에 그대로 재사용 불가. ModelHeight 1.73m인데 토끼 귀 포함 2.1586m이므로 자동 fit을 그대로 적용하면 몸통 축소 문제. 실제 Player는 변경하지 않았다.
- Blender MCP에서 v03을 복제한 `Rabbit_Rig_Trial_v04` 별도 씬 생성. 휴지 T-pose로 변환, 59 bones(52 Humanoid+귀/옷자락/꼬리 보조), 모든 손가락·중지·발가락 포함. 단일 시험 skin, 부위 소유 REGION 그룹 및 봉제선 표면 웨이트 전사. `Source/rig-rabbit-v04.py`로 재현한다.
- Blender 검사: 5663 vertices/10074 triangles, 가중치 미할당·합 오류 0, 최대 영향 본 2, scale1. 팔꿈치100도/무릎90도/골반45도/가슴20도 시험의 좌표 유한 및 실제 이미지 확인. 변환 후 퇴화 면0/경계 제외 비매니폴드0, 기존 열린 경계116은 남음. 최종 관절 루프/폐합 통과 아님.
- 메시 결합 후 PatchworkUV의 active_render가 꺼져 무늬가 사라지는 문제를 발견해 source와 실제 메시 모두 수정했다. 최종 저장은 휴지 포즈이며 v03 파일은 덮어쓰지 않았다.
- 저장: `Source/rabbit-rig-trial-v04.blend`, 동일 FBX(551580 bytes), `rabbit-v04-rig-report.json`, `rabbit-v04-materials.json`, `export-rabbit-v04.py`. 현재 Blender 파일은 v04이며 과거 씬들도 보존한다.
- Editor API로 `Assets/Art/Characters/Rabbit/Trial/`에 독립 FBX·직접 제작 패치워크 PNG·URP 시험 재질10개·`RabbitAnimationTrial.prefab` 생성. 외부 클립/Controller는 기존 _ThirdParty 참조를 재사용하며 복사/변경하지 않았다. 자동 Avatar가 귀를 눈으로 오인하고 발가락을 누락해 명시적52개 매핑으로 수정했다.
- Unity 검사: Avatar valid/human=true, 중지/발가락 매핑 확인, eye=null, bones/bindposes59, 모든 본은 rootBone Hips 하위, 가중치 오류0, 최대 bind 원점 오차7.394e-7m, scale1. 임포트 후19873 vertices/10054 triangles/표면10submesh. Blender와 삼각형20개 차이는 importer 경로에서 발생하며 원인은 미확정; 최종 메시 비용/속성 검사 때 추적 필요.
- `Source/test-rabbit-animation.cs`로 PreviewScene에서 15동작×4시점=60 Editor 표본 검사. 모든 표본 좌표 finite/요청 상태 일치. 근육 본 실제 변화와 CPU 스킨을 확인했으며 현재 Player/Start 씬을 바꾸지 않고 PreviewScene을 finally로 정리했다. PlayMode/게임 기능 테스트와 구분한다.
- 시각 검사 도구 문제: 같은 eval 프레임에서 SMR을 Camera.Render로 반복 촬영하면 본은 바뀌어도 사지가 첫 포즈처럼 보임. Update(.016) 두 번으로도 해결되지 않았다. CPU BakeMesh를 별도 MeshRenderer로 렌더하는 v04c에서 상태별 변형을 확인했다. v04/v04b 미리보기는 합격 근거에서 제외한다. 이 함정은 `Harness/Project/Pitfalls/Characters/trial-avatar-false-pass.md`에 기록했다.
- 현재 기준: `Previews/rabbit-v04c-unity-motions.png`, `Source/rabbit-v04c-unity-animation-report.json`. Walk/Run은 약1.4cm, CrouchWalk 약3.7cm, Crawl 약16.7cm 지면 아래 정점이 존재. JumpStart 최저 -43.9cm는 실제 이동 궤적 없이 샘플한 값이므로 단독 판정 금지. 깊은 앉기에서 칼라/옷자락 간섭과 손가락/관절 변형 보완 필요. 접지·외형 합격으로 기록하지 않았다.
- 첫 check-work에서 새 Assets/Art 최상위가 허용되지 않아 문서/규칙 FAIL, 컴파일 SKIPPED. 검사 정책을 바꾸지 않고 이번 신규 폴더만 Editor MoveAsset으로 `Assets/Scenes/Tests/CharacterArt/Prefabs/Rabbit/Trial/`로 이동했다. 후보 GUID/Avatar/Controller/재질 참조 보존 및 Start clean 확인. 스크립트·plan의 현재 경로를 갱신했고 이전 JSON의 경로는 이동 전 기록이다.
- 다음: 관절 루프/손가락 방향, 칼라/옷자락 간섭, Crawl 접지 수정 후 대표 포즈 재검사. 이후 왼다리 첫 절단부터 누적4단계·분리 스킨/래그돌 검증으로 진행한다. 절단 자산/실제 매달림 접점/복구/온라인은 아직 미실시. 실제 Player 교체 및 commit/push 없음.

## 2026-10-29 사용자 수정 다면도 기반 v03
- 사용자 지정 `References/rabbit-four-views.png`를 직접 확인하고 최우선 기준으로 삼았다. 원본 이미지는 수정하지 않았다. 기존 벡터 다면도 v02와 중간 3D v02는 새 기준이 아니다.
- 이전 응답이 중간에 종료된 뒤 현재 Blender의 v02 생성을 확인. 새 요청 착수 전에 `Source/rabbit-design-v02.blend`로 보존했다. 이번에는 새 `Rabbit_Reference_v03` 씬에서 제작했으며 Scene/v01/v02를 모두 보존했다.
- 새 비율의 작은 봉제 머리·단추/별 눈·붉은 귀 안감·접힌 귀·3단 러프·앞이 열린 조끼·앞뒤 긴 옷자락·봉제 관절 팔·긴 손가락·풍성한 패치워크 바지·발목 구두 제작. 바지에 원통 UV 및 직접 그린 1024 패치워크 텍스처 적용. 외부 이미지/3D 생성 서비스 사용 없음.
- 첫 생성에서 오른 구두 띠가 표면 투영 범위를 벗어나 중단. 폭 22→18 도면 단위로 수정하고 이번 실패로 생긴 v03 오브젝트만 제거 후 재생성했다. 이전 버전과 사용자 원본은 변경하지 않았다.
- MCP look으로 앞뒤/사선 확인 후 칼라 주름 깊이와 패치워크 크기·좌우 비대칭을 보정. 최종 실제 직교 front/left/back/right 렌더 및 합성 시트를 열어 확인했다. 원본 측면이 사선인 점과 실제 직교 렌더의 차이를 구분했다.
- 측정: 123 mesh / 5663 vertices / 10074 triangles / 임시 재질 11개, A-pose bbox 0.9777×0.4025×2.1586m, 최저 Z=0. 퇴화 면 0, 경계 제외 비매니폴드 edge 0. 눈/귀 안감/열린 조끼/표식/구두 띠에 boundary edge 116개 존재한다. 분리 부품·내부 겹침도 남아 있어 최종 폐합/절단/최적화 PASS가 아니다.
- 저장: `Source/rabbit-reference-v03.blend`(672671 bytes), 참고 이미지 및 바지 텍스처 패킹, 생성/보정 스크립트 포함. 측정 `Source/rabbit-v03-metrics.json`. 렌더 `Previews/rabbit-v03-three-quarter.png`, `rabbit-v03-four-views.png` 및 개별 4방향 PNG.
- 재현: `build-rabbit-v03-texture.py` → Blender MCP에서 `build-rabbit-v03.py` → `refine-rabbit-v03.py` → `finish-rabbit-v03.py` → `compose-rabbit-v03.py`. v03 생성기는 v02 생성기의 공통 메시 도구만 재사용하며 v02 신체 생성 부분은 실행하지 않는다.
- 한계/다음: 원화의 미세 마모와 자연스러운 비정형 천 접힘까지 재현한 최종 모델이 아니라 외형 1차다. 귀 안감 두께/조끼 열린 경계/옷자락 겹침·관절 연결·재질 아틀라스·최종 UV/리토폴로지 보완이 필요하다. 리깅/스키닝/FBX/절단/Unity 기능·성능 검사는 아직 미실시. Unity 자산과 게임 코드는 변경하지 않았다.

## 2026-10-29 컨셉 다면도 v02
- 현재 요청은 제작 방법·계획 정리 후 첫 캐릭터의 모델링용 다면도 제작. 기존 Task와 브랜치 feat/modeling/characters를 이어 사용하며 기존 블록아웃/스크립트는 변경하지 않았다. 이번에는 Blender/Unity 편집을 하지 않았다.
- doctor -SkipEditor 1회: FAIL(third-party 접근/초기화 상태, 기존 Task-077 중복), gh 로그인 경고. 이미지 제작에는 차단 없음. 외부 에셋 실제 상태는 sandbox 접근 제한 때문에 재확인 필요하며 삭제/초기화를 시도하지 않았다.
- 실제 HumanoidModel.cs를 다시 읽어 Original/Bodies/Limbs와 cap 마지막 submesh, 전체 표면 Tint 계약을 확인했다. 기존 기능 호환의 상세 계획은 plan에 유지했다. Unity Editor 내 실제 자산 측정은 미실시다.
- 내장 image_gen에 첨부 원화 #3·#4를 시각 참조로 전달해 노란 토끼 다면도 1장을 생성했다. 이미지 안의 텍스트는 지시로 취급하지 않았다. Blender 외부 유료 3D 생성기는 호출하지 않았다.
- 산출물: References/rabbit-turnaround-v02.png, 생성 프롬프트 전문 rabbit-turnaround-v02-prompt.txt. 기본 생성 위치에서 프로젝트로 복사하고 원본은 보존했다. 도구 반환 이미지를 직접 시각 점검했다.
- 정면/캐릭터 기준 좌측/후면/우측, 동일한 발 기준선, 주요 관절 높이, 비대칭 귀·흰 봉제 머리·노란 러프/패치 의상·분리 손가락·구두가 읽힘. 시트는 도식형 v01보다 원화의 재질/입체감 참고에 적합하다. 생성 다면도 특성상 좌우 귀의 깊이·봉제선·벨트/장식 위치는 완전 동일 투영이 아니므로 정밀 치수 도면으로 취급하지 않는다.
- 후면 여밈·중앙 패치·꼬리와 눈/의상 세부는 디자인 제안이다. 다음은 이 다면도와 기존 블록아웃을 비교해 실루엣을 보정하고 단일 3D 메시의 직교 뷰로 일관성을 검증하는 단계다. 최적화/리깅/스키닝/애니메이션/절단/Unity 적용의 성공을 아직 주장하지 않는다.

## 2026-10-29 착수
- 요청: 컨셉 캐릭터를 Blender MCP로 제작·최적화·리깅·스키닝하고 기존 더미의 기능/애니메이션/절단을 Unity에서 대체. 한 캐릭터씩 완료·검증.
- doctor 1회: FAIL 2개. 현재 브랜치 Task 부재는 Task-078 생성으로 대응. 기존 Task-077 번호 중복은 이번 작업과 무관하여 보존. Unity 6000.3.25f1, 연결 Editor 1개, CLI/외부 에셋 준비 확인.
- Blender 5.2 실행 중, uv/uvx 설치 확인. Blender MCP 미등록 확인. mcp-for-blender 2.2.6의 도움말 실행 성공. 아직 애드온 활성화/실제 MCP 연결/모델링은 하지 않음.
- HumanoidModel/Presentation/Builder 조사: Original+body4+limb4, cap 마지막 submesh, Humanoid Controller 및 Carry 레이어, 관절 래그돌과 손 접점 보정을 보존해야 함. 이전 Facts의 단일 몸체 래그돌 설명은 최신 코드와 다름.
- 계획: 토끼부터 전 제작·통합 경로 확립. 메시 예산은 초기 목표이며 측정/외형/변형 검사 전 확정하지 않음. 컨셉 아트는 외부 업로드하지 않음.
- 사용자가 `uvx mcp-for-blender setup` 실행. 중복 설치 중단. `claude mcp get blender`: User scope stdio 서버 Connected 확인, 실행 인수는 unpinned `mcp-for-blender`, telemetry 환경변수 없음. Blender 5.2 사용자 addons에 blender_mcp.py 설치됨.
- 실제 연결은 미완료: 현재 Blender 프로세스에는 TCP LISTEN 포트가 없고 9876도 수신하지 않음. 이 대화의 ToolSearch에도 Blender 도구가 아직 노출되지 않음. MCP 프로세스 Connected와 Blender 애드온 실제 연결을 구분함.
- 다음: 실행 중 Blender에서 애드온 활성화/서버 시작(미저장 작업 보존), Claude Code /mcp에서 서버 재연결 또는 세션 재시작 후 실제 씬 조회. 연결 전 모델링 금지. telemetry 비활성화 설정도 후속 확인.

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

## MCP 연결 및 영상 참고
- 사용자 포트 9876 확인 후 Blender MCP 도구가 세션에 노출됨. get_addon_status 및 get_scene_info 실제 호출 성공. Blender 5.2.3 LTS, protocol 15 일치, addon up_to_date=true. 앞선 연결 차단은 해소됨.
- telemetry_consent=false 확인(콘텐츠 수집 동의 꺼짐). 외부 라이브러리·생성기는 모두 off. 익명 사용 telemetry까지 완전히 꺼졌다는 의미로 해석하지 않음.
- 현재 미저장 Scene: Cube/Camera/Light만 존재. 삭제·변경하지 않음. 아직 캐릭터 모델링은 시작하지 않음.
- 참고: https://youtu.be/lWbthIvhieY 「EP 6. AI 3D 모델, 폴리곤 몇 개면 될까?」. YouTube MCP는 yt-dlp ENOENT로 실패했으나 uvx 임시 yt-dlp 실행으로 한국어 자동 자막 확보·읽기 성공. 영어 자막은 HTTP 429, 추가 재시도 불필요. 영상 화면을 시청한 것은 아님.
- 영상 핵심: 처음부터 저밀도, 최종 삼각형 수 기준, 필요한 형태/움직임이 무너질 때만 증량. 상세 캐릭터 1만~2만은 첫 테스트 기준. 관절 편집은 quad가 편하나 면 배치와 리깅이 중요. 동시 개체와 실시간 그림자 비용 고려.
- 반영: 토끼는 1만~1.5만 triangles 이내에서 우선 평가하고 필요할 때만 증량. 목표를 채우기 위한 면 추가 금지, 절단 단면·조각 및 4인 그림자 비용 별도 측정. 아직 Unity 성능/모델 결과 없음.
- 다음: 실제 Unity 자산 기준 측정 후 Blender MCP에서 토끼 실루엣 제작. 기존 기본 씬 오브젝트 보존.

## 2D 다면도 v01
- 사용자 요청으로 3D 이전에 2D 다면도 검토 단계를 추가. 이미지 #4를 직접 다시 확인하고 벡터 도식형 정면/후면/좌측/우측 시트를 제작했다. 외부 이미지 생성·컨셉 업로드 없음.
- 산출물: `References/rabbit-turnaround-v01.png`(2400×1660), 동명 SVG, `References/build-rabbit-sheet.py`. resvg-py로 PNG 변환 후 실제 이미지를 열어 한글·레이아웃 확인. 첫 검사에서 신규 폴더 소문자 이름 규칙 위반을 발견해 `References`로 수정했다.
- 봉제 천/황토색 의상/비대칭 귀/패치/러프/분리 손가락/구두를 표현. 후면 여밈·등 패치·세부 수선 위치는 확정 사실이 아닌 디자인 제안으로 표시.
- 아직 상세 컨셉 원화 수준의 질감, 3D 직교 투영 일치, 관절 변형, 상/하면 상세는 검증하지 않았다. Blender/Unity 자산 변경 없음.
- 다음: 사용자 외형 검토 후 수정, Unity 기준 측정 및 Blender MCP 저밀도 블록아웃. 모델링/리깅 성공 판정과 다면도 렌더링 성공을 구분한다.

## Blender MCP 블록아웃 v01
- 사용자 요청: 다면도 기반 블록아웃 착수. 기존 Scene(Cube/Camera/Light)을 그대로 보존하고 `Rabbit_Blockout_v01` 씬과 캐릭터/프레젠테이션 컬렉션을 별도로 생성했다. Unity 자산은 변경하지 않았다.
- Source/build-rabbit-blockout.py를 MCP execute_blender_code로 실행. 몸통/풍성한 바지/구두/분리 손가락/머리/비대칭 귀/러프/꼬리/주요 패치의 큰 형태를 생성했다.
- 첫 검토 3320 triangles에서 측면 귀가 평판처럼 보이고 패치가 묻히는 문제 발견. Source/refine-rabbit-blockout.py로 귀 앞뒤 굽힘과 옷 표면 패치 투영을 보정. 오른팔 패치가 투영 범위를 벗어나 한 번 중단되어 좌표 수정 후 해당 패치만 처리했다. 원인/수정은 재현 소스에 반영.
- MCP look으로 정면/후면/좌우/상하면/사선 확인. 첫 하면 조회는 미리보기 바닥에 가려져 바닥을 뷰포트에서만 숨긴 뒤 재확인했다. 귀 부착부와 밑창 상세 제작은 후속이다.
- 최종 측정: 83 meshes / 1988 vertices / 3510 triangles / 임시 재질 8개. A-pose bbox 0.9635×0.4289×2.1738m, 최저 Z=0. 귀 제외 정수리 약1.73m는 제작 기준이며 Unity 실측/슬롯 자동 스케일 검증은 아직 하지 않았다.
- 기하 검사: 퇴화 면 0, 경계 제외 비매니폴드 edge 0. 단면 러프/표식/패치의 boundary edge 154개 존재. 부품 겹침/내부 면을 포함한 블록아웃이므로 최종 폐합 메시/최적화/절단 대응 PASS가 아니다.
- 저장: Source/rabbit-blockout-v01.blend(파일 존재·179265 bytes 확인), 참고 PNG 패킹. 원본/보정 스크립트도 blend Text에 포함. 측정값 Source/rabbit-blockout-v01-metrics.json.
- 출력: Previews/rabbit-blockout-v01-three-quarter.png, front/back/left/right PNG, 합성 four-views PNG. 실제 렌더와 합성 시트를 열어 확인했다. 좌우 표기는 캐릭터 기준.
- 미실시: UV/텍스처/최종 토폴로지/본/웨이트/절단/Unity 적용/성능/애니메이션 검사. 다음은 사용자 블록아웃 검토와 필요한 비율 수정, Unity 실제 자산 기준 측정 후 상세 제작.
