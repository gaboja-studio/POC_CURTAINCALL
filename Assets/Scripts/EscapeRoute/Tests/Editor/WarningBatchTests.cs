using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class WarningBatchTests
    {
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

        [Test]
        public void TransformReferencedWarningDescendantsAreNeverExtractedAsStaticFactoryGeometry()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var background = new GameObject("Fixture background"); SceneManager.MoveGameObjectToScene(background, scene);
            var warning = new GameObject("Cue-owned warning"); warning.transform.SetParent(background.transform);
            var strip = new GameObject("Nested warning strip"); strip.transform.SetParent(warning.transform);
            var renderer = strip.AddComponent<MeshRenderer>();
            var owner = new GameObject("Separate hazard owner"); owner.transform.SetParent(background.transform); owner.SetActive(false);
            var cue = owner.AddComponent(T("CurtainCall.HumanCannon.HumanCannonHazardCue"));
            cue.GetType().GetField("warning").SetValue(cue, warning.transform);
            var builder = T("CurtainCall.EscapeRoute.EditorTools.EscapeFactoryArtPass");
            var previousBackground = builder.GetField("background", Static).GetValue(null);
            var previousReferences = builder.GetField("referenced", Static).GetValue(null);
            try
            {
                builder.GetField("background", Static).SetValue(null, background.transform);
                builder.GetMethod("CollectReferences", Static).Invoke(null, new object[] { scene });
                Assert.That(builder.GetMethod("PureStatic", Static).Invoke(null, new object[] { renderer }), Is.False);
            }
            finally
            {
                builder.GetField("background", Static).SetValue(null, previousBackground);
                builder.GetField("referenced", Static).SetValue(null, previousReferences);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void WarningStartsHiddenBlinksOnlyBeforeFireAndClearsOnStopOrDisable()
        {
            var owner = new GameObject("Warning clock fixture"); owner.SetActive(false);
            var warning = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var type = T("CurtainCall.HumanCannon.HumanCannonHazardCue"); var cue = owner.AddComponent(type);
            type.GetField("warning").SetValue(cue, warning.transform);
            type.GetField("warningSeconds").SetValue(cue, 1.15f);
            type.GetField("activeSeconds").SetValue(cue, 1.6f); type.GetField("restSeconds").SetValue(cue, 2.25f);
            void Present(double elapsed, bool running) => type.GetMethod("Present").Invoke(cue, new object[] { elapsed, running });
            try
            {
                owner.SetActive(true);
                type.GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cue, null);
                Assert.IsFalse(warning.activeSelf);
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    double epoch = cycle * 5d;
                    Present(epoch + .02, true); Assert.IsTrue(warning.activeInHierarchy);
                    Present(epoch + .20, true); Assert.IsFalse(warning.activeInHierarchy);
                    Present(epoch + .36, true); Assert.IsTrue(warning.activeInHierarchy);
                    Present(epoch + 1.16, true); Assert.IsFalse(warning.activeInHierarchy);
                    Present(epoch + 3, true); Assert.IsFalse(warning.activeInHierarchy);
                }
                Present(.02, true); Present(.02, false); Assert.IsFalse(warning.activeSelf);
                Present(.02, true); owner.SetActive(false);
                type.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cue, null);
                Assert.IsFalse(warning.activeSelf);
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(warning); }
        }
    }
}
