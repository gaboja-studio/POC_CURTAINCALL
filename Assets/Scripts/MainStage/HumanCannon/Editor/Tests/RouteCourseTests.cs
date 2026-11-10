using System.Linq;
using System.Reflection;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    /// <summary>044 연장된 저장 씬의 배치·연결 검사. 테스트가 연 씬만 닫고 기존 작업 씬은 보존한다.</summary>
    public sealed class RouteCourseTests
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
                Assert.That(Find("SplitRouteCourse"), Is.Not.Null, "042 코스가 저장된 씬이어야 합니다.");
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
        public void OriginalSixCannons_RemainThreeDepartureAndThreeShelterStations()
        {
            var systems = All<HumanCannonSystem>();
            Assert.That(systems, Has.Length.EqualTo(1));
            var stations = Field<HumanCannonStation[]>(systems[0], "stations");
            Assert.That(stations.Length, Is.GreaterThanOrEqualTo(6));
            Assert.That(stations, Has.All.Not.Null);
            CollectionAssert.AllItemsAreUnique(stations);
            CollectionAssert.AreEquivalent(All<HumanCannonStation>(), stations);
            for (int i = 0; i < 6; i++)
            {
                Assert.That(stations[i].transform.position.y, Is.EqualTo(i < 3 ? 4f : 6f).Within(0.001f));
                Assert.That(stations[i].transform.position.z, Is.EqualTo(i < 3 ? 3f : 79f).Within(0.001f));
                Assert.That(stations[i].Muzzle, Is.Not.Null);
            }
        }

        [TestCase(0, "FirstCatch")]
        [TestCase(3, "SecondCatch")]
        public void ThreeUnsteeredTrajectories_DescendInsideTheirReceivingArea(int firstStation, string targetName)
        {
            var system = All<HumanCannonSystem>().Single();
            var stations = Field<HumanCannonStation[]>(system, "stations");
            Assert.That(stations.Length, Is.GreaterThanOrEqualTo(6));
            var trampoline = All<HumanCannonTrampoline>().Single();
            Transform target = Find("SplitRouteCourse/" + targetName);
            Assert.That(target, Is.Not.Null);
            float gravity = GameSettings.Base.Character.Gravity;
            Assert.That(gravity, Is.GreaterThan(0));
            float surfaceY = target.position.y + Field<float>(trampoline, "surfaceHeight");
            for (int i = firstStation; i < firstStation + 3; i++)
            {
                var station = stations[i];
                Vector3 origin = station.Muzzle.position;
                Vector3 velocity = station.LaunchVelocity;
                float discriminant = velocity.y * velocity.y + 2f * gravity * (origin.y - surfaceY);
                Assert.That(discriminant, Is.GreaterThanOrEqualTo(0), station.name + " cannot reach receiving height");
                float time = (velocity.y + Mathf.Sqrt(discriminant)) / gravity;
                Assert.That(time, Is.GreaterThan(0));
                Assert.That(velocity.y - gravity * time, Is.LessThan(0), "받기는 하강 궤적이어야 합니다.");
                Vector3 impact = origin + velocity * time + Vector3.down * (0.5f * gravity * time * time);
                Vector3 offset = impact - target.position;
                Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(trampoline.HalfSize.x), station.name + " x misses receiving area");
                Assert.That(Mathf.Abs(offset.z), Is.LessThanOrEqualTo(trampoline.HalfSize.y), station.name + " z misses receiving area");
                Assert.That(impact.y, Is.EqualTo(surfaceY).Within(0.001f));
            }
        }

        [Test]
        public void DepartureGroundAndFirstCourse_HaveNoWalkableColliderAcrossTheirGap()
        {
            var ground = FloorBounds(Find("Ground"));
            var floor = FloorBounds(Find("SplitRouteCourse/FirstCourseFloor"));
            Assert.That(ground.max.z, Is.EqualTo(7).Within(0.001f));
            Assert.That(floor.min.z, Is.EqualTo(16).Within(0.001f));
            Assert.That(ground.max.z, Is.LessThan(floor.min.z));
            float jumpHeight = GameSettings.Base.Character.JumpHeight;
            var bridges = All<Collider>().Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger)
                .Where(c => c.bounds.min.z < 15.999f && c.bounds.max.z > 7.001f
                    && c.bounds.min.x < 22f && c.bounds.max.x > -22f
                    && c.bounds.min.y <= jumpHeight && c.bounds.max.y >= -0.001f)
                .Select(c => c.name).ToArray();
            Assert.That(bridges, Is.Empty, "출발 무대와 첫 코스 사이의 도보 틈을 Collider가 메우면 안 됩니다.");
        }

        [Test]
        public void ShelterDeck_RequiresLiftOrFlightToReachItsSixMeterTop()
        {
            var deck = FloorBounds(Find("SplitRouteCourse/ShelterDeck"));
            var floor = FloorBounds(Find("SplitRouteCourse/FirstCourseFloor"));
            Assert.That(deck.max.y, Is.EqualTo(6).Within(0.001f));
            Assert.That(deck.max.y - floor.max.y,
                Is.GreaterThan(GameSettings.Base.Character.JumpHeight), "일반 점프로 쉼터 승강을 우회하면 안 됩니다.");
            Assert.That(Find("SplitRouteCourse/ShelterDeck").GetComponent<HumanCannonLandingZone>(), Is.Not.Null);
        }

        [Test]
        public void Route_ConnectsThreeTransportsAndShelterWithGroundToDeckLift()
        {
            var routes = All<HumanCannonRouteSystem>();
            Assert.That(routes, Has.Length.EqualTo(1));
            var route = routes[0];
            var transports = Field<HumanCannonTransport[]>(route, "transports");
            Assert.That(transports, Has.Length.EqualTo(3));
            Assert.That(transports, Has.All.Not.Null);
            CollectionAssert.AllItemsAreUnique(transports);
            CollectionAssert.AreEquivalent(All<HumanCannonTransport>(), transports);
            Assert.That(transports.All(t => t.IsConfigured), Is.True);
            Assert.That(transports[0].name, Is.EqualTo("DepartureTransport"));
            Assert.That(transports[1].name, Is.EqualTo("ShelterLift"));
            Assert.That(transports[2].name, Is.EqualTo("ShelterDeparture"));
            var shelter = Field<HumanCannonLandingZone>(route, "shelter");
            Assert.That(shelter, Is.SameAs(Find("SplitRouteCourse/ShelterDeck").GetComponent<HumanCannonLandingZone>()));
            // 2026-10-30 리프트 5.5×4: 접근로 끝·쉼터 갑판 가장자리에 붙인 위치.
            Assert.That(Vector3.Distance(transports[1].StartPosition, new Vector3(-31.75f, 0, 68)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(transports[1].Position(1), new Vector3(-24.75f, 6, 78)), Is.LessThan(0.001f));
            // 정면 대각선으로 갑판 높이까지 오른 뒤 오른쪽으로 이동한다(한 번에 대각선으로 가로지르지 않는다).
            var lift = transports[1];
            Assert.That(lift.Via, Is.Not.Null);
            Assert.That(Vector3.Distance(lift.Via.position, new Vector3(-31.75f, 6, 78)), Is.LessThan(0.001f));
            float first = Vector3.Distance(lift.StartPosition, lift.Via.position);
            float total = first + Vector3.Distance(lift.Via.position, lift.Position(1));
            for (int i = 0; i <= 10; i++)
            {
                float progress = i / 10f;
                var point = lift.Position(progress);
                if (progress * total <= first) Assert.That(point.x, Is.EqualTo(lift.StartPosition.x).Within(0.001f), "올라가는 동안 옆으로 움직이지 않는다");
                else Assert.That(point.y, Is.EqualTo(6).Within(0.001f), "갑판 높이에서 오른쪽으로 이동한다");
            }
            Assert.That(transports[2].StartPosition.y, Is.EqualTo(6).Within(0.001f));
            Assert.That(transports[2].Position(1).y, Is.EqualTo(6).Within(0.001f));
        }

        [Test]
        public void GroundMarkers_IncludeBothUnsafeCourseFloorsWithoutChangingLandingRules()
        {
            var markers = All<HumanCannonGroundMarkers>().Single();
            var expected = Find("SplitRouteCourse/FirstCourseFloor").GetComponentsInChildren<Collider>()
                .Concat(Find("SplitRouteCourse/SecondCourseFloor").GetComponentsInChildren<Collider>())
                .Where(c => c.enabled && !c.isTrigger).ToArray();
            Assert.That(expected, Is.Not.Empty);
            CollectionAssert.AreEquivalent(expected, Field<Collider[]>(markers, "courseFloors"));
            Assert.That(Field<Material>(markers, "markerMaterial"), Is.Not.Null);
        }

        [Test]
        public void TwoFireSections_AreEnabledWithThirtySecondWarningAndOriginalSpeed()
        {
            var route = All<HumanCannonRouteSystem>().Single();
            var fires = Field<HumanCannonFireWall[]>(route, "fires");
            Assert.That(fires, Has.Length.EqualTo(2));
            Assert.That(fires, Has.All.Not.Null);
            CollectionAssert.AllItemsAreUnique(fires);
            CollectionAssert.AreEquivalent(All<HumanCannonFireWall>(), fires);
            Assert.That(fires.All(f => f.IsConfigured), Is.True);
            // 2구간 불은 숨김·사용 안 함(2026-11-03 담당자 결정). 배치·참조는 남겨 다시 켤 수 있다.
            Assert.IsTrue(fires[0].Available && fires[0].gameObject.activeSelf, "1구간 불꽃벽은 사용한다.");
            Assert.IsFalse(fires[1].Available || fires[1].gameObject.activeSelf, "2구간 불꽃벽은 숨긴다.");
            // 1구간 불 예고 25초(2026-11-03 담당자 결정). 숨긴 2구간 불은 기존 30초 유지.
            Assert.That(Field<float>(fires[0], "warningSeconds"), Is.EqualTo(25f));
            Assert.That(Field<float>(fires[1], "warningSeconds"), Is.EqualTo(30f));
            Assert.That(fires.All(f => Mathf.Approximately(Field<float>(f, "speed"), 0.7f)), Is.True);
            Assert.That(fires.All(f => !Field<Transform>(f, "visual").gameObject.activeSelf), Is.True);
            var firstBoundary = Field<Transform>(fires[0], "boundary");
            var secondBoundary = Field<Transform>(fires[1], "boundary");
            var shelter = FloorBounds(Find("SplitRouteCourse/ShelterDeck"));
            var cushion = Find("LandingCushion").GetComponent<Collider>();
            // 2026-11-03: 1구간 불은 멈추지 않고 쉼터·2구간을 지나 도착 코스 앞 최종 대포 4대 줄까지 온다.
            var finals = Field<HumanCannonStation[]>(All<HumanCannonSystem>().Single(), "stations").Where(s => s.name.StartsWith("FinalCannon")).ToArray();
            Assert.That(finals, Has.Length.EqualTo(4));
            Assert.That(finals.All(s => Mathf.Abs(s.transform.position.z - firstBoundary.position.z) < 0.01f), Is.True);
            Assert.That(firstBoundary.position.y, Is.EqualTo(0).Within(0.001f));
            // 생존자 전원이 2구간(쉼터 앞 z72)·3구간(쿠션 앞 z143)에 도착하면 1.3배·1.6배(2026-11-03 담당자 결정).
            Assert.That(fires[0].StageCount, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 1.3f, 1.6f }, Field<float[]>(fires[0], "stageSpeedMultipliers"));
            var lines = Field<Transform[]>(fires[0], "stageLines");
            Assert.That(lines[0].position.z, Is.EqualTo(shelter.min.z).Within(0.001f));
            Assert.That(lines[1].position.z, Is.EqualTo(cushion.bounds.min.z).Within(0.001f));
            Assert.That(fires[1].StageCount, Is.EqualTo(0));
            // 2026-11-03: 1구간 불은 빨간 상자 대신 폭 50m 불꽃 파티클 벽으로 보인다. 상자는 판정 크기만 따라간다.
            var effect = Field<Transform>(fires[0], "effect");
            Assert.IsNotNull(effect);
            Assert.IsFalse(effect.gameObject.activeSelf, "켜지기 전에는 숨긴다");
            Assert.That(effect.localScale.x, Is.EqualTo(Field<float>(fires[0], "halfWidth") * 2).Within(0.001f));
            Assert.That(effect.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(3));
            Assert.That(UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(effect.gameObject),
                Does.EndWith("HumanCannon/Prefabs/HumanCannonChaseFire.prefab"));
            Assert.IsFalse(Field<Transform>(fires[0], "visual").GetComponent<Renderer>().enabled, "빨간 상자는 그리지 않는다");
            Assert.That(finals.All(s => Mathf.Abs(s.transform.position.x) < Field<float>(fires[0], "halfWidth")
                && s.transform.position.y - firstBoundary.position.y < Field<float>(fires[0], "height")), Is.True, "최종 대포 위까지 판정한다");
            Assert.That(secondBoundary.position.z, Is.EqualTo(141.5f).Within(0.001f));
            // 2026-11-02: 쿠션 뒤끝만 143으로 당겼다. 불꽃 경계는 그대로라 쿠션 앞 1.5m 일반 바닥까지만 온다.
            Assert.That(cushion.bounds.min.z, Is.EqualTo(secondBoundary.position.z + 1.5f).Within(0.001f));
            Assert.That(secondBoundary.position.y, Is.EqualTo(6).Within(0.001f));
            Assert.That(Field<Transform>(fires[1], "start").position.y, Is.EqualTo(6).Within(0.001f));
            Assert.That(cushion.bounds.center.y, Is.EqualTo(6.1f).Within(0.001f));
            Assert.That(cushion.bounds.max.y, Is.EqualTo(6.2f).Within(0.001f));
            Assert.That(Field<Transform>(fires[1], "start").position.z, Is.EqualTo(shelter.min.z).Within(0.001f));
            Assert.That(fires.All(f => Field<Transform>(f, "start").position.z < cushion.bounds.min.z), Is.True);
        }

        [Test]
        public void FirstFire_ArmsFromDepartureTransportOrAnyOfTheThreeDepartureCannons()
        {
            var route = All<HumanCannonRouteSystem>().Single();
            var stations = Field<HumanCannonStation[]>(All<HumanCannonSystem>().Single(), "stations");
            // 경로 시스템은 transports[0] 출발 시 ArmFire(0)을 호출한다(2026-11-03 담당자 확인).
            Assert.That(Field<HumanCannonTransport[]>(route, "transports")[0].name, Is.EqualTo("DepartureTransport"));
            CollectionAssert.AreEqual(new[] { "Cannon", "DepartureCannon2", "DepartureCannon3" },
                stations.Take(3).Select(s => s.name).ToArray(), "출발 대포 1~3");
            for (int i = 0; i < stations.Length; i++)
                Assert.That(route.FireSectionForStation(i), i < 3 ? Is.EqualTo(0) : Is.Not.EqualTo(0), stations[i].name);
        }

        [Test]
        public void FourPoles_KeepOrderAndBothCooperationGates()
        {
            var systems = All<HumanCannonPoleSystem>();
            Assert.That(systems, Has.Length.EqualTo(1));
            // Task-044 2026-10-22: 휴식 봉5 + 관문 연결 봉2. 2026-11-02 담당자 결정으로 코스 첫 그네(PoleGateCourse/NormalPole),
            // 도착지점 그네(ExtendedCourse044/CeilingPole1_4), 도착지점에 가장 가까운 그네(HumanCannonEnhancement/AddedPole2) 삭제.
            Assert.That(systems[0].PoleCount, Is.EqualTo(4));
            var poles = Field<HumanCannonPole[]>(systems[0], "poles");
            Assert.That(poles, Has.Length.EqualTo(4));
            Assert.That(poles, Has.All.Not.Null);
            Assert.That(poles.Count(p => p.LinkedGate == null), Is.EqualTo(2));
            Assert.That(Find("HumanCannonEnhancement/AddedPole2"), Is.Null);
            Assert.That(Find("HumanCannonEnhancement/AddedPole2Ceiling"), Is.Null);
            CollectionAssert.AllItemsAreUnique(poles);
            CollectionAssert.AreEquivalent(All<HumanCannonPole>(), poles);
            Assert.That(Find("PoleGateCourse/NormalPole"), Is.Null);
            Assert.That(Find("ExtendedCourse044/CeilingPole1_4"), Is.Null);
            Assert.That(Find("ExtendedCourse044/CeilingPole1_4Ceiling"), Is.Null);
            Assert.That(poles[0].transform, Is.SameAs(Find("PoleGateCourse/LinkedPole")));
            Assert.That(poles[1].transform, Is.SameAs(Find("SplitRouteCourse/SecondSectionPoleGate/NormalPole")));
            Assert.That(poles[2].transform, Is.SameAs(Find("SplitRouteCourse/SecondSectionPoleGate/LinkedPole")));
            Assert.That(poles.Skip(3).All(p => p.LinkedGate == null), Is.True);
            // 2026-11-04 사용자 결정: 톱벽을 작동시키는 연결 그네 2개만 남기고 일반 그네는 줄·천장과 함께 숨긴다(삭제 아님).
            foreach (var pole in poles)
                Assert.That(pole.gameObject.activeSelf, Is.EqualTo(pole.LinkedGate != null), pole.name);
            foreach (var ceiling in new[] { "ExtendedCourse044/ExistingCeiling2", "HumanCannonEnhancement/AddedPole0Ceiling" })
                Assert.That(Find(ceiling).gameObject.activeSelf, Is.False, ceiling);
            foreach (var ceiling in new[] { "ExtendedCourse044/ExistingCeiling1", "ExtendedCourse044/ExistingCeiling3" })
                Assert.That(Find(ceiling).gameObject.activeSelf, Is.True, ceiling);
            var gates = poles.Where(p => p.LinkedGate != null).Select(p => p.LinkedGate).Distinct()
                .OrderBy(g => g.transform.position.z).ToArray();
            Assert.That(gates, Has.Length.EqualTo(2));
            CollectionAssert.AreEquivalent(All<HumanCannonSawGate>(), gates);
            Assert.That(gates[0].transform.position.z, Is.EqualTo(47).Within(0.001f));
            Assert.That(gates[0].transform.position.y, Is.EqualTo(0).Within(0.001f));
            Assert.That(gates[1].transform.position.z, Is.EqualTo(123).Within(0.001f));
            Assert.That(gates[1].transform.position.y, Is.EqualTo(6).Within(0.001f));
            Assert.That(gates[0].transform.root.name, Is.EqualTo("PoleGateCourse"));
            foreach (var gate in gates)
            {
                foreach (var half in new[] { "LeftBlades", "RightBlades" })
                {
                    var blades = gate.transform.Find("SawVisual/" + half);
                    Assert.That(blades.Cast<Transform>().Count(t => t.name.StartsWith("GateSawDisc")), Is.EqualTo(22));
                    // 갈라지는 동안 바닥에 끌리는 스파크(바닥 바로 위, 방출만 꺼진 상태로 대기).
                    var sparks = blades.Find("DragSparks")?.GetComponent<ParticleSystem>();
                    Assert.That(sparks, Is.Not.Null, half + " 끌림 스파크");
                    Assert.That(sparks.main.playOnAwake, Is.False);
                    Assert.That(sparks.transform.position.y, Is.EqualTo(gate.transform.position.y + 0.05f).Within(.01f));
                }
                Assert.That(new UnityEditor.SerializedObject(gate).FindProperty("dragSparks").arraySize, Is.EqualTo(2));
                Assert.That(gate.transform.Find("SawVisual/LeftBlades/GateSawDisc0/Disc"), Is.Not.Null);
                var barrier = gate.transform.Find("Barrier");
                Assert.That(barrier.GetComponent<Renderer>().enabled, Is.False, "기존 판정 상자는 숨긴다.");
                Assert.That(barrier.GetComponent<Collider>(), Is.Null, "충돌은 좌우 절반 벽이 나눠 맡는다.");
                var config = new UnityEditor.SerializedObject(gate);
                Assert.That(config.FindProperty("leftParts").arraySize, Is.EqualTo(2));
                Assert.That(config.FindProperty("rightParts").arraySize, Is.EqualTo(2));
                // 열린 틈 = 트램펄린 폭 + 좌우 1m.
                float trampoline = All<HumanCannonTrampoline>().Single().GetComponentsInChildren<Renderer>(true)
                    .Max(r => Mathf.Max(r.bounds.size.x, r.bounds.size.z));
                Assert.That(gate.OpenGap, Is.EqualTo(trampoline + 2f).Within(.01f));
                var fence = barrier.GetComponentsInChildren<Renderer>(true).Where(r => r.transform != barrier).ToArray();
                Assert.That(fence, Is.Not.Empty, "적용된 철망 외형은 유지한다.");
                Assert.That(fence.All(r => r.enabled), Is.True, "Active 관문은 철망 외형을 표시한다.");
            }
        }

        [Test]
        public void EveryLiftHasOpposingDoorsAndNoSolidFloorImmediatelyUnderneath()
        {
            foreach (var transport in All<HumanCannonTransport>())
            {
                Assert.That(transport.HasTrapDoors, Is.True, transport.name);
                var left = Field<Transform>(transport, "leftHinge");
                var right = Field<Transform>(transport, "rightHinge");
                Assert.That(left.localPosition.x, Is.EqualTo(-right.localPosition.x));
                Assert.That(left.localPosition.x, Is.LessThan(0));
                var doors = Field<Collider[]>(transport, "trapColliders");
                Assert.That(doors, Has.Length.EqualTo(2));
                foreach (float progress in new[] { 0f, 1f })
                {
                    var below = transport.Position(progress) - Vector3.up * 0.5f;
                    var blockers = All<Collider>().Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger)
                        .Where(c => !c.transform.IsChildOf(transport.Platform) && c.bounds.Contains(below)).ToArray();
                    Assert.That(blockers, Is.Empty, transport.name + " trap needs a clear drop at both endpoints");
                }
            }
        }

        [Test]
        public void EveryLift_RefusesOverloadWithoutOpeningFloor()
        {
            // 2026-11-02 사용자 결정: 왼쪽 리프트는 2명 이상이면 바닥을 열지 않고 출발만 막는다(빨간 발판).
            // 2026-11-04 사용자 결정: 쉼터 승강기·쉼터 출발 장치도 같다.
            foreach (string name in new[] { "DepartureTransport", "ShelterLift", "ShelterDeparture" })
                Assert.IsFalse(All<HumanCannonTransport>().Single(t => t.name == name).OverloadTrap, name);
        }

        [Test]
        public void FirstDepartureHasNoLowerRecoveryCourse()
        {
            // 2026-10-30 담당자 결정: 시작 지점 아래 안전 바닥과 복귀 계단을 삭제하고 낙하는 코스 아래 추락 처리에 맡긴다(2026-11-03부터 외줄타기와 같은 공용 사망).
            var lift = All<HumanCannonTransport>().Single(t => t.name == "DepartureTransport");
            Assert.That(lift.StartPosition.y, Is.EqualTo(4));
            Assert.That(Find("SplitRouteCourse/LiftSafetyLesson"), Is.Null);
            float fallHeight = Field<float>(All<HumanCannonRouteSystem>().Single(), "fallHeight");
            var start = FloorBounds(Find("Ground"));
            var below = All<Collider>().Where(c => c.enabled && !c.isTrigger && c.bounds.size.sqrMagnitude > 0
                && c.bounds.max.y < start.min.y && c.bounds.min.y > fallHeight
                && c.bounds.max.z <= start.max.z && c.bounds.min.z >= start.min.z - 2f).Select(c => c.name).ToArray();
            Assert.That(below, Is.Empty, "시작 바닥 아래에 남은 바닥이나 계단이 없어야 합니다.");
        }

        [Test]
        public void SplitShelterLandingFillsOldLiftHoleButKeepsDepartureHole()
        {
            var zone = Find("SplitRouteCourse/ShelterDeck").GetComponent<HumanCannonLandingZone>();
            Assert.That(zone.SupportsLanding(new Vector3(0, 6.05f, 78)), Is.True);
            Assert.That(zone.SupportsLanding(new Vector3(-9, 6.05f, 81)), Is.False);
            Assert.That(zone.SupportsLanding(new Vector3(-9, 6.05f, 72.5f)), Is.True);
            var firstFloor = Find("SplitRouteCourse/FirstCourseFloor").GetComponentsInChildren<Collider>()
                .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger).ToArray();
            foreach (float z in new[] { 35.1f, 39f, 41.9f })
                Assert.That(firstFloor.Any(c => c.bounds.Contains(new Vector3(-9, -0.05f, z))), Is.True, "옛 하부 리프트 구멍 복원");
        }

        [Test]
        public void ShelterAccess_ConnectsFirstCourseToLowerLiftAndUpperLiftToDeck()
        {
            var lift = All<HumanCannonTransport>().Single(t => t.name == "ShelterLift");
            var access = FloorBounds(Find("SplitRouteCourse/ShelterLiftAccess"));
            var firstFloor = FloorBounds(Find("SplitRouteCourse/FirstCourseFloor"));
            var deck = FloorBounds(Find("SplitRouteCourse/ShelterDeck"));
            Assert.That(access.min.x, Is.EqualTo(-35).Within(0.001f));
            Assert.That(access.max.x, Is.EqualTo(-22).Within(0.001f));
            Assert.That(access.min.z, Is.EqualTo(63).Within(0.001f));
            Assert.That(access.max.z, Is.EqualTo(66).Within(0.001f));
            Assert.That(access.max.y, Is.EqualTo(0).Within(0.001f));
            Assert.That(access.max.x, Is.EqualTo(firstFloor.min.x).Within(0.001f));
            Assert.That(access.max.y, Is.EqualTo(firstFloor.max.y).Within(0.001f));
            Assert.That(access.max.z, Is.EqualTo(lift.StartPosition.z - lift.HalfSize.y).Within(0.001f));
            Assert.That(lift.StartPosition.x - lift.HalfSize.x, Is.GreaterThanOrEqualTo(access.min.x - 0.001f));
            Assert.That(lift.StartPosition.x + lift.HalfSize.x, Is.LessThanOrEqualTo(access.max.x + 0.001f));
            Assert.That(lift.Position(1).x + lift.HalfSize.x, Is.EqualTo(deck.min.x).Within(0.001f));
            Assert.That(lift.Position(1).y, Is.EqualTo(deck.max.y).Within(0.001f));
            Assert.That(lift.Position(1).z - lift.HalfSize.y, Is.GreaterThanOrEqualTo(deck.min.z));
            Assert.That(lift.Position(1).z + lift.HalfSize.y, Is.LessThanOrEqualTo(deck.max.z));
        }

        [Test]
        public void ShelterAccess_HasBodyClearanceWithSideFencesRemoved()
        {
            var solids = All<Collider>().Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger).ToArray();
            var passage = new Bounds(new Vector3(-22, 0.8f, 64.5f), new Vector3(1, 1.58f, 2.98f));
            var blockers = solids.Where(c => c.bounds.Intersects(passage)).Select(c => c.name).ToArray();
            Assert.That(blockers, Is.Empty, "z63..66 출입구는 캐릭터 높이까지 막힘이 없어야 합니다.");
            Assert.That(solids.Any(c => c.bounds.Contains(new Vector3(-22, 1, 46))), Is.False, "구간 중간 측벽 제거");
            Assert.That(solids.Any(c => c.bounds.Contains(new Vector3(-22, 1, 68))), Is.False, "출입구 뒤 측벽 제거");
            var firstGate = All<HumanCannonSawGate>().OrderBy(g => g.transform.position.z).First();
            Assert.That(FloorBounds(Find("SplitRouteCourse/ShelterLiftAccess")).min.z,
                Is.GreaterThan(firstGate.transform.position.z + GameSettings.Base.Character.CapsuleRadius));
        }

        [Test]
        public void ShelterDirectJump_CannotReachLowerLiftOrAccessEvenWithCapsuleAndSkinAllowance()
        {
            var lift = All<HumanCannonTransport>().Single(t => t.name == "ShelterLift");
            var deck = FloorBounds(Find("SplitRouteCourse/ShelterDeck"));
            var access = FloorBounds(Find("SplitRouteCourse/ShelterLiftAccess"));
            var character = GameSettings.Base.Character;
            var playerPrefab = Resources.Load<GameObject>("Prefabs/Characters/Players/Player");
            Assert.That(playerPrefab, Is.Not.Null);
            var body = playerPrefab.GetComponent<CharacterController>();
            Assert.That(body, Is.Not.Null);
            float drop = deck.max.y - lift.StartPosition.y;
            float verticalSpeed = Mathf.Sqrt(2f * character.Gravity * character.JumpHeight);
            float time = (verticalSpeed + Mathf.Sqrt(verticalSpeed * verticalSpeed + 2f * character.Gravity * drop)) / character.Gravity;
            float reach = character.FreeMoveSpeed * time + 2f * (character.CapsuleRadius + body.skinWidth);
            float liftGap = deck.min.x - (lift.StartPosition.x + lift.HalfSize.x);
            float accessGap = deck.min.z - access.max.z;
            Assert.That(drop, Is.EqualTo(6).Within(0.001f));
            Assert.That(liftGap, Is.EqualTo(7).Within(0.001f));
            Assert.That(accessGap, Is.EqualTo(6).Within(0.001f));
            Assert.That(reach, Is.LessThan(liftGap), "하부 리프트까지 직접 일반 점프 불가");
            Assert.That(reach, Is.LessThan(accessGap), "접근 통로까지 직접 일반 점프 불가");
        }

        [Test]
        public void ShelterSideGap_HasNoFixedSupportBetweenDeckAndLowerLift()
        {
            // 기존 측벽의 두께를 제외한 옆 틈. 접근 통로와 첫 코스 바닥도 이 안으로 뻗지 않아야 한다.
            var gap = new Bounds(new Vector3(-25.6f, -2, 69), new Vector3(6.6f, 15.8f, 5.8f));
            var blockers = All<Collider>().Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger)
                .Where(c => c.GetComponentInParent<HumanCannonTransport>() == null && c.bounds.Intersects(gap))
                .Select(c => c.name).ToArray();
            Assert.That(blockers, Is.Empty, "쉼터 왼쪽의 하부 리프트 사이 틈은 지면 없는 낙하 공간이어야 합니다.");
        }

        static Bounds FloorBounds(Transform root)
        {
            var solids = root.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
            Assert.That(solids, Is.Not.Empty);
            var bounds = solids[0].bounds;
            foreach (var solid in solids.Skip(1)) bounds.Encapsulate(solid.bounds);
            return bounds;
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
