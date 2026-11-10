using System.Linq;
using System.Reflection;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    /// <summary>043 준비 공간과 마지막 4대의 저장 씬 연결·탄도 검사. 기존 작업 씬은 보존한다.</summary>
    public sealed class FinalCannonCourseTests
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject[] roots;
        Scene inspectedScene;
        Scene previousActiveScene;
        bool openedHere;

        [OneTimeSetUp]
        public void SetUp()
        {
            previousActiveScene = SceneManager.GetActiveScene();
            inspectedScene = SceneManager.GetSceneByPath(HumanCannonTestSceneBuilder.ScenePath);
            try
            {
                if (!inspectedScene.IsValid() || !inspectedScene.isLoaded)
                {
                    inspectedScene = EditorSceneManager.OpenScene(HumanCannonTestSceneBuilder.ScenePath, OpenSceneMode.Additive);
                    openedHere = true;
                }
                RestoreActiveScene();
                roots = inspectedScene.GetRootGameObjects();
                Assert.That(Find("FinalCannonCourse"), Is.Not.Null, "043 마지막 대포 구간이 저장된 씬이어야 합니다.");
            }
            catch
            {
                TearDown();
                throw;
            }
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            RestoreActiveScene();
            if (openedHere && inspectedScene.IsValid() && inspectedScene.isLoaded)
                EditorSceneManager.CloseScene(inspectedScene, true);
            openedHere = false;
        }

        void RestoreActiveScene()
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded
                && SceneManager.GetActiveScene() != previousActiveScene)
                SceneManager.SetActiveScene(previousActiveScene);
        }

        [Test]
        public void TenUniqueStations_RegisterFourFinalCannonsAfterExistingSix()
        {
            var systems = All<HumanCannonSystem>();
            Assert.That(systems, Has.Length.EqualTo(1));
            var stations = Field<HumanCannonStation[]>(systems[0], "stations");
            Assert.That(stations, Has.Length.EqualTo(10));
            Assert.That(stations, Has.All.Not.Null);
            CollectionAssert.AllItemsAreUnique(stations);
            CollectionAssert.AreEquivalent(All<HumanCannonStation>(), stations);
            var finalRoot = Find("FinalCannonCourse");
            Assert.That(finalRoot.GetComponentsInChildren<HumanCannonStation>(true), Has.Length.EqualTo(4));
            for (int i = 0; i < 4; i++)
                Assert.That(stations[6 + i].transform, Is.SameAs(finalRoot.Find("FinalCannon" + (i + 1))));
        }

        [Test]
        public void FinalCannons_HaveEqualHeightDirectionVelocityAndAirControl()
        {
            var stations = FinalStations();
            for (int i = 0; i < stations.Length; i++)
            {
                var station = stations[i];
                // 2026-11-02 담당자 결정: 대포+갑판을 갑판 뒤끝이 초록 쿠션 중간에 오도록 당김(z152 → 149.76 → 147.26).
                // 2026-11-02 담당자 결정: 대포 중심 간격 4m → 6m.
                // 2026-11-02 앞 다리(차대)가 바닥 끝을 넘지 않도록 0.135m 뒤로(147.26 → 147.125).
                Assert.That(Vector3.Distance(station.transform.position, new Vector3(-9 + 6 * i, 6.2f, 147.125f)),
                    Is.LessThan(0.001f), station.name);
                Assert.That(Quaternion.Angle(station.transform.rotation, Quaternion.identity), Is.LessThan(0.001f), station.name);
                Assert.That(station.Muzzle.position.y, Is.EqualTo(stations[0].Muzzle.position.y).Within(0.001f), station.name);
                Assert.That(station.Muzzle.position.z, Is.EqualTo(stations[0].Muzzle.position.z).Within(0.001f), station.name);
                Assert.That(Vector3.Angle(station.Muzzle.forward, Vector3.forward), Is.LessThan(0.001f), station.name);
                // 2026-11-02 담당자 결정: 무조작 착지가 도착지점 중간이 되도록 앞 속도 8 → 11.74 → 11.79(위 속도 12 유지).
                Assert.That(Vector3.Distance(station.LaunchVelocity, new Vector3(0, 12, 11.79f)), Is.LessThan(0.001f), station.name);
                Assert.That(station.AirControlSpeed, Is.EqualTo(3f).Within(0.001f), station.name);
                Assert.That(station.BoardingDistance, Is.EqualTo(2.5f).Within(0.001f), station.name);
            }
        }

        [Test]
        public void CushionAndPreparationDeck_JoinAtSameSafeHeightAndSupportEveryBoardingPoint()
        {
            var cushion = SafeSurface("LandingCushion");
            var deck = SafeSurface("FinalCannonCourse/PreparationDeck");
            Assert.That(cushion.bounds.max.y, Is.EqualTo(6.2f).Within(0.001f));
            Assert.That(deck.bounds.max.y, Is.EqualTo(cushion.bounds.max.y).Within(0.001f));
            // 2026-11-02 담당자 결정: 갑판을 쿠션 중간(z144)까지 당기고 덮인 쿠션 앞쪽 절반은 잘라 이어 붙인다.
            // 2026-11-02 담당자 결정: 초록 뒤 경사를 대포 쪽으로 1.5m 당기고 쿠션 뒤끝도 같이 줄인다(141.5 → 143).
            Assert.That(cushion.bounds.min.z, Is.EqualTo(143f).Within(0.001f));
            Assert.That(cushion.bounds.max.z, Is.EqualTo(144f).Within(0.001f));
            Assert.That(deck.bounds.min.z, Is.EqualTo(cushion.bounds.max.z).Within(0.001f));
            float overlapMin = Mathf.Max(cushion.bounds.min.x, deck.bounds.min.x);
            float overlapMax = Mathf.Min(cushion.bounds.max.x, deck.bounds.max.x);
            Assert.That(overlapMax - overlapMin, Is.GreaterThan(2f * GameSettings.Base.Character.CapsuleRadius));
            float crossingX = (overlapMin + overlapMax) * 0.5f;
            Assert.That(cushion.GetComponent<HumanCannonLandingZone>().SupportsLanding(
                new Vector3(crossingX, cushion.bounds.max.y, cushion.bounds.max.z - 0.05f)), Is.True);
            var deckZone = deck.GetComponent<HumanCannonLandingZone>();
            Assert.That(deckZone.SupportsLanding(new Vector3(crossingX, deck.bounds.max.y, deck.bounds.min.z + 0.05f)), Is.True);
            foreach (var station in FinalStations())
            {
                Assert.That(deckZone.SupportsLanding(station.BoardingPoint.position), Is.True, station.name + " boarding needs safe footing");
                float legFront = station.GetComponentsInChildren<Renderer>()
                    .Where(r => r.enabled && r.bounds.min.y <= deck.bounds.max.y + 0.05f).Max(r => r.bounds.max.z);
                Assert.That(legFront, Is.EqualTo(deck.bounds.max.z).Within(0.005f), station.name + " 앞 다리가 바닥 끝에 딱 맞아야 한다");
            }
            // 2026-11-02 담당자 결정: 발사 방향 쪽 갑판 앞끝은 지상 바닥(2구간 바닥) 끝에 맞춘다(1m 여유 결정 대체).
            float groundFront = new[] { "Floor_0_1", "Floor_1_1", "Floor_2_1" }
                .Max(n => Find("SplitRouteCourse/SecondCourseFloor/" + n).GetComponent<Collider>().bounds.max.z);
            Assert.That(deck.bounds.max.z, Is.EqualTo(groundFront).Within(0.001f), "대포 바닥 앞끝 = 지상 바닥 끝");
        }

        [Test]
        public void FourUnsteeredFinalFlights_LandAtMiddleOfSafeLandingPad()
        {
            // 2026-11-02 담당자 결정: 도착지점을 5m 뒤로(z169.33 → 174.33) 옮긴 뒤, 최종 대포 앞 속도를 조절해
            // 조작 없이 도착지점 중간에 내린다.
            var pad = SafeSurface("FinalCannonCourse/FlightLandingPad");
            var zone = pad.GetComponent<HumanCannonLandingZone>();
            // 2026-11-02 대포 간격 6m에 맞춰 바깥 탄도에서 4m 여유를 유지: 폭 20 → 26.
            Assert.That(pad.bounds.size.x, Is.EqualTo(26f).Within(0.001f));
            var deck = SafeSurface("FinalCannonCourse/PreparationDeck");
            Assert.That(deck.bounds.size.x, Is.EqualTo(24f).Within(0.001f), "대포 바닥은 바깥 대포 중심에서 3m 여유");
            Assert.That(pad.bounds.size.z, Is.EqualTo(10f).Within(0.001f));
            Assert.That(pad.bounds.min.z, Is.EqualTo(174.33f).Within(0.01f));
            Assert.That(pad.bounds.max.y, Is.EqualTo(6.2f).Within(0.001f));
            float gravity = GameSettings.Base.Character.Gravity;
            Assert.That(gravity, Is.GreaterThan(0));
            float bodyRadius = GameSettings.Base.Character.CapsuleRadius;
            foreach (var station in FinalStations())
            {
                Vector3 origin = station.Muzzle.position;
                Vector3 velocity = station.LaunchVelocity;
                float discriminant = velocity.y * velocity.y + 2f * gravity * (origin.y - pad.bounds.max.y);
                Assert.That(discriminant, Is.GreaterThanOrEqualTo(0), station.name);
                float time = (velocity.y + Mathf.Sqrt(discriminant)) / gravity;
                Assert.That(time, Is.GreaterThan(0));
                Assert.That(velocity.y - gravity * time, Is.LessThan(0), "하강 중 착지해야 합니다.");
                Vector3 impact = origin + velocity * time + Vector3.down * (0.5f * gravity * time * time);
                Assert.That(impact.x, Is.InRange(pad.bounds.min.x + bodyRadius, pad.bounds.max.x - bodyRadius), station.name);
                Assert.That(impact.z, Is.EqualTo(pad.bounds.center.z).Within(0.1f), station.name + " 조작 없이 도착지점 중간");
                Assert.That(zone.SupportsLanding(impact), Is.True, station.name + " must finish as a safe landing");
            }
        }

        [Test]
        public void FinaleRings_CenterEachUnsteeredFlightAtRiseApexAndFall()
        {
            // 2026-11-02 담당자 결정: 대포마다 조작 없는 탄도 위 링 3개(상승·정점·하강), 총 12개. 충돌 없음.
            var root = Find("FinalCannonCourse/FinaleRings");
            Assert.That(root, Is.Not.Null);
            // 2026-11-02 담당자 결정: 링은 지우지 않고 숨겨 둔다(다시 켜면 위치는 탄도에 맞다).
            Assert.That(root.gameObject.activeSelf, Is.False, "피날레 링은 숨김 상태");
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty, "링은 비행을 막지 않는다.");
            float gravity = GameSettings.Base.Character.Gravity;
            float bodyCenter = GameSettings.Base.Character.CapsuleHeight * 0.5f;
            float inner = HumanCannonFinaleRingBuilder.RingRadius - HumanCannonFinaleRingBuilder.TubeRadius;
            Assert.That(bodyCenter + GameSettings.Base.Character.CapsuleRadius, Is.LessThan(inner), "서 있는 몸 전체가 링 안을 지나야 한다.");
            var stations = FinalStations();
            Assert.That(root.childCount, Is.EqualTo(stations.Length * 3));
            foreach (var station in stations)
            {
                Vector3 origin = station.Muzzle.position, velocity = station.LaunchVelocity;
                float apex = velocity.y / gravity;
                float[] times = { apex * 0.5f, apex, apex * 1.5f };
                for (int i = 0; i < times.Length; i++)
                {
                    var ring = root.Find(station.name + "_Ring" + (i + 1));
                    Assert.That(ring, Is.Not.Null, station.name + " ring " + (i + 1));
                    float t = times[i];
                    Vector3 expected = origin + velocity * t + Vector3.down * (0.5f * gravity * t * t) + Vector3.up * bodyCenter;
                    Assert.That(Vector3.Distance(ring.position, expected), Is.LessThan(0.001f), ring.name);
                    Vector3 tangent = (velocity + Vector3.down * (gravity * t)).normalized;
                    Assert.That(Vector3.Angle(ring.forward, tangent), Is.LessThan(0.01f), ring.name + " 링 면이 진행 방향을 마주봐야 한다.");
                    Assert.That(ring.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null, ring.name);
                }
            }
        }

        [Test]
        public void GreenCushion_WrapsPreparationDeckOnBackLeftAndRight()
        {
            // 2026-11-02 담당자 결정: 초록 쿠션이 대포 바닥의 뒤·좌·우를 2.5m 폭으로 감싼다.
            var deck = SafeSurface("FinalCannonCourse/PreparationDeck").bounds;
            var back = SafeSurface("LandingCushion").bounds;
            var left = SafeSurface("LandingCushion_Left").bounds;
            var right = SafeSurface("LandingCushion_Right").bounds;
            Assert.That(back.max.z, Is.EqualTo(deck.min.z).Within(0.001f));
            Assert.That(back.min.x, Is.EqualTo(deck.min.x - 2.5f).Within(0.001f));
            Assert.That(back.max.x, Is.EqualTo(deck.max.x + 2.5f).Within(0.001f));
            Assert.That(left.max.x, Is.EqualTo(deck.min.x).Within(0.001f));
            Assert.That(left.min.x, Is.EqualTo(back.min.x).Within(0.001f));
            Assert.That(right.min.x, Is.EqualTo(deck.max.x).Within(0.001f));
            Assert.That(right.max.x, Is.EqualTo(back.max.x).Within(0.001f));
            Assert.That(Find("SplitRouteCourse/SecondSectionPoleGate/ReceivingArea"), Is.Null, "옛 쿠션 테두리 선은 남기지 않는다.");
            // 2026-11-02 담당자 결정: 초록 쿠션과 대포 바닥 외형을 하나의 매끈한 초록 면(FinalGreenGround)으로 합친다.
            // 지상에 닿는 바깥 3면(뒤·좌·우)은 1.2m 경사로 지면까지 내린다. 외형 전용이며 충돌·착지는 기존 4개 구역이 맡는다.
            Assert.That(Find("LandingCushionSurround"), Is.Null);
            Assert.That(Find("LandingCushionRamp"), Is.Null);
            var green = Find("FinalGreenGround");
            Assert.That(green, Is.Not.Null);
            Assert.That(green.GetComponentsInChildren<Collider>(true), Is.Empty);
            var visual = green.GetComponent<Renderer>().bounds;
            Assert.That(visual.min.x, Is.EqualTo(back.min.x - 1.2f).Within(0.01f));
            Assert.That(visual.max.x, Is.EqualTo(back.max.x + 1.2f).Within(0.01f));
            Assert.That(visual.min.z, Is.EqualTo(back.min.z - 1.2f).Within(0.01f));
            Assert.That(visual.max.z, Is.EqualTo(deck.max.z).Within(0.01f), "지상이 없는 앞쪽(z148 너머)으로는 내리지 않는다");
            Assert.That(visual.min.y, Is.EqualTo(6f).Within(0.01f), "바깥 끝은 지상 높이");
            Assert.That(visual.max.y, Is.EqualTo(deck.max.y).Within(0.005f), "윗면은 실제 착지면 높이");
            foreach (var zone in new[] { "FinalCannonCourse/PreparationDeck", "LandingCushion", "LandingCushion_Left", "LandingCushion_Right" })
                Assert.That(Find(zone).GetComponentsInChildren<Renderer>().Any(r => r.enabled), Is.False, zone + " 옛 외형은 숨긴다");
            // 2026-11-04 사용자 결정: 초록 면 전체가 안전지대다. 경사 테두리 아래 지상(6.0)에 떨어져도 부상당하지 않는다.
            var greenLanding = Find("FinalCannonCourse/FinalGreenGroundLanding").GetComponent<HumanCannonLandingZone>();
            Assert.That(greenLanding, Is.Not.Null);
            foreach (var feet in new[] { new Vector3(visual.min.x + .1f, 6f, visual.min.z + .1f), new Vector3(visual.max.x - .1f, 6f, visual.max.z - .1f),
                new Vector3(visual.center.x, 6f, visual.min.z + .1f) })
                Assert.That(greenLanding.SupportsLanding(feet), Is.True, "초록 면 " + feet);
            foreach (var feet in new[] { new Vector3(visual.center.x, 6f, visual.min.z - .3f), new Vector3(visual.max.x + .3f, 6f, visual.center.z) })
                Assert.That(greenLanding.SupportsLanding(feet), Is.False, "초록 면 밖 " + feet);
            foreach (var side in new[] { left, right })
            {
                Assert.That(side.min.z, Is.EqualTo(deck.min.z).Within(0.001f));
                Assert.That(side.max.z, Is.EqualTo(deck.max.z).Within(0.001f), "옆 쿠션 앞끝도 대포 바닥·지상 바닥 끝에 맞춘다");
                Assert.That(side.max.y, Is.EqualTo(deck.max.y).Within(0.001f));
            }
        }

        [Test]
        public void FinalCannons_HaveNoFloorNumberLabels()
        {
            // 2026-11-02 담당자 결정: 대포 앞 바닥의 흰 숫자 1~4 삭제.
            for (int i = 1; i <= 4; i++) Assert.That(Find("FinalCannonCourse/CannonLabel" + i), Is.Null);
        }

        [Test]
        public void LandingPad_HasNoFinishMarkersOrLabels()
        {
            // 2026-11-02 담당자 결정: 도착지점의 초록 테두리·기둥·현수막과 FINISH 글자(월드 라벨 포함) 삭제. 착지대는 유지.
            Assert.That(Find("FinalCannonCourse/DestinationMarkers"), Is.Null);
            Assert.That(Find("FinalCannonCourse/FlightLandingPad"), Is.Not.Null);
            var finishTexts = roots.SelectMany(r => r.GetComponentsInChildren<TMPro.TMP_Text>(true))
                .Where(t => t.text.Trim().ToUpperInvariant() == "FINISH").Select(t => t.name).ToArray();
            Assert.That(finishTexts, Is.Empty);
        }

        [Test]
        public void LandingPad_HasSixFireworkPropsInARowOnTheFarEdge()
        {
            // 2026-11-02 담당자 결정: 폭죽 소품 6개를 도착지점 상단(대포 반대쪽 끝)에 한 줄로(장식, 충돌 없음). 처음엔 좌우 3개씩이었다.
            var pad = SafeSurface("FinalCannonCourse/FlightLandingPad").bounds;
            var root = Find("FinalCannonCourse/FinishFireworks");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.childCount, Is.EqualTo(6));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty, "소품은 착지·이동을 막지 않는다");
            foreach (Transform firework in root)
            {
                var bounds = firework.GetComponentInChildren<Renderer>().bounds;
                Assert.That(bounds.min.y, Is.EqualTo(pad.max.y).Within(0.01f), firework.name + " 바닥 위에 선다");
                Assert.That(bounds.size.y, Is.LessThan(1.2f), firework.name + " 막대는 거의 없게 줄인 짧은 폭죽");
                Assert.That(bounds.center.z, Is.EqualTo(pad.max.z - 0.5f).Within(0.05f), firework.name + " 도착지점 상단 끝");
                Assert.That(bounds.center.x, Is.InRange(pad.min.x, pad.max.x), firework.name);
                // 2026-11-02 담당자 결정: 공연 첫 도착 때 한 번 색종이·불꽃이 함께 터진다.
                var confetti = firework.Find("Confetti")?.GetComponent<ParticleSystem>();
                // 2026-11-02 담당자 결정: 불꽃(Sparks)을 후방 불벽(빨간 반투명 메시)과 다른 황금빛 불길 파티클(Flame)로 교체.
                var flame = firework.Find("Flame")?.GetComponent<ParticleSystem>();
                Assert.That(firework.Find("Sparks"), Is.Null, firework.name + " 옛 불꽃은 남기지 않는다");
                Assert.That(confetti, Is.Not.Null, firework.name);
                Assert.That(flame, Is.Not.Null, firework.name);
                // 2026-11-02 담당자 결정: 파티클은 폭죽 머리에서 나오고 색종이는 높이 솟는다.
                var nose = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                Assert.That(Vector3.Distance(confetti.transform.position, nose), Is.LessThan(0.05f), firework.name + " 색종이 발사 위치");
                Assert.That(Vector3.Distance(flame.transform.position, nose), Is.LessThan(0.05f), firework.name + " 불길 발사 위치");
                Assert.That(flame.shape.radius * flame.transform.lossyScale.x, Is.LessThan(0.2f), firework.name + " 불길은 머리 한 점에서 나온다");
                Assert.That(Vector3.Angle(flame.transform.forward, Vector3.up), Is.LessThan(1f), firework.name + " 불길은 위로 뿜는다");
                Assert.That(confetti.main.scalingMode, Is.EqualTo(ParticleSystemScalingMode.Hierarchy), "3배 폭죽 아래에서도 실제 크기·속도");
                // 2026-11-02 담당자 결정: 색종이 머리 위 약 6m(속도 7.6~9.4), 불길 약 4m(속도 7.3~10.2).
                Assert.That(confetti.main.startSpeed.constantMin, Is.EqualTo(7.6f).Within(0.01f), firework.name + " 색종이 발사 속도");
                Assert.That(confetti.main.startSpeed.constantMax, Is.EqualTo(9.4f).Within(0.01f), firework.name + " 색종이 발사 속도");
                Assert.That(flame.main.startSpeed.constantMin, Is.EqualTo(7.3f).Within(0.02f), firework.name + " 불길 발사 속도");
                Assert.That(flame.main.startSpeed.constantMax, Is.EqualTo(10.22f).Within(0.02f), firework.name + " 불길 발사 속도");
            }
            var celebration = root.GetComponent<HumanCannonFinishCelebration>();
            Assert.That(celebration, Is.Not.Null);
            Assert.That(celebration.Bursts, Has.Length.EqualTo(12));
            foreach (var burst in celebration.Bursts)
            {
                Assert.That(burst, Is.Not.Null);
                foreach (var system in burst.GetComponentsInChildren<ParticleSystem>(true))
                {
                    Assert.That(system.main.loop, Is.False, system.name + " 한 번만 터진다");
                    Assert.That(system.main.playOnAwake, Is.False, system.name + " 도착 전에는 재생하지 않는다");
                    Assert.That(system.main.cullingMode, Is.EqualTo(ParticleSystemCullingMode.AlwaysSimulate),
                        system.name + " 화면 밖 컬링으로 0초 버스트를 건너뛰면 안 된다");
                    Assert.That(system.main.useUnscaledTime, Is.True,
                        system.name + " 공연 성공 시 Time.timeScale=0 멈춤 중에도 끝까지 터진다");
                }
            }
        }

        [Test]
        public void FinalLaunches_DoNotBelongToEitherRouteFireSection()
        {
            var route = All<HumanCannonRouteSystem>().Single();
            Assert.That(route.FireSectionForStation(-1), Is.EqualTo(-1));
            for (int i = 0; i < 3; i++) Assert.That(route.FireSectionForStation(i), Is.EqualTo(0));
            for (int i = 3; i < 6; i++) Assert.That(route.FireSectionForStation(i), Is.EqualTo(1));
            for (int i = 6; i < 10; i++) Assert.That(route.FireSectionForStation(i), Is.EqualTo(-1));
        }

        [Test]
        public void FinalCourse_RemovesAirSawTracksAndEntryTriggerWhileKeepingAllFourCannons()
        {
            // Task-044 2026-10-22 실플레이 피드백은 이전 빌더의 최종 공중 톱 배치보다 우선한다.
            Assert.That(All<HumanCannonFinalSaws>(), Is.Empty, "공중 장애물 제거 후 공유 주기를 실행할 사본도 남기지 않는다.");
            Assert.That(Find("FinalCannonCourse/FinalSaws"), Is.Null, "톱 외형·경로·예고와 EntryZone 트리거를 함께 제거한다.");
            var stations = FinalStations();
            Assert.That(stations, Has.Length.EqualTo(4));
            Assert.That(stations, Has.All.Not.Null);
            CollectionAssert.AllItemsAreUnique(stations);
            SafeSurface("FinalCannonCourse/PreparationDeck");
            SafeSurface("FinalCannonCourse/FlightLandingPad");
        }

        HumanCannonStation[] FinalStations()
        {
            var stations = Field<HumanCannonStation[]>(All<HumanCannonSystem>().Single(), "stations");
            Assert.That(stations, Has.Length.EqualTo(10));
            return stations.Skip(6).ToArray();
        }

        BoxCollider SafeSurface(string path)
        {
            var surface = Find(path);
            Assert.That(surface, Is.Not.Null, path);
            var collider = surface.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null, path);
            Assert.That(collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger, Is.True, path);
            Assert.That(surface.GetComponent<HumanCannonLandingZone>(), Is.Not.Null, path);
            return collider;
        }

        T[] All<T>() where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        Transform Find(string path)
        {
            int separator = path.IndexOf('/');
            string rootName = separator < 0 ? path : path.Substring(0, separator);
            var root = roots.SingleOrDefault(item => item.name == rootName);
            return root == null ? null : separator < 0 ? root.transform : root.transform.Find(path.Substring(separator + 1));
        }

        static T Field<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, PrivateInstance);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            return (T)field.GetValue(target);
        }
    }
}
