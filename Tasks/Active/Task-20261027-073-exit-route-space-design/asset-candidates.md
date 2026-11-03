# A 탈출로 환경 에셋 후보 — 2026-11-02

담당 @RagSpirin / 브랜치 feat/exit-route/design-map. 후보 선정 결과이며 구매·최종 채택·배치는 아직 하지 않았다.

## 참고한 팀 자료

- [무료 에셋 및 유료 에셋 정리](https://hrjoo122770.atlassian.net/wiki/spaces/bYmkRlnpusng/pages/22544387): 본문은 [Figma 후보 보드](https://www.figma.com/design/1Zn3GnziPNSnmaOUzmBDEc/?node-id=0-1) 링크. 브라우저에서 후보 카드 이름/이미지 확인. 작성자 표시명은 이수; 사용자가 언급한 김현수와의 동일인 여부 미확정.
- [커튼 콜 에셋 관리와 검수 기준](https://hrjoo122770.atlassian.net/wiki/spaces/bYmkRlnpusng/pages/9469960): 3.1~3.3 환경 목록의 Abandoned Industrial Factory, Steampunk Clock Factory, POLYGON Apocalypse 등을 현재 보유 파일과 비교.
- 보드에는 Sci-Fi Pipes Pack Vol.01, Creepy Circus / 55+ Assets, Old Metro Tunnel / 63+ Assets, Sewer Modular Environment 및 전화기·거울·공구·실내 소품이 있다. 이번 선정은 A 환경에 필요한 부분을 선별한 것이며 전체 보드의 모든 상품을 검수한 것은 아니다.

## 추천 순서

현재의 콘크리트 산업 시설과 동선을 유지하면서 큰 배관·철골·설비의 반복으로 규모감을 만든다. 새 패키지 하나로 맵 전체를 교체하지 않는다. 아래 적용 위치와 우선순위는 조사에 따른 제안이다.

| 순서 | 후보 | A에서의 용도 | 판단 |
|---|---|---|---|
| 1 | 보유 AbandonedFactory 배관 SM_PipeMadule1 | 시작 전망과 하강 공동의 경로 밖 수직 배관 | 가장 먼저 재사용 비교. 원본 LOD/URP 렌더는 배치 전에 확인 |
| 2 | 보유 SM_Crane_Beam | 높은 상부 철골·구간별 층 기준 | 기존 그네/외줄과 분리해 시설 크기를 읽히게 함 |
| 3 | 보유 SM_Machinery_01 + SM_Control_Panel | 멀리 보이는 대표 설비와 실제 기능 옆 조작반 외형 | 큰 기계는 소수만. 새 가짜 버튼/우회 통로로 오해되지 않게 구성 |
| 4 | 보드 Sci-Fi Pipes Pack Vol.01 | 기존 배관으로 부족한 굵은 관·굽힘·끝마개 | 신규 도입 우선 후보. Unity 상품/URP 구성만 검토 |
| 5 | 보드 Old Metro Tunnel / 63+ Assets | 화염 복도 벽 부속·문틀·노후 시설 표현 | 완성 터널 대신 부품 선별. 벽/바닥 치수·구멍/동선은 유지 |
| 6 | 보드 Creepy Circus / 55+ Assets | 입구/전환부의 낡은 서커스 흔적 | 보조 후보. 텐트·매달린 장식의 대량 배치는 시야/비행 궤적을 가리므로 후순위 |

## 추가 도입 후보의 확인된 조건

2026-11-02 공식 상품 페이지 기준 표시 금액(USD, 세금 별도). 패키지 용량은 실행 중 메모리 사용량이 아니다. Unity 2022 지원 표시는 현재 Unity 6000.3/URP17에서 직접 검증했다는 뜻이 아니다.

| 공식 상품 | 표시 금액 / 용량 | 확인 사항 |
|---|---|---|
| [Sci-Fi Pipes Pack Vol.01](https://assetstore.unity.com/packages/3d/props/sci-fi-pipes-pack-vol-01-288457) | $29.99 / 2.6GB | URP 지원, 31프리팹, LOD 있음. 데모 맵은 HDRP이며 텍스처140장/최대4K. 사용할 부품/재질만 선별 |
| [Old Metro Tunnel / 63+ Assets](https://assetstore.unity.com/packages/3d/props/old-metro-tunnel-63-assets-264524) | $29.99 / 3.6GB | URP 지원 표기. 읽은 공식 페이지에서 LOD/측면 폐쇄 구조 상세를 확인하지 못했으므로 도입 조건으로 남김 |
| [Creepy Circus / 55+ Assets](https://marketplace.unity.com/packages/3d/props/creepy-circus-55-assets-285532?locale=ko-KR) | $39.99 / 2.7GB | URP 별도 구성, 기본 HDRP/데모 HDRP 전용. LOD 없음, 53메시/159프리팹, 평균11,296tris·자동 충돌체. 소량 후보 |

보드 가격만 보고 구매하지 않는다. 현재 로컬 경로/목록에서는 위 세 상품의 설치를 확인하지 못했으며 팀의 구매 보유 여부는 별개다. Unreal용 상품과 Unity용 상품도 구분한다.

## 기존 프리팹 사전 점검

Unity API로 읽은 LOD0 삼각형 수와 재질 상태. 지원 셰이더 표시는 실제 씬에서 정상 렌더되는지·다인 성능까지 검증한 결과가 아니다. 저장된 프리팹 bounds는 실제 배치 치수 승인에 사용하지 않는다.

| 프리팹 | LOD0 tris | 상태/선정 이유 |
|---|---:|---|
| SM_PipeMadule1 | 160 | 재질1·LOD 있음·충돌체1. 배관 반복 후보 |
| SM_Crane_Beam | 608 | 재질1·LOD 없음·충돌체1. 경로 밖 철골 후보 |
| SM_bridge | 1,356 | 재질1·LOD 있음·충돌체1. 외형 부품 보조 후보; 낭떠러지에 새 지름길/착지 발판을 만들지 않음 |
| SM_Control_Panel | 8,778 | 재질2·LOD 있음·충돌체1. 실제 작동 기물 주변 소수 배치 후보 |
| SM_Machinery_01 | 53,990 | 재질4·LOD 있음·충돌체1. 대표 설비 소수 후보 |
| SM_CS1_Ventilator_c1_Nanite | 37,520 | 작은 장식에 비해 LOD0 비용 큼. 반복용에서 후순위 |
| SM_Silo / SM_Metalpanel | 1,584 / 3,890 | Hidden/InternalErrorShader 검출. 원본 그대로 사용 보류; A 전용 재질/렌더 확인 후 재평가 |

경로: 배관/bridge/Silo/Metalpanel은 `Assets/_ThirdParty/LeartesStudios/AbandonedFactory/HDRP/Art/Prefabs/`, 나머지는 `Assets/_ThirdParty/LeartesStudios/HDRP/Art/Prefabs/`. 폴더 이름 HDRP만으로 실제 호환성을 판단하지 않는다.

## 후속 배치 기준

1. 입구에서 큰 배관·철골·심부 설비가 먼저 보이게 기존 빈 공간 바깥에 후보를 소량 비교한다.
2. 컨베이어/하강 구간은 그네 최대 진폭·플레이어 몸통·3인칭 카메라·대포 탄도와 분리한다. 제거한 벽/낮은 천장/회수층을 복원하지 않는다.
3. 복도 부속은 불꽃 피해 범위/예고 시야를 유지한다. 마지막 전원함·배관은 기존 다색 배선24가닥/톱 발사선을 가리지 않는다.
4. 기존 벽/바닥 충돌은 유지한다. 장식과 벽의 관통·겹침, 측면 구멍, 반복되는 이음새를 검사한다. 단면 메시를 양면 셰이더만으로 가리는 대신 필요한 폐쇄 구조를 확인한다.
5. 원본 프리팹/GUID는 보존하고 필요한 파생 재질/배치는 A에 한정한다. 근/원거리 LOD 소실을 확인하고 사용하지 않는 데모·텍스처·실시간 조명·스크립트는 가져오지 않는다.

이번 단계는 조사만 완료. A 준비 전/후 SHA256 동일, _ThirdParty 변경 없음, Play/MPPM 미실행. 에디터 종료 과정의 escape-labels 동적 폰트 캐시 변화는 별도 보존했다. 후보 원시 조사 결과는 `.harness/local/asset-prep-20261102/candidates.json`에 있다.

## 2026-11-02 선별 임포트

학원 NAS의 3D/소품에서 Industrial Elevator Modular와 Disel generator를 받아 Unity 프로젝트 밖에서 선별했다. 원본 두 패키지 합계 1,572,871,316바이트. 결과는 private Task073/SelectedEquipment에 보관한다.

- Elevator/FreightCar.prefab은 녹슨 화물칸 메시 하나(8,666 삼각형)와 원본의 3개 재질 슬롯을 사용한다. 전체 모듈 FBX에서 해당 메시만 독립 저장했다.
- Generator/Generator.prefab은 발전기 본체와 3단 LOD(2,662/1,570/718 삼각형)를 사용한다.
- URP Lit 재질 4개. 색/노멀 512~1,024, 금속도·AO·매끄러움을 합친 채널 텍스처 512, 압축과 mip streaming을 적용했다.
- .meta 제외 20개 파일, 10,311,906바이트. 두 프리팹의 의존성 밖 파일 0, 현재 A 씬의 신규 에셋 의존성 0. 두 프리팹의 편집 렌더를 확인했다. 게임 로딩 시간은 미측정.
