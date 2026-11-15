using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>Moving object endpoint: arrows glide between path refreshes and keep flashing every pulse interval.</summary>
    public sealed class NavigationSmoothFollowPlayTests
    {
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        static object Call(object target, string name, params object[] args) =>
            target.GetType().GetMethods().Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);

        [UnityTest]
        public IEnumerator MovingObjectDestination_GlidesWithoutSnappingAndPulsesEveryInterval()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Smooth navigation floor";
            floor.transform.position = new Vector3(0, -.1f, 40); floor.transform.localScale = new Vector3(40, .2f, 40);
            var goal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goal.name = "Moving navigation goal";
            Object.DestroyImmediate(goal.GetComponent<Collider>());
            goal.transform.position = new Vector3(-6, .5f, 52);
            var target = T("CurtainCall.UI.Wayfinding.NavigationTarget");
            object destination = target.GetMethod("Object", new[] { typeof(Transform) }).Invoke(null, new object[] { goal.transform });
            object start = target.GetMethod("Position").Invoke(null, new object[] { new Vector3(0, 0, 28) });
            var handle = (IDisposable)T("CurtainCall.UI.Wayfinding.WorldNavigation").GetMethod("Show")
                .Invoke(null, new object[] { destination, start, false, 1f, 1f, (Color?)Color.cyan });
            try
            {
                float deadline = Time.realtimeSinceStartup + 20;
                while (!(bool)Get(handle, "IsVisible") && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue((bool)Get(handle, "IsVisible"), "Route to the moving object must appear.");
                yield return new WaitForSecondsRealtime(.6f); // first appearance fades in

                var service = (Component)Object.FindFirstObjectByType(T("CurtainCall.UI.Wayfinding.NavigationService"));
                var view = service.GetComponentsInChildren(T("CurtainCall.UI.Wayfinding.RehearsalNavigationView"))
                    .First(v => ((Transform)Get(v, "Destination")).gameObject.activeSelf);
                var pulse = view.GetComponent(T("CurtainCall.UI.Wayfinding.NavigationPulseSequence"));
                float interval = (float)Get(Get(T("CurtainCall.Settings.GameSettings").GetProperty("Base").GetValue(null), "Navigation"), "PulseIntervalSeconds");
                Assert.AreEqual(interval, (float)Get(pulse, "PulseIntervalSeconds"), .001f, "Guides without overrides use the common interval.");
                var markers = ((IEnumerable<Transform>)Get(view, "MarkerInstances")).ToList();
                var goalMarker = (Transform)Get(view, "Destination");
                int nearIndex = Mathf.Max(0, (int)Get(view, "VisibleMarkerCount") - 3); // stays on the route while its length changes
                var nearGlyph = markers[nearIndex].GetComponent(T("CurtainCall.UI.Wayfinding.NavigationPulseGlyph"));
                var previous = markers.Select(m => m.position).ToArray();
                var previousActive = markers.Select(m => m.gameObject.activeSelf).ToArray();
                var goalStart = goalMarker.position; var previousGoal = goalMarker.position;
                float maxStep = 0, maxGoalStep = 0, elapsed = 0; int flashes = 0; bool lit = false;
                while (elapsed < 3 * interval)
                {
                    yield return null;
                    float dt = Time.unscaledDeltaTime; elapsed += dt;
                    goal.transform.position += Vector3.right * 3 * dt; // walking-speed target
                    for (int i = 0; i < markers.Count; i++)
                    {
                        bool active = markers[i].gameObject.activeSelf;
                        float presence = (float)Call(view, "GetMarkerPresence", i);
                        if (active && previousActive[i] && presence > .05f)
                            maxStep = Mathf.Max(maxStep, Vector3.Distance(previous[i], markers[i].position) / Mathf.Max(dt, 1 / 60f) / 60);
                        previous[i] = markers[i].position; previousActive[i] = active;
                    }
                    maxGoalStep = Mathf.Max(maxGoalStep, Vector3.Distance(previousGoal, goalMarker.position) / Mathf.Max(dt, 1 / 60f) / 60);
                    previousGoal = goalMarker.position;
                    float opacity = (float)Get(nearGlyph, "Opacity");
                    if (!lit && opacity > .9f) flashes++;
                    lit = opacity > .9f ? true : opacity < .1f ? false : lit;
                }
                Debug.Log($"[NavigationSmooth] max marker step/60fps={maxStep:0.000}m goal step/60fps={maxGoalStep:0.000}m flashes={flashes} goal moved={Vector3.Distance(goalStart, goalMarker.position):0.00}m");
                Assert.Greater(Vector3.Distance(goalStart, goalMarker.position), 5, "Destination marker must follow the moving object.");
                Assert.Less(maxGoalStep, .2f, "Destination must glide instead of jumping at each path refresh.");
                Assert.Less(maxStep, .2f, "Route arrows must glide instead of jumping at each path refresh.");
                Assert.GreaterOrEqual(flashes, 2, "The arrow near the start must keep flashing every pulse interval.");

                // Shorten the route abruptly: arrows outside the new route must vanish on the same refresh, not fade out.
                int before = markers.Count(m => m.gameObject.activeSelf);
                goal.transform.position = new Vector3(0, .5f, 37);
                deadline = Time.realtimeSinceStartup + 3;
                Vector3[] path;
                do { yield return null; path = (Vector3[])Get(handle, "Path"); }
                while ((path.Length == 0 || Vector3.Distance(path[path.Length - 1], new Vector3(0, 0, 37)) > 1.5f) && Time.realtimeSinceStartup < deadline);
                int visible = (int)Get(view, "VisibleMarkerCount");
                var lingering = markers.Select((m, i) => (m, i)).Where(p => p.m.gameObject.activeSelf && p.i >= visible).ToList();
                Debug.Log($"[NavigationSmooth] shortened route: active before={before}, visible slots after={visible}, lingering={lingering.Count}");
                Assert.Less(visible, before, "The shortened route must use fewer arrows.");
                Assert.IsEmpty(lingering, "Arrows outside the new route must disappear immediately.");

                // Common settings (GameSettings > Base > 네비게이션 안내) apply live to guides without overrides.
                var common = Get(T("CurtainCall.Settings.GameSettings").GetProperty("Base").GetValue(null), "Navigation");
                var colorField = common.GetType().GetField("color", BindingFlags.Instance | BindingFlags.NonPublic);
                var intervalField = common.GetType().GetField("pulseIntervalSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
                var originalColor = colorField.GetValue(common); var originalInterval = intervalField.GetValue(common);
                try
                {
                    Call(handle, "SetAppearance", null, null, null); // drop the explicit cyan override
                    colorField.SetValue(common, Color.magenta); intervalField.SetValue(common, 2f);
                    yield return new WaitForSecondsRealtime(.4f);
                    Assert.AreEqual(2f, (float)Get(pulse, "PulseIntervalSeconds"), .001f, "Common pulse interval must apply live.");
                    var layer = goalMarker.GetComponentsInChildren<SpriteRenderer>(true).First(r => r.color.r + r.color.g + r.color.b > .1f);
                    var block = new MaterialPropertyBlock(); layer.GetPropertyBlock(block);
                    Assert.AreEqual(Color.magenta, block.GetColor("_SignalColor"), "Common color must apply live.");
                    Call(handle, "SetAppearance", (Color?)Color.yellow, null, null);
                    yield return new WaitForSecondsRealtime(.4f);
                    layer.GetPropertyBlock(block);
                    Assert.AreEqual(Color.yellow, block.GetColor("_SignalColor"), "A per-guide override must win over the common color.");
                }
                finally { colorField.SetValue(common, originalColor); intervalField.SetValue(common, originalInterval); }
            }
            finally
            {
                handle.Dispose();
                Object.Destroy(goal); Object.Destroy(floor);
            }
        }
    }
}
