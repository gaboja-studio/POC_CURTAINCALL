using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class CampaignPlacementTests
    {
        GameObject root;
        Component content, cue;
        readonly Vector3 origin = new(50, -17.4f, -78);
        readonly Vector3 destination = new(50, -17.4f, -70);
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        static void Set(Component target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        static Vector3 Point(Component target, string field) => (Vector3)target.GetType().GetField(field).GetValue(target);
        void Place(Vector3 position, Vector3 euler)
        {
            Set(content, "campaignPosition", position); Set(content, "campaignEuler", euler);
            content.GetType().GetMethod("PlaceCampaign").Invoke(content, null);
        }
        void AssertPaths()
        {
            Assert.That(Vector3.Distance(Point(cue, "origin"), root.transform.TransformPoint(origin)), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(Point(cue, "destination"), root.transform.TransformPoint(destination)), Is.LessThan(.001f));
        }
        [SetUp] public void SetUp()
        {
            root = new GameObject("Placement regression");
            content = root.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapeCampaignContent"));
            var hazard = new GameObject("Authored cue"); hazard.transform.SetParent(root.transform, false);
            hazard.transform.localPosition = origin;
            cue = hazard.AddComponent(TypeOf("CurtainCall.HumanCannon.HumanCannonHazardCue"));
            Set(cue, "origin", origin); Set(cue, "destination", destination);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void SavedEscapePlacementUsesTheSamePlayableHeightInDirectAndCampaignEntry()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Levels/escape_route.unity");
            try
            {
                var facility = scene.GetRootGameObjects().Single();
                var campaign = facility.GetComponent(TypeOf("CurtainCall.EscapeRoute.EscapeCampaignContent"));
                var baseSettings = TypeOf("CurtainCall.Settings.GameSettings").GetProperty("Base").GetValue(null);
                var damage = baseSettings.GetType().GetProperty("BodyDamage").GetValue(baseSettings);
                float deathY = (float)damage.GetType().GetProperty("WorldDeathY").GetValue(damage);
                var savedPosition = facility.transform.position;
                campaign.GetType().GetMethod("PlaceCampaign").Invoke(campaign, null);
                Assert.That(facility.transform.position, Is.EqualTo(savedPosition), "Campaign entry must not undo the saved height.");
                var stations = facility.GetComponentsInChildren(TypeOf("CurtainCall.HumanCannon.HumanCannonStation"), true);
                Assert.That(stations.Length, Is.GreaterThan(0));
                foreach (var station in stations)
                {
                    var board = (Transform)station.GetType().GetProperty("BoardingPoint").GetValue(station);
                    Assert.That(board.position.y, Is.GreaterThan(deathY), station.name);
                }
                var legacyVoids = facility.GetComponentsInChildren(TypeOf("CurtainCall.EscapeRoute.EscapeFallVoid"), true);
                Assert.That(legacyVoids.Cast<Behaviour>().Any(v => v.enabled), Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test] public void AlreadyPlacedSavedFrameRepairsInactiveLegacyPathsOnlyOnce()
        {
            var position = new Vector3(-100, -16, 118.3f);
            var euler = new Vector3(0, 180, 0);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            cue.gameObject.SetActive(false);
            Place(position, euler); AssertPaths();
            var once = Point(cue, "origin");
            Place(position, euler); AssertPaths();
            Assert.That(Point(cue, "origin"), Is.EqualTo(once));
        }

        [Test] public void RelocationAndRoundTripKeepCuesAndWorldLinesWithTheFacility()
        {
            var line = cue.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.positionCount = 2;
            line.SetPositions(new[] { origin, destination });
            foreach (var pose in new[] {
                (new Vector3(-100, -16, 118.3f), new Vector3(0, 180, 0)),
                (new Vector3(320, 12, -55), new Vector3(0, 90, 0)),
                (Vector3.zero, Vector3.zero) })
            {
                Place(pose.Item1, pose.Item2); AssertPaths();
                Assert.That(Vector3.Distance(line.GetPosition(0), Point(cue, "origin")), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(line.GetPosition(1), Point(cue, "destination")), Is.LessThan(.001f));
            }
        }
    }
}
