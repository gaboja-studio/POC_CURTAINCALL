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
                Assert.That(Vector3.Distance(station.transform.position, new Vector3(-6 + 4 * i, 6.2f, 152)),
                    Is.LessThan(0.001f), station.name);
                Assert.That(Quaternion.Angle(station.transform.rotation, Quaternion.identity), Is.LessThan(0.001f), station.name);
                Assert.That(station.Muzzle.position.y, Is.EqualTo(stations[0].Muzzle.position.y).Within(0.001f), station.name);
                Assert.That(station.Muzzle.position.z, Is.EqualTo(stations[0].Muzzle.position.z).Within(0.001f), station.name);
                Assert.That(Vector3.Angle(station.Muzzle.forward, Vector3.forward), Is.LessThan(0.001f), station.name);
                Assert.That(Vector3.Distance(station.LaunchVelocity, new Vector3(0, 12, 8)), Is.LessThan(0.001f), station.name);
                Assert.That(station.AirControlSpeed, Is.EqualTo(3f).Within(0.001f), station.name);
                Assert.That(station.BoardingDistance, Is.EqualTo(2f).Within(0.001f), station.name);
            }
        }

        [Test]
        public void CushionAndPreparationDeck_JoinAtSameSafeHeightAndSupportEveryBoardingPoint()
        {
            var cushion = SafeSurface("LandingCushion");
            var deck = SafeSurface("FinalCannonCourse/PreparationDeck");
            Assert.That(cushion.bounds.max.y, Is.EqualTo(6.2f).Within(0.001f));
            Assert.That(deck.bounds.max.y, Is.EqualTo(cushion.bounds.max.y).Within(0.001f));
            Assert.That(cushion.bounds.max.z, Is.EqualTo(146.5f).Within(0.001f));
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
                Assert.That(deckZone.SupportsLanding(station.BoardingPoint.position), Is.True, station.name + " boarding needs safe footing");
        }

        [Test]
        public void FourUnsteeredFinalFlights_DescendInsideSafeLandingPad()
        {
            var pad = SafeSurface("FinalCannonCourse/FlightLandingPad");
            var zone = pad.GetComponent<HumanCannonLandingZone>();
            Assert.That(pad.bounds.size.x, Is.EqualTo(20f).Within(0.001f));
            Assert.That(pad.bounds.size.z, Is.EqualTo(10f).Within(0.001f));
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
                Assert.That(impact.z, Is.InRange(pad.bounds.min.z + bodyRadius, pad.bounds.max.z - bodyRadius), station.name);
                Assert.That(zone.SupportsLanding(impact), Is.True, station.name + " must finish as a safe landing");
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
        public void DoubledFinalSaws_RegisterFourSpacedHorizontalTracksAndTwoWarnedVerticalViews()
        {
            var saws = All<HumanCannonFinalSaws>().Single();
            Assert.That(saws.HorizontalCount, Is.EqualTo(4));
            Assert.That(saws.VerticalCount, Is.EqualTo(2));
            var origins = Field<Transform[]>(saws, "horizontalOrigins");
            var views = Field<Transform[]>(saws, "horizontalViews");
            Assert.That(views, Has.Length.EqualTo(4));
            CollectionAssert.AllItemsAreUnique(origins.Select(origin => origin.GetInstanceID()).ToArray());
            CollectionAssert.AllItemsAreUnique(views.Select(view => view.GetInstanceID()).ToArray());
            Assert.That(Field<Transform>(saws, "secondaryWarningView"), Is.Not.Null);
            Assert.That(Field<float>(saws, "horizontalRadius"), Is.EqualTo(1));
            Assert.That(Field<float>(saws, "verticalRadius"), Is.EqualTo(1.2f));
            float[] distances = { 7, 13, 10, 16 };
            var station = FinalStations()[0];
            for (int i = 0; i < origins.Length; i++)
            {
                float time = distances[i] / station.LaunchVelocity.z;
                float expectedY = station.Muzzle.position.y + station.LaunchVelocity.y * time
                    - 0.5f * GameSettings.Base.Character.Gravity * time * time + 0.45f;
                Assert.That(origins[i].position.z - station.Muzzle.position.z, Is.EqualTo(distances[i]).Within(0.001f));
                Assert.That(origins[i].position.y, Is.EqualTo(expectedY).Within(0.001f));
            }
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
