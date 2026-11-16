# 리메이크 설계에 적용할 조사 근거

## 경로별 도전 균형 재설계 — 추가 조사

2026-11-15 추가 지시: 원래 배치와 코스 순서는 고정 조건이 아니다. 모든 선택 경로의 체감 난이도와 기믹 밀도를 비슷하게 맞추며, 콘셉트의 봉→봉 전환을 실제 공통 기물로 구현한다.

- [Travis Hoffstetter의 Traversal Level Design Principles](https://www.gamedeveloper.com/design/traversal-level-design-principles): Tomb Raider 개발자가 공간의 높이·방향 변화, 반복 동작의 분절, 움직이는 환경, 명확한 이동 표면과 카메라 여유를 설명한다. 적이 있을 때 도주 가능성과 공격 가능한 공간도 함께 고려한다. 적용: 긴 단일 외줄 대신 짧은 균형→연속 봉→착지→다음 판단으로 나누고, 합류 전 도전 묶음을 비교한다.
- [Hazelight 디자인 리드 Oliver Granlund 인터뷰](https://80.lv/articles/hazelight-studios-working-culture-and-approach-to-creating-collaborative-gameplay): 협동 플레이의 다양성과 기획·코드·아트가 함께 반복 제작하는 방식을 설명한다. 적용: 공통 조작은 유지하면서 구역별로 역할·순서·공간 조건을 달리한다. It Takes Two의 두 사람 전용 능력이나 부활 규칙을 도입하는 근거는 아니다.
- [Jusant 디자이너 Lucas Boisseau의 직접 제작 기록](https://www.lucas-boisseau-game-designer.com/portfolio/jusant---don-t-nod-entertainment/mechanics-and-game-features/): 기존 도구를 에셋에 맞추고 손 위치·수치를 조정하는 일과 페이싱의 관계, 기믹을 방해하지 않는 아트 제작을 설명한다. 적용: 실제 공통 그립·2명 반동 궤적을 먼저 검증하고 보·앵커·배경을 맞춘다.
- [Jusant 내러티브 디자이너 Félix Elvis Le Pottier 인터뷰](https://www.pointnthink.fr/en/interview-felix-elvis-le-pottier-writer-narrative-designer/): 지역과 장소를 레벨 디자이너와 함께 구성하고 공간·소품으로 서사를 전달하는 협업을 설명한다. 적용: 검사동의 걸림/운반 구조, 열 회수실의 돌아 나오는 배관, 반출동의 교량을 실제 이동 조합과 일치시킨다.
- [한국닌텐도 TOTK 개발자 인터뷰 4장](https://www.nintendo.com/kr/interview/totk/04.html)을 재확인했다. 조합으로 생기는 서로 다른 경험과 읽히는 용도를 참고한다. 서로 다른 해법이 반드시 같은 시간·난이도라는 주장은 자료에 없으며, **경로 간 균형은 이번 사용자의 요구에 맞춘 별도 제작 기준**이다.

제작 기준: 같은 분기점→같은 합류점을 비교한다. 숙련 반복 시간뿐 아니라 기물 전환/타이밍 판단 수, 치명적 실수 구간, 부상 시 가능한 조작, 3·4인 대기/교대, 추격자 노출과 실제 공격 기회까지 기록한다. 긴 거리만 늘리거나 공통 기물 수치를 바꾸어 균형을 맞추지 않는다. 표본 시간 차이 20%를 초기 재검토 기준으로 쓰되 난이도 통과 판정으로 대신하지 않는다.

2026-11-15. 국내 공식 개발자 인터뷰, 해외 제작자 블로그, 개발 발표 자료를 읽고 현재 묘기·추격 시스템에 적용할 원칙을 정리했다. 인터넷 전체나 모든 명작의 설계를 조사했다는 뜻은 아니다. 아래 적용안은 이 프로젝트를 위한 제안이다.

| 자료와 직접 확인한 내용 | 이번 맵에 적용할 제안 |
| --- | --- |
| [BOTW 기술 디렉터 도타의 공식 글](https://www.nintendo.com/en-gb/News/2019/April/Discover-how-The-Legend-of-Zelda-Breath-of-the-Wild-works-in-VR-with-Nintendo-Labo--1547803.html): 개발 목표로 플레이어 스스로 방법과 재미를 찾는 multiplicative gameplay를 설명한다. | 묘기를 추가하기보다 기존 봉·대포·트램펄린·목마를 다른 공간 관계로 조합한다. |
| [한국닌텐도 TOTK 개발자 인터뷰 4장](https://www.nintendo.com/kr/interview/totk/04.html): 능력과 필드 조합으로 서로 다른 경험을 만들고, 기존 재료로 새 놀이를 시제품화했다. | 같은 출구에 서로 다른 묘기 조합으로 접근한다. 조립·마법·글라이더 같은 젤다의 새 능력을 가져오는 제안은 아니다. |
| [한국닌텐도 TOTK 개발자 인터뷰 5장](https://www.nintendo.com/kr/interview/totk/05.html): 플레이어가 생각한 시도를 지원하고, 만질 수 없는 물건은 외형으로 구분한다. | 여러 방법의 해법을 허용하되, 장식 계단·잡을 수 없는 봉처럼 보이는 부품은 제거하거나 형태를 명확히 바꾼다. |
| [닌텐도 공식 시작 가이드](https://www.nintendo.com/jp/zelda/totk/guide/en/index.html): 주변을 관찰하고 다른 방법을 실험하며 기물을 조합하도록 안내한다. | 선택 전 관찰 구역을 둔다. 발사 뒤에야 다음 기물이 보이는 배치, 위협 때문에 기계 주기를 한 번도 볼 수 없는 배치를 피한다. |
| [David Shaver, Invisible Intuition, GDC 2018](https://media.gdcvault.com/gdc2018/presentations/DShaver_Invisible_Intuition_GDC2018.pdf): 25·33·35쪽의 사용 가능성/불가능성 표현, 73~77·179쪽의 랜드마크, 112·119·158쪽의 구도·빛과 초기 테스트. 발표자 자신의 자료와 노트를 확인했다. | 출구를 여러 높이에서 다시 보고, 진행은 읽히되 전체 미궁을 한 번에 다 보이지 않게 한다. 첫 블록아웃부터 벽 개구·조명·사용 가능 모서리를 넣는다. |
| [Valve, Portal Post-Mortem, GDC 2008](https://cdn.fastly.steamstatic.com/apps/valve/2008/GDC2008_PortalPostMortem.pdf): 13~14쪽의 관찰 테스트, 30쪽의 낮은 스트레스에서 학습, 41~51쪽의 결말 강도·전달·퍼즐 단순화 시행착오. | 평온한 관찰→위협 속 재사용→조합 선택을 한 공간 안에서 설계한다. 마지막 배선 작업에서 처음으로 복잡한 조작을 배우게 하지 않는다. |
| [Valve, The AI Systems of Left 4 Dead, 2009](https://cdn.cloudflare.steamstatic.com/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf): 77~81·91쪽의 긴장 고저, 팀 상태 관찰, 난도와 페이싱 구분. | 팀원의 피해·강제 대기·실제 도주 진척에 맞춰 접근 빈도를 조절하는 방향을 검토한다. 발표의 시간 수치를 그대로 복사하거나 영구 속도 증가로 대체하지 않는다. |
| [Frictional Games, Thomas Grip의 공포 설계 회고](https://frictionalgames.com/2019-10-9-years-9-lessons-on-horror/): 단순 미궁+괴물이 짜증을 만들 수 있고, 불확실성과 선택이 공포에 중요하다고 설명한다. | 목적은 기억할 수 있고 회피 선택은 남아 있는 미궁. 어디서 나타날지 모르는 압박과 플레이어가 판단할 수 없는 사망을 구분한다. |
| [Alien: Isolation의 Al Hope 인터뷰](https://www.gamedeveloper.com/design/fear-in-unpredictability-i-alien-isolation-i-s-brand-of-survival-horror): 정해진 패턴을 외우는 공포보다 예측하기 어려운 사냥 경험을 강조한다. | 검증된 여러 출현 위치와 실제 은폐 이동을 사용한다. 눈앞 순간이동이나 대포 전면 봉쇄는 배제한다. |

## 해석의 경계

젤다의 모든 사당에 같은 4단계 공식이 있다고 단정하지 않는다. 이번 설계의 관찰→시도→조합→압박은 이 프로젝트를 위한 구성이다. 젤다의 밝은 신전 외형이나 무제한 문제 해결 능력을 이식하는 것이 아니다.

Uncharted·Titanfall 2의 구도 사례는 위 Shaver 발표에서 확인했다. BOTW GDC 공식 발표 소개와 국내 CEDEC 번역 블로그도 탐색했지만, 전체 영상을 시청했다고 기록하지 않는다. 핵심 근거는 위에서 직접 읽은 제작자 자료로 한정했다.

한 명 유인으로 다른 사람이 도망치는 것은 협동의 보상으로 남긴다. 장기간 안전한 어그로 고정만 제한하고, 공격하는 플레이어의 위치·회피에 관계없이 누군가를 반드시 죽이는 방식은 채택하지 않는다.

아트 이미지는 built-in imagegen으로 만든 검토용 콘셉트다. 실제 Unity 렌더나 충돌·도약 검증 결과가 아니며, 정확한 제작 동선은 remake-plan의 연결 관계와 이후 블록아웃 측정으로 결정한다.
