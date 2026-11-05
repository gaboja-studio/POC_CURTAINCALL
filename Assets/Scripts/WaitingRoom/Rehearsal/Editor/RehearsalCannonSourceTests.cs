using System.Linq;
using CurtainCall.HumanCannon;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.EditorTools.Tests
{
    public sealed class RehearsalCannonSourceTests
    {
        Scene source;
        RehearsalCannonCourse copy;
        [SetUp] public void OpenSource()
        {
            source = EditorSceneManager.OpenPreviewScene(RehearsalCannonSourceBuilder.ScenePath);
            copy = AssetDatabase.LoadAssetAtPath<GameObject>(RehearsalCannonSourceBuilder.PrefabPath).GetComponent<RehearsalCannonCourse>();
        }
        [TearDown] public void CloseSource() { if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source); }
        T Original<T>() where T : Component => source.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).Single();
        static Object Reference(Object owner, string name, int index = -1)
        {
            var property = new SerializedObject(owner).FindProperty(name);
            return (index < 0 ? property : property.GetArrayElementAtIndex(index)).objectReferenceValue;
        }
        static float Number(Object owner, string name) => new SerializedObject(owner).FindProperty(name).floatValue;
        static string Path(Transform root, Transform child) => AnimationUtility.CalculateTransformPath(child, root);
        static void SameArt(Transform original, Transform clone)
        {
            var expected = original.GetComponentsInChildren<Transform>(true);
            var actual = clone.GetComponentsInChildren<Transform>(true);
            CollectionAssert.AreEqual(expected.Select(t => Path(original, t)), actual.Select(t => Path(clone, t)), "Complete original hierarchy, including disabled legacy pieces, must be copied.");
            for (int i = 1; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].localPosition, actual[i].localPosition, expected[i].name);
                Assert.AreEqual(expected[i].localRotation, actual[i].localRotation, expected[i].name);
                Assert.AreEqual(expected[i].localScale, actual[i].localScale, expected[i].name);
                Assert.AreEqual(expected[i].gameObject.activeSelf, actual[i].gameObject.activeSelf);
            }
            var renderers = original.GetComponentsInChildren<Renderer>(true);
            var mirrors = clone.GetComponentsInChildren<Renderer>(true);
            Assert.AreEqual(renderers.Length, mirrors.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                Assert.AreEqual(renderers[i].GetType(), mirrors[i].GetType());
                Assert.AreEqual(renderers[i].enabled, mirrors[i].enabled);
                CollectionAssert.AreEqual(renderers[i].sharedMaterials, mirrors[i].sharedMaterials, renderers[i].name);
            }
            CollectionAssert.AreEqual(original.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh),
                clone.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh));
        }

        [Test]
        public void W6_CannonSource_PreservesActualSceneDeviceSettingsAndArtOverrides()
        {
            var system = Original<HumanCannonSystem>();
            var station = (HumanCannonStation)Reference(system, "stations", 0);
            Assert.IsFalse(system.PracticeMode, "The normal Start-connected performance keeps injury/flight defaults.");
            Assert.AreEqual(system.UseThirdPersonView, copy.Cannon.UseThirdPersonView);
            Assert.AreSame(system.AirborneView, copy.Cannon.AirborneView);
            foreach (var clone in copy.Stations)
            {
                foreach (string key in new[] { "boardingDistance", "forwardSpeed", "upwardSpeed", "airControlSpeed" })
                    Assert.AreEqual(Number(station, key), Number(clone, key), key);
                SameArt(station.transform, clone.transform);
                Assert.AreEqual(station.transform.InverseTransformPoint(station.Muzzle.position), clone.transform.InverseTransformPoint(clone.Muzzle.position));
                Assert.AreEqual(station.transform.InverseTransformPoint(station.BoardingPoint.position), clone.transform.InverseTransformPoint(clone.BoardingPoint.position));
            }
            var trampoline = Original<HumanCannonTrampoline>();
            SameArt(trampoline.transform, copy.Trampoline.transform);
            Assert.AreEqual(trampoline.HalfSize, copy.Trampoline.HalfSize);
            Assert.AreEqual(trampoline.LoadHalfSize, copy.Trampoline.LoadHalfSize);
            Assert.AreEqual(trampoline.CircularSurface, copy.Trampoline.CircularSurface);
            Assert.AreEqual(trampoline.RequireCarrierForBounce, copy.Trampoline.RequireCarrierForBounce);
            Assert.AreEqual(trampoline.UpwardSpeed, copy.Trampoline.UpwardSpeed);
            Assert.AreEqual(trampoline.CarryMoveSpeed, copy.Trampoline.CarryMoveSpeed);
            var route = Original<HumanCannonRouteSystem>();
            var transport = (HumanCannonTransport)Reference(route, "transports", 0);
            SameArt(transport.transform, copy.Transport.transform);
            Assert.AreEqual(transport.HalfSize, copy.Transport.HalfSize);
            Assert.AreEqual(transport.TravelSeconds, copy.Transport.TravelSeconds);
            Assert.AreEqual(transport.OverloadTrap, copy.Transport.OverloadTrap);
            Assert.IsFalse(copy.Transport.OverloadTrap, "Current normal performance rejects overload and does not open trap doors.");
            Assert.AreEqual(Number(route, "boardingSeconds"), Number(copy.Route, "boardingSeconds"));
            Assert.AreEqual(Number(route, "returnDelaySeconds"), Number(copy.Route, "returnDelaySeconds"));
        }

        [Test]
        public void W6_CannonSource_OnlyCheckpointAndArrivalDecksAcceptSafeFlightLandings()
        {
            Assert.IsNotNull(copy.StartFloor.GetComponent<HumanCannonLandingZone>());
            Assert.IsNotNull(copy.MiddleFloor.GetComponent<HumanCannonLandingZone>());
            Assert.IsNotNull(copy.FinishFloor.GetComponent<HumanCannonLandingZone>());
            Assert.IsNull(copy.LowerFloor.GetComponent<HumanCannonLandingZone>(), "Missing the trampoline must remain a native bad landing, not silently become safe.");
            Assert.AreEqual(4, copy.Stations.Length);
            Assert.IsFalse(copy.gameObject.activeSelf, "Configure native systems before enabling their networking and pilot bindings.");
        }

        [Test]
        public void W6_CannonSource_ReusesActualContextualCanvasesWithoutTakingWaitingRoomUi()
        {
            var scene = EditorSceneManager.OpenPreviewScene(CurtainCall.UI.SceneIntegration.CommonPerformanceUI.ScenePath);
            try
            {
                var original = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<CurtainCall.UI.SceneIntegration.CommonPerformanceUI>(true)).Single();
                foreach (var pair in new[] {
                    (original.Guide.GetComponentInParent<Canvas>(true), copy.Guide.GetComponentInParent<Canvas>(true)),
                    (original.Tooltip.GetComponentInParent<Canvas>(true), copy.Tooltip.GetComponentInParent<Canvas>(true)) })
                {
                    SameArt(pair.Item1.transform, pair.Item2.transform);
                    Assert.AreEqual(pair.Item1.sortingOrder, pair.Item2.sortingOrder);
                    var labels = pair.Item1.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    var copies = pair.Item2.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    Assert.AreEqual(labels.Length, copies.Length);
                    for (int i = 0; i < labels.Length; i++)
                    {
                        Assert.AreSame(labels[i].font, copies[i].font);
                        Assert.AreSame(labels[i].fontSharedMaterial, copies[i].fontSharedMaterial);
                        Assert.AreEqual(labels[i].fontSize, copies[i].fontSize);
                        Assert.AreEqual(labels[i].color, copies[i].color);
                    }
                }
                Assert.IsNotNull(copy.DeviceUi);
                var fonts = original.GetComponent<CurtainCall.UI.SceneIntegration.UiRuntimeFontFallback>();
                var copiedFonts = copy.GetComponent<CurtainCall.UI.SceneIntegration.UiRuntimeFontFallback>();
                Assert.IsNotNull(copiedFonts, "The source's font policy lives outside the two canvas roots.");
                foreach (string field in new[] { "fallback", "mediumSource", "semiBoldSource" })
                    Assert.AreSame(Reference(fonts, field), Reference(copiedFonts, field), field);
                Assert.IsFalse(copy.DeviceUi.GetComponent<CurtainCall.UI.SceneIntegration.PerformanceSceneBinding>().enabled,
                    "Practice hints must not register as a performance space or restore timer/cheer/progress UI.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
