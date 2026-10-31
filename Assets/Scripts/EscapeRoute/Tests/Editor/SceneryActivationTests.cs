using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class SceneryActivationTests
    {
        GameObject background;
        Type type;
        object activation;
        [SetUp] public void SetUp()
        {
            background = new GameObject("Background");
            type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.EscapeRoute.EscapeSceneryActivation")).First(t => t != null);
            activation = Activator.CreateInstance(type);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(background);
        object Call(string method, params object[] args) => type.GetMethod(method).Invoke(activation, args);
        GameObject Prop(Transform parent, string name)
        {
            var item = new GameObject(name); item.transform.SetParent(parent);
            item.AddComponent<MeshRenderer>(); item.AddComponent<BoxCollider>(); return item;
        }

        [Test] public void ModulesActivateInOrderWithoutSplittingGameplayOrEnablingAuthoredInactiveObjects()
        {
            var scenery = Prop(background.transform, "Wall");
            var animated = Prop(background.transform, "Animated machinery"); animated.AddComponent<Animator>();
            var controlled = Prop(background.transform, "Controlled mechanism");
            var logic = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.EscapeRoute.EscapeIndependentInteractions")).First(t => t != null);
            controlled.AddComponent(logic);
            var child = Prop(controlled.transform, "Mechanism part");
            var inactive = Prop(background.transform, "Preserved old scenery"); inactive.SetActive(false);
            Call("Prepare", background); Call("Suspend");
            Assert.That(scenery.activeSelf, Is.False);
            Assert.That(animated.activeSelf || controlled.activeSelf, Is.False);
            Assert.That(child.activeSelf, Is.True, "Controlled descendants must not be split away from their owner.");
            Assert.That(controlled.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That((bool)Call("Advance", 256), Is.False);
            Assert.That(scenery.activeSelf, Is.True);
            Assert.That(animated.activeSelf || controlled.activeSelf, Is.False);
            Assert.That((bool)Call("Advance", 256), Is.False);
            Assert.That(animated.activeSelf && !controlled.activeSelf, Is.True);
            Assert.That((bool)Call("Advance", 256), Is.True);
            Assert.That(controlled.activeSelf && child.activeInHierarchy, Is.True);
            Call("Restore");
            Assert.That(inactive.activeSelf, Is.False);
        }

        [Test] public void ActivationIsBoundedAndCancellationRestoresEveryAuthoredObject()
        {
            var props = new GameObject[600];
            for (int group = 0; group < 3; group++)
            {
                var module = new GameObject("Module"); module.transform.SetParent(background.transform);
                for (int i = 0; i < 200; i++) props[group * 200 + i] = Prop(module.transform, "Scenery");
            }
            Call("Prepare", background); Call("Suspend");
            Assert.That(props.Count(p => p.activeSelf), Is.Zero);
            for (int i = 0; i < 3; i++) Assert.That((bool)Call("Advance", 256), Is.False);
            Assert.That(props.Count(p => p.activeSelf), Is.Zero);
            Assert.That((bool)Call("Advance", 256), Is.False);
            Assert.That(props.Count(p => p.activeSelf), Is.EqualTo(256));
            Call("Suspend"); // A departure request must not repeat the preload work or reset progress.
            Assert.That(props.Count(p => p.activeSelf), Is.EqualTo(256));
            Call("Restore");
            Assert.That(props.All(p => p.activeSelf), Is.True);
            Call("Suspend");
            int frames = 0;
            do { frames++; } while (!(bool)Call("Advance", 256));
            Assert.That(frames, Is.EqualTo(6));
            Assert.That(props.All(p => p.activeSelf), Is.True);
        }
    }
}
