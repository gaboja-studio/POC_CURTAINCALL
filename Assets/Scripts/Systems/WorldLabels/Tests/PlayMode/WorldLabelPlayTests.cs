using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CurtainCall.WorldLabels.Tests
{
    public sealed class WorldLabelPlayTests
    {
        readonly List<GameObject> owned = new List<GameObject>();
        readonly string[] kinds = { "Wood", "Cloth", "Paper", "Item", "Shelf" };
        readonly Vector2[] defaults = { new Vector2(320, 96), new Vector2(320, 96), new Vector2(240, 200), new Vector2(224, 64), new Vector2(96, 64) };
        readonly Vector2[] minimums = { new Vector2(192, 80), new Vector2(192, 80), new Vector2(192, 160), new Vector2(128, 56), new Vector2(96, 56) };
        float previousScale;
        GameObject Own(GameObject value) { owned.Add(value); return value; }
        GameObject Spawn(int kind)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/WorldLabels/WorldLabel" + kinds[kind]);
            Assert.NotNull(prefab, kinds[kind]);
            return Own(Object.Instantiate(prefab));
        }
        MonoBehaviour Component(GameObject root, string name) => root.GetComponentsInChildren<MonoBehaviour>(true).Single(x => x != null && x.GetType().Name == name);
        object Call(MonoBehaviour component, string name, params object[] args) => component.GetType().GetMethod(name).Invoke(component, args);
        T Value<T>(MonoBehaviour component, string name) => (T)component.GetType().GetProperty(name).GetValue(component);
        MonoBehaviour View(GameObject root) => Component(root, "WorldLabelView");
        TMP_Text Title(GameObject root) => root.GetComponentsInChildren<TMP_Text>(true).Single(x => x.name == "50_LabelTMP");
        TMP_Text Body(GameObject root) => root.GetComponentsInChildren<TMP_Text>(true).Single(x => x.name == "60_BodyTMP");
        bool Content(MonoBehaviour view, string title, string body = null, Sprite icon = null) => (bool)Call(view, "TrySetContent", title, body, icon);
        bool Size(MonoBehaviour view, Vector2 size) => (bool)Call(view, "TrySetSize", size);
        Transform Observer(Vector3 position) { var observer = Own(new GameObject("TestObserver")); observer.transform.position = position; return observer.transform; }
        void Glyphs(TMP_Text text)
        {
            Assert.AreEqual(AtlasPopulationMode.Static, text.font.atlasPopulationMode);
            Assert.IsTrue(text.font.HasCharacters(text.text.Replace("\n", "").Replace("\r", "")), text.text);
            text.ForceMeshUpdate(true, true);
            if (text.text.Length > 0) Assert.Greater(text.textInfo.characterCount, 0);
        }
        Vector3[][] Mesh(TMP_Text text)
        {
            text.ForceMeshUpdate(true, true);
            return text.textInfo.meshInfo.Select(x => x.vertices == null ? Array.Empty<Vector3>() : x.vertices.Take(x.vertexCount).ToArray()).ToArray();
        }
        void SameMesh(TMP_Text text, Vector3[][] before)
        {
            var after = Mesh(text); Assert.AreEqual(before.Length, after.Length);
            for (int i = 0; i < before.Length; ++i) CollectionAssert.AreEqual(before[i], after[i]);
        }
        [SetUp] public void SetUp() { previousScale = Time.timeScale; }
        [UnityTearDown] public IEnumerator TearDown()
        {
            try { foreach (var root in owned) if (root != null) Object.Destroy(root); owned.Clear(); yield return null; }
            finally { Time.timeScale = previousScale; }
        }

        [UnityTest] public IEnumerator FivePrefabsHaveExactGeometryAndPassiveInput()
        {
            for (int i = 0; i < kinds.Length; ++i)
            {
                var root = Spawn(i); var view = View(root); var canvas = root.GetComponent<Canvas>();
                Assert.NotNull(canvas); Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode);
                Assert.AreEqual(100, canvas.referencePixelsPerUnit);
                Assert.AreEqual(8, root.GetComponentsInChildren<Transform>(true).Length);
                Assert.AreEqual(5, root.GetComponentsInChildren<Image>(true).Length);
                Assert.AreEqual(2, root.GetComponentsInChildren<TMP_Text>(true).Length);
                Assert.AreEqual(defaults[i], Value<Vector2>(view, "LogicalSize"));
                Assert.AreEqual(defaults[i], ((RectTransform)root.transform).sizeDelta);
                Assert.IsEmpty(root.GetComponentsInChildren<GraphicRaycaster>(true));
                Assert.IsEmpty(root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true));
                foreach (var g in root.GetComponentsInChildren<Graphic>(true)) Assert.IsFalse(g.raycastTarget);
                var group = root.GetComponent<CanvasGroup>(); Assert.IsFalse(group.blocksRaycasts); Assert.IsFalse(group.interactable);
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    foreach (var c in t.GetComponents<UnityEngine.Component>()) Assert.NotNull(c);
                    if (t != root.transform) Assert.AreEqual(Vector3.one, t.localScale);
                }
                Assert.IsFalse(Component(root, "WorldLabelBillboard").enabled);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator SamplesHaveStaticGlyphsAndExpectedTypography()
        {
            for (int i = 0; i < kinds.Length; ++i)
            {
                var root = Spawn(i); var title = Title(root); Glyphs(title); Glyphs(Body(root));
                Assert.AreEqual(i == 4 ? 32 : 24, title.fontSize);
                Assert.IsFalse(title.enableAutoSizing); Assert.IsFalse(title.richText); Assert.IsFalse(title.parseCtrlCharacters);
                Assert.AreEqual(FontStyles.Normal, title.fontStyle);
                if (i == 4) StringAssert.Contains("Bold", title.font.name);
                else StringAssert.Contains("Medium", title.font.name);
                if (i == 2) { Assert.AreEqual(18, Body(root).fontSize); Assert.LessOrEqual(Body(root).textInfo.lineCount, 4); }
            }
            yield return null;
        }

        [UnityTest] public IEnumerator SlicedLayersAndShadowOffsetAreExact()
        {
            for (int i = 0; i < kinds.Length; ++i)
            {
                var root = Spawn(i);
                foreach (var image in root.GetComponentsInChildren<Image>(true).Where(x => x.name != "40_Icon"))
                {
                    Assert.AreEqual(Image.Type.Sliced, image.type); Assert.NotNull(image.sprite);
                    Assert.AreEqual(200, image.sprite.pixelsPerUnit); Assert.AreEqual(1, image.pixelsPerUnitMultiplier);
                    float border = i < 3 ? 32 : 24; Assert.AreEqual(new Vector4(border, border, border, border), image.sprite.border);
                }
                var shadow = root.transform.Find("00_Shadow").GetComponent<RectTransform>();
                Assert.AreEqual(-1, shadow.anchoredPosition.y);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ShortTitlesAndMinimumSizesAreAcceptedForAllKinds()
        {
            for (int i = 0; i < kinds.Length; ++i)
            {
                var root = Spawn(i); var view = View(root);
                Assert.IsTrue(Content(view, "1")); Assert.IsTrue(Size(view, minimums[i]), Value<string>(view, "LastIssue"));
                Assert.AreEqual(minimums[i], Value<Vector2>(view, "LogicalSize")); Glyphs(Title(root));
                if (i == 4) { Assert.AreEqual(40, Title(root).rectTransform.sizeDelta.y); Assert.AreEqual(-8, Title(root).rectTransform.anchoredPosition.y); Assert.LessOrEqual(Title(root).preferredHeight, 40); }
            }
            yield return null;
        }

        [UnityTest] public IEnumerator InvalidSizesAndLongTitlesPreserveAcceptedContentAndMesh()
        {
            for (int i = 0; i < kinds.Length; ++i)
            {
                var root = Spawn(i); var view = View(root); Assert.IsTrue(Content(view, "1")); var before = Mesh(Title(root));
                foreach (var invalid in new[] { Vector2.zero, new Vector2(-1, 100), new Vector2(float.NaN, 100), new Vector2(float.PositiveInfinity, 100), minimums[i] - Vector2.one })
                { Assert.IsFalse(Size(view, invalid)); Assert.AreEqual(defaults[i], Value<Vector2>(view, "LogicalSize")); }
                Assert.IsFalse(Content(view, new string('W', 300))); Assert.AreEqual("1", Value<string>(view, "Title")); SameMesh(Title(root), before);
                Assert.IsFalse(Content(view, " \r\n\t ")); Assert.AreEqual("1", Value<string>(view, "Title"));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator PaperAcceptsFourLinesAndRejectsFiveWithoutChangingMesh()
        {
            var root = Spawn(2); var view = View(root);
            Assert.IsTrue(Content(view, "1", "1\n2\n3\n4"), Value<string>(view, "LastIssue"));
            Glyphs(Body(root)); Assert.AreEqual(4, Body(root).textInfo.lineCount); var before = Mesh(Body(root));
            Assert.IsFalse(Content(view, "2", "1\n2\n3\n4\n5")); Assert.AreEqual("1", Value<string>(view, "Title"));
            Assert.AreEqual("1\n2\n3\n4", Value<string>(view, "Body")); SameMesh(Body(root), before);
            yield return null;
        }

        [UnityTest] public IEnumerator OnlyPaperAcceptsBodyAndTitlesFlattenNewlines()
        {
            for (int i = 0; i < kinds.Length; ++i)
            {
                var root = Spawn(i); var view = View(root);
                Assert.IsTrue(Content(view, "1\n2")); Assert.IsFalse(Value<string>(view, "Title").Contains("\n"));
                Assert.AreEqual(i == 2, Content(view, "1", "2"));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator OptionalIconsChangeOnlySupportedLayouts()
        {
            var source = Spawn(0); var sprite = source.transform.Find("40_Icon").GetComponent<Image>().sprite; Assert.NotNull(sprite);
            for (int i = 0; i < kinds.Length; ++i)
            {
                var root = Spawn(i); var view = View(root); bool supports = i == 0 || i == 1 || i == 3;
                Assert.AreEqual(supports, Content(view, "1", null, sprite));
                if (supports)
                {
                    float withIcon = Title(root).rectTransform.sizeDelta.x;
                    Assert.IsTrue(Content(view, "1")); Assert.IsFalse(root.transform.Find("40_Icon").GetComponent<Image>().enabled);
                    Assert.Greater(Title(root).rectTransform.sizeDelta.x, withIcon);
                }
            }
            yield return null;
        }

        [UnityTest] public IEnumerator SurfaceToggleHidesOnlyPhysicalFourLayers()
        {
            for (int i = 0; i < kinds.Length; ++i)
            {
                var root = Spawn(i); var view = View(root); bool iconEnabled = root.transform.Find("40_Icon").GetComponent<Image>().enabled;
                Assert.AreEqual(i < 3, (bool)Call(view, "TrySetSurfaceVisible", false));
                foreach (var image in root.GetComponentsInChildren<Image>(true).Where(x => x.name != "40_Icon")) Assert.AreEqual(i >= 3, image.enabled);
                Assert.IsTrue(Title(root).enabled); Assert.AreEqual(iconEnabled, root.transform.Find("40_Icon").GetComponent<Image>().enabled);
                Assert.IsTrue((bool)Call(view, "TrySetSurfaceVisible", true));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator InformationLabelsFailClosedWithoutObserver()
        {
            for (int i = 3; i < 5; ++i)
            {
                var root = Spawn(i); var view = View(root); Call(view, "SetVisible", true); yield return null;
                Assert.IsFalse(Value<bool>(view, "IsVisible")); Assert.AreEqual(0, root.GetComponent<CanvasGroup>().alpha);
                Call(Component(root, "WorldLabelProximity"), "BindObserver", (object)null);
                Assert.IsFalse(Value<bool>(view, "IsVisible"));
            }
        }

        [UnityTest] public IEnumerator ProximityHasInclusiveThresholdsAndHysteresis()
        {
            for (int i = 3; i < 5; ++i)
            {
                var root = Spawn(i); root.transform.position = Vector3.zero; var view = View(root); var gate = Component(root, "WorldLabelProximity");
                var observer = Observer(new Vector3(2.1f, 0, 0)); Call(gate, "Configure", observer, 2f, 2.4f);
                Assert.IsFalse(Value<bool>(view, "IsVisible")); observer.position = new Vector3(2, 0, 0); yield return null;
                Assert.IsTrue(Value<bool>(view, "IsVisible")); observer.position = new Vector3(2.3f, 0, 0); yield return null;
                Assert.IsTrue(Value<bool>(view, "IsVisible")); observer.position = new Vector3(2.4f, 0, 0); yield return null;
                Assert.IsTrue(Value<bool>(view, "IsVisible")); observer.position = new Vector3(2.5f, 0, 0); yield return null;
                Assert.IsFalse(Value<bool>(view, "IsVisible")); observer.position = new Vector3(2.2f, 0, 0); yield return null;
                Assert.IsFalse(Value<bool>(view, "IsVisible"));
            }
        }

        [UnityTest] public IEnumerator VisibilityRequestDoesNotOverrideProximity()
        {
            var root = Spawn(3); var view = View(root); var gate = Component(root, "WorldLabelProximity"); var observer = Observer(root.transform.position);
            Call(gate, "BindObserver", observer); Assert.IsTrue(Value<bool>(view, "IsVisible"));
            Call(view, "SetVisible", false); Assert.IsFalse(Value<bool>(view, "IsVisible"));
            observer.position += Vector3.right * 3; yield return null; Call(view, "SetVisible", true); Assert.IsFalse(Value<bool>(view, "IsVisible"));
            observer.position = root.transform.position; yield return null; Assert.IsTrue(Value<bool>(view, "IsVisible"));
        }

        [UnityTest] public IEnumerator InvalidDistancesPreserveThresholdsAndRebindingResetsHysteresis()
        {
            var root = Spawn(4); var view = View(root); var gate = Component(root, "WorldLabelProximity");
            var observer = Observer(root.transform.position); Call(gate, "BindObserver", observer);
            foreach (var pair in new[] { new Vector2(0, 1), new Vector2(3, 2), new Vector2(float.NaN, 3), new Vector2(1, float.PositiveInfinity) })
                Assert.IsFalse((bool)Call(gate, "TrySetDistances", pair.x, pair.y));
            Assert.AreEqual(2, Value<float>(gate, "EnterDistance")); Assert.AreEqual(2.4f, Value<float>(gate, "ExitDistance"));
            observer.position = root.transform.position + Vector3.right * 2.2f; yield return null; Assert.IsTrue(Value<bool>(view, "IsVisible"));
            Call(gate, "BindObserver", observer); Assert.IsFalse(Value<bool>(view, "IsVisible"));
        }

        [UnityTest] public IEnumerator InactiveMissingAndDisabledObserverGateFailClosed()
        {
            var root = Spawn(3); var view = View(root); var gate = Component(root, "WorldLabelProximity"); var observer = Observer(root.transform.position);
            Call(gate, "BindObserver", observer); Assert.IsTrue(Value<bool>(view, "IsVisible"));
            observer.gameObject.SetActive(false); yield return null; Assert.IsFalse(Value<bool>(view, "IsVisible"));
            observer.gameObject.SetActive(true); yield return null; Assert.IsTrue(Value<bool>(view, "IsVisible"));
            gate.enabled = false; Assert.IsFalse(Value<bool>(view, "IsVisible")); gate.enabled = true; yield return null;
            Assert.IsTrue(Value<bool>(view, "IsVisible")); Object.Destroy(observer.gameObject); yield return null;
            // Destroy는 프레임 끝에 반영됩니다. 실제 소멸 뒤 한 번의 LateUpdate 판정을 기다립니다.
            Assert.IsTrue(observer == null); yield return null;
            Assert.IsFalse(Value<bool>(view, "IsVisible"));
        }

        [UnityTest] public IEnumerator BillboardUsesOnlyAssignedCameraAndRestoresRotation()
        {
            var root = Spawn(0); var billboard = Component(root, "WorldLabelBillboard");
            var parent = Own(new GameObject("TestParent")); parent.transform.rotation = Quaternion.Euler(0, 35, 0);
            root.transform.SetParent(parent.transform, true); root.transform.localRotation = Quaternion.Euler(5, 10, 15);
            var original = root.transform.localRotation; var position = root.transform.position; var scale = root.transform.localScale;
            var camera = Own(new GameObject("AssignedCamera")).AddComponent<Camera>(); camera.enabled = false; camera.transform.rotation = Quaternion.Euler(10, 50, 0);
            Call(billboard, "SetCamera", camera); billboard.enabled = true; yield return null;
            Assert.Less(Quaternion.Angle(camera.transform.rotation, root.transform.rotation), .001f);
            Assert.AreEqual(position, root.transform.position); Assert.AreEqual(scale, root.transform.localScale);
            billboard.enabled = false; Assert.Less(Quaternion.Angle(original, root.transform.localRotation), .001f);
            billboard.enabled = true; Call(billboard, "SetCamera", (object)null); yield return null;
            Assert.Less(Quaternion.Angle(original, root.transform.localRotation), .001f);
        }

        [UnityTest] public IEnumerator GatesAndBillboardWorkWhileTimeScaleIsZero()
        {
            Time.timeScale = 0;
            var root = Spawn(3); var view = View(root); var gate = Component(root, "WorldLabelProximity"); var observer = Observer(root.transform.position + Vector3.right * 3);
            Call(gate, "BindObserver", observer); yield return null; Assert.IsFalse(Value<bool>(view, "IsVisible"));
            observer.position = root.transform.position; yield return null; Assert.IsTrue(Value<bool>(view, "IsVisible"));
            Assert.IsTrue(Content(view, "1")); Assert.AreEqual(0, Time.timeScale);
        }

        [UnityTest] public IEnumerator DisableEnablePreservesAcceptedContentAndSurfaceState()
        {
            var root = Spawn(0); var view = View(root); Assert.IsTrue(Content(view, "1")); Assert.IsTrue((bool)Call(view, "TrySetSurfaceVisible", false));
            Call(view, "SetVisible", false); root.SetActive(false); yield return null; root.SetActive(true); yield return null;
            Assert.AreEqual("1", Value<string>(view, "Title")); Assert.IsFalse(Value<bool>(view, "SurfaceVisible")); Assert.IsFalse(Value<bool>(view, "IsVisible"));
            foreach (var image in root.GetComponentsInChildren<Image>(true).Where(x => x.name != "40_Icon")) Assert.IsFalse(image.enabled);
        }

        [UnityTest] public IEnumerator InactiveContentMeasurementsAreRejectedWithoutMutation()
        {
            var root = Spawn(0); var view = View(root); Assert.IsTrue(Content(view, "1")); root.SetActive(false);
            Assert.IsFalse(Content(view, "2")); Assert.IsFalse(Size(view, defaults[0] + Vector2.one * 10));
            Assert.AreEqual("1", Value<string>(view, "Title")); Assert.AreEqual(defaults[0], Value<Vector2>(view, "LogicalSize"));
            root.SetActive(true); yield return null; Glyphs(Title(root)); Assert.AreEqual("1", Title(root).text);
        }
    }
}
