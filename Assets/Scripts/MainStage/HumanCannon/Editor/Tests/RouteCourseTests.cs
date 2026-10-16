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
                    && c.bounds.min.x < 15f && c.bounds.max.x > -15f
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
            Assert.That(Vector3.Distance(transports[1].StartPosition, new Vector3(-25, 0, 70)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(transports[1].Position(1), new Vector3(-18, 6, 78)), Is.LessThan(0.001f));
            Assert.That(transports[2].StartPosition.y, Is.EqualTo(6).Within(0.001f));
            Assert.That(transports[2].Position(1).y, Is.EqualTo(6).Within(0.001f));
        }

        [Test]
        public void ExactlyTwoFireSections_StopAtShelterAndCushionWithoutAnotherFinalPressure()
        {
            var route = All<HumanCannonRouteSystem>().Single();
            var fires = Field<HumanCannonFireWall[]>(route, "fires");
            Assert.That(fires, Has.Length.EqualTo(2));
            Assert.That(fires, Has.All.Not.Null);
            CollectionAssert.AllItemsAreUnique(fires);
            CollectionAssert.AreEquivalent(All<HumanCannonFireWall>(), fires);
            Assert.That(fires.All(f => f.IsConfigured), Is.True);
            var firstBoundary = Field<Transform>(fires[0], "boundary");
            var secondBoundary = Field<Transform>(fires[1], "boundary");
            var shelter = FloorBounds(Find("SplitRouteCourse/ShelterDeck"));
            var cushion = Find("LandingCushion").GetComponent<Collider>();
            Assert.That(firstBoundary.position.z, Is.EqualTo(72).Within(0.001f));
            Assert.That(firstBoundary.position.z, Is.EqualTo(shelter.min.z).Within(0.001f));
            Assert.That(secondBoundary.position.z, Is.EqualTo(141.5f).Within(0.001f));
            Assert.That(secondBoundary.position.z, Is.EqualTo(cushion.bounds.min.z).Within(0.001f));
            Assert.That(secondBoundary.position.y, Is.EqualTo(6).Within(0.001f));
            Assert.That(Field<Transform>(fires[1], "start").position.y, Is.EqualTo(6).Within(0.001f));
            Assert.That(cushion.bounds.center.y, Is.EqualTo(6.1f).Within(0.001f));
            Assert.That(cushion.bounds.max.y, Is.EqualTo(6.2f).Within(0.001f));
            Assert.That(Field<Transform>(fires[1], "start").position.z, Is.EqualTo(firstBoundary.position.z).Within(0.001f));
            Assert.That(fires.All(f => Field<Transform>(f, "start").position.z < cushion.bounds.min.z), Is.True);
        }

        [Test]
        public void SixPoles_KeepOriginalIndicesAndBothCooperationGates()
        {
            var systems = All<HumanCannonPoleSystem>();
            Assert.That(systems, Has.Length.EqualTo(1));
            Assert.That(systems[0].PoleCount, Is.EqualTo(6));
            var poles = Field<HumanCannonPole[]>(systems[0], "poles");
            Assert.That(poles, Has.Length.EqualTo(6));
            Assert.That(poles, Has.All.Not.Null);
            CollectionAssert.AllItemsAreUnique(poles);
            CollectionAssert.AreEquivalent(All<HumanCannonPole>(), poles);
            Assert.That(poles[0].transform, Is.SameAs(Find("PoleGateCourse/NormalPole")));
            Assert.That(poles[1].transform, Is.SameAs(Find("PoleGateCourse/LinkedPole")));
            Assert.That(poles[2].transform, Is.SameAs(Find("SplitRouteCourse/SecondSectionPoleGate/NormalPole")));
            Assert.That(poles[3].transform, Is.SameAs(Find("SplitRouteCourse/SecondSectionPoleGate/LinkedPole")));
            Assert.That(poles.Skip(4).All(p => p.LinkedGate == null), Is.True);
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
                Assert.That(gate.transform.Find("SawVisual").childCount, Is.EqualTo(13));
                Assert.That(gate.transform.Find("SawVisual/GateSawDisc0/Disc"), Is.Not.Null);
                Assert.That(gate.transform.Find("Barrier").GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), Is.True);
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

        [TestCase(0.75f)]
        [TestCase(0.8f)]
        [TestCase(0.9f)]
        public void FirstLiftOverloadDuringTravel_HasClearDropUnderWholePlatform(float progress)
        {
            var lift = All<HumanCannonTransport>().Single(t => t.name == "DepartureTransport");
            var recovery = Find("SplitRouteCourse/LiftSafetyLesson/RecoveryFloor").GetComponent<BoxCollider>();
            Vector3 position = lift.Position(progress);
            var footprint = new Bounds(position, Vector3.zero);
            foreach (float x in new[] { -lift.HalfSize.x, lift.HalfSize.x })
                foreach (float z in new[] { -lift.HalfSize.y, lift.HalfSize.y })
                    footprint.Encapsulate(position + lift.Rotation * new Vector3(x, 0, z));
            // 경계에 맞닿은 옆 바닥은 제외하고, 발판 내부에서 안전 바닥 직전까지의 낙하 공간을 검사한다.
            const float inset = 0.001f;
            var drop = new Bounds();
            drop.SetMinMax(new Vector3(footprint.min.x + inset, recovery.bounds.max.y + inset, footprint.min.z + inset),
                new Vector3(footprint.max.x - inset, position.y - inset, footprint.max.z - inset));
            Assert.That(drop.size.y, Is.GreaterThan(0));
            var blockers = All<Collider>()
                .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c != recovery)
                .Where(c => c.GetComponentInParent<HumanCannonTransport>() == null && c.bounds.Intersects(drop))
                .Select(c => c.transform.parent != null ? c.transform.parent.name + "/" + c.name : c.name).ToArray();
            Assert.That(blockers, Is.Empty, "첫 리프트 이동 중 과승자는 고정 바닥을 거치지 않고 하부 안전 바닥까지 떨어져야 합니다.");
        }

        [Test]
        public void FirstDepartureHasSafeLowerFloorAndConnectedSmallStepReturn()
        {
            var lift = All<HumanCannonTransport>().Single(t => t.name == "DepartureTransport");
            var recovery = Find("SplitRouteCourse/LiftSafetyLesson/RecoveryFloor").GetComponent<BoxCollider>();
            Assert.That(lift.StartPosition.y, Is.EqualTo(4));
            Assert.That(recovery.bounds.max.y, Is.EqualTo(-4));
            foreach (float progress in new[] { 0f, 0.5f, 1f })
            {
                var point = lift.Position(progress); point.y = recovery.bounds.center.y;
                Assert.That(recovery.bounds.Contains(point), Is.True, "첫 이동 경로 전체 아래 안전 바닥");
            }
            var stairs = Find("SplitRouteCourse/LiftSafetyLesson/ReturnStairs").GetComponentsInChildren<BoxCollider>()
                .OrderBy(c => c.bounds.max.y).ToArray();
            Assert.That(stairs, Has.Length.EqualTo(40));
            float previousHeight = -4;
            foreach (var stair in stairs)
            {
                Assert.That(stair.bounds.max.y - previousHeight, Is.EqualTo(0.2f).Within(0.001f));
                previousHeight = stair.bounds.max.y;
                Assert.That(stair.GetComponent<HumanCannonLandingZone>(), Is.Not.Null);
            }
            Assert.That(previousHeight, Is.EqualTo(FloorBounds(Find("Ground")).max.y).Within(0.001f));
            Assert.That(recovery.GetComponent<HumanCannonLandingZone>().SupportsLanding(new Vector3(-9, -3.95f, 3)), Is.True);
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
            Assert.That(access.min.x, Is.EqualTo(-28).Within(0.001f));
            Assert.That(access.max.x, Is.EqualTo(-15).Within(0.001f));
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
        public void ShelterAccess_HasBodyClearanceThroughFenceOnlyAfterFirstGate()
        {
            var solids = All<Collider>().Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger).ToArray();
            var passage = new Bounds(new Vector3(-15, 0.8f, 64.5f), new Vector3(1, 1.58f, 2.98f));
            var blockers = solids.Where(c => c.bounds.Intersects(passage)).Select(c => c.name).ToArray();
            Assert.That(blockers, Is.Empty, "z63..66 출입구는 캐릭터 높이까지 막힘이 없어야 합니다.");
            Assert.That(solids.Any(c => c.bounds.Contains(new Vector3(-15, 1, 46))), Is.True, "구간 중간 관문 바로 전 측벽 보존");
            Assert.That(solids.Any(c => c.bounds.Contains(new Vector3(-15, 1, 68))), Is.True, "출입구 뒤 측벽 보존");
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
            var gap = new Bounds(new Vector3(-18.6f, -2, 69), new Vector3(6.6f, 15.8f, 5.8f));
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
