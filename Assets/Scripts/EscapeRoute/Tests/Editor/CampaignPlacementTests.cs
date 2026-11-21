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
        [TestCase(false)] [TestCase(true)]
        [Category("EscapeShaftFadeRegression")]
        public void ShaftDarknessFollowsPlacementWithoutChangingSharedMaterialsOrTint(bool inactive)
        {
            var material = new Material(Shader.Find("CurtainCall/Escape/FactoryConcrete"));
            var initial = new Vector4(-89, -45, 1, 0); material.SetVector("_ShaftFade", initial);
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube); surface.transform.SetParent(root.transform, false);
            var view = surface.GetComponent<Renderer>(); view.sharedMaterial = material;
            surface.SetActive(!inactive);
            var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", Color.red); view.SetPropertyBlock(block);
            try
            {
                foreach (float y in new[] { -16f, 25f, 67f, 25f })
                {
                    Place(new Vector3(-200, y, 118.3f), new Vector3(0, 180, 0));
                    view.GetPropertyBlock(block);
                    Assert.That(block.GetVector("_ShaftFade"), Is.EqualTo(new Vector4(y - 56, y - 33, 1, 0)));
                    Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.red));
                    Assert.That(material.GetVector("_ShaftFade"), Is.EqualTo(initial), "다른 씬/프리팹의 공유 재질을 런타임에 변경하지 않는다.");
                }
                root.transform.position += Vector3.up * 10;
                content.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(content, null);
                view.GetPropertyBlock(block);
                Assert.That(block.GetVector("_ShaftFade"), Is.EqualTo(new Vector4(-21, 2, 1, 0)), "직접 루트 이동도 다음 갱신에 반영");
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test, Category("EscapeShaftFadeRegression")]
        public void ShaftDarknessReadsLiveGameSettingsAndSupportsDisablingTheEffect()
        {
            var settings = TypeOf("CurtainCall.Settings.GameSettings").GetProperty("EscapeRoute").GetValue(null);
            var tuning = settings.GetType().GetProperty("ShaftFade").GetValue(settings);
            var strength = tuning.GetType().GetField("strength", BindingFlags.Instance | BindingFlags.NonPublic);
            var oldStrength = strength.GetValue(tuning);
            var material = new Material(Shader.Find("CurtainCall/Escape/FactoryConcrete"));
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube); surface.transform.SetParent(root.transform, false);
            var view = surface.GetComponent<Renderer>(); view.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            try
            {
                Place(new Vector3(0, 25, 0), Vector3.zero);
                foreach (float value in new[] { .6f, 0f, 1f })
                {
                    strength.SetValue(tuning, value);
                    content.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(content, null);
                    view.GetPropertyBlock(block);
                    Assert.That(block.GetVector("_ShaftFade").z, Is.EqualTo(value));
                }
            }
            finally { strength.SetValue(tuning, oldStrength); Object.DestroyImmediate(material); }
        }

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
                var voidType = TypeOf("CurtainCall.EscapeRoute.EscapeFallVoid");
                var voids = facility.GetComponentsInChildren(voidType, true).Cast<Behaviour>()
                    .Where(v => v.enabled && v.gameObject.activeInHierarchy).ToArray();
                var contains = voidType.GetMethod("Contains");
                bool Lethal(Vector3 point) => voids.Any(v => (bool)contains.Invoke(v, new object[] { point }));
                foreach (var shaft in new[] { new Vector3(8, -31, 17), new Vector3(46, -31, -74),
                    new Vector3(180, -31, 0), new Vector3(345, -31, 60) })
                {
                    Vector3 feet = facility.transform.TransformPoint(shaft);
                    Assert.That(feet.y, Is.GreaterThan(deathY), "This depth is above the common world death plane.");
                    Assert.That(Lethal(feet), Is.True, $"An escape shaft can strand a survivor at {shaft}.");
                }
                var stranded = voidType.GetMethod("ContainsStrandedLanding", BindingFlags.Instance | BindingFlags.NonPublic);
                bool Stranded(Vector3 point) => voids.Any(v => (bool)stranded.Invoke(v, new object[] { point }));
                var foundation = facility.GetComponentsInChildren<BoxCollider>(false).Single(c => c.name == "Processing bay foundation");
                var foundationTop = foundation.transform.TransformPoint(foundation.center + Vector3.up * foundation.size.y * .5f);
                Assert.That(Lethal(foundationTop), Is.False, "The higher boundary must allow a swing above the foundation.");
                Assert.That(Stranded(foundationTop), Is.True, "Landing on the concrete foundation cannot strand the player.");
                foreach (var station in stations)
                {
                    var board = (Transform)station.GetType().GetProperty("BoardingPoint").GetValue(station);
                    Assert.That(Lethal(board.position) || Stranded(board.position), Is.False, station.name);
                }
                var landingType = TypeOf("CurtainCall.HumanCannon.HumanCannonLandingZone");
                foreach (var landing in facility.GetComponentsInChildren(landingType, false).Cast<Component>())
                    foreach (var tile in landing.GetComponents<BoxCollider>().Concat(
                        (BoxCollider[])landingType.GetField("additionalSurfaces", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(landing)))
                    {
                        if (!tile || !tile.enabled || tile.isTrigger) continue;
                        var top = tile.transform.TransformPoint(tile.center + Vector3.up * tile.size.y * .5f);
                        Assert.That(Lethal(top) || Stranded(top), Is.False, tile.name);
                    }
                Assert.That(Lethal(facility.transform.TransformPoint(new Vector3(-40, -35, 17))), Is.False,
                    "The route volume must not reach the neighboring facility.");
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
