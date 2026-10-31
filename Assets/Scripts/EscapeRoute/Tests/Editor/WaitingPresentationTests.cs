using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class WaitingPresentationTests
    {
        GameObject room, passage;
        object presentation;
        Type type;
        [SetUp] public void SetUp()
        {
            room = new GameObject("Waiting room"); passage = new GameObject("Passage");
            type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.EscapeRoute.EscapeWaitingPresentation")).First(t => t != null);
            presentation = Activator.CreateInstance(type);
        }
        void Hide(params GameObject[] roots) => type.GetMethod("Hide").Invoke(presentation, new object[] { roots });
        void Restore() => type.GetMethod("Restore").Invoke(presentation, null);
        [TearDown] public void TearDown() { Restore(); Object.DestroyImmediate(room); Object.DestroyImmediate(passage); }

        [Test] public void SealedRoomHidesGraphicsButPreservesPassageCollisionAndServices()
        {
            var renderer = room.AddComponent<MeshRenderer>();
            var collider = room.AddComponent<BoxCollider>();
            var light = room.AddComponent<Light>();
            var passageRenderer = passage.AddComponent<MeshRenderer>();
            Hide(room);
            Assert.That(renderer.forceRenderingOff, Is.True);
            Assert.That(light.enabled, Is.False);
            Assert.That(room.activeInHierarchy && collider.enabled && renderer.enabled, Is.True);
            Assert.That(passageRenderer.forceRenderingOff, Is.False);
        }
        [Test] public void RepeatedHideAndOverlappingRootsRestoreExactlyTheOriginalGraphicsState()
        {
            var child = new GameObject("Child"); child.transform.SetParent(room.transform);
            var visible = room.AddComponent<MeshRenderer>();
            var alreadyHidden = child.AddComponent<MeshRenderer>(); alreadyHidden.forceRenderingOff = true;
            var disabledLight = child.AddComponent<Light>(); disabledLight.enabled = false;
            var lit = room.AddComponent<Light>();
            Hide(room, child); Hide(room); Restore(); Restore();
            Assert.That(visible.forceRenderingOff, Is.False);
            Assert.That(alreadyHidden.forceRenderingOff, Is.True);
            Assert.That(disabledLight.enabled, Is.False);
            Assert.That(lit.enabled, Is.True);
        }
        [Test] public void NewDepartureUsesCurrentStateAndDoesNotEnableInactiveRooms()
        {
            var renderer = room.AddComponent<MeshRenderer>();
            Hide(room); Restore(); renderer.forceRenderingOff = true;
            Hide(room); Restore(); Assert.That(renderer.forceRenderingOff, Is.True);
            room.SetActive(false); Hide(room); Restore(); Assert.That(room.activeSelf, Is.False);
        }
        [Test] public void SmallRoomRetiresAndRestoresEveryRenderer()
        {
            var views = new MeshRenderer[600];
            for (int i = 0; i < views.Length; i++)
            {
                var child = new GameObject("Prop"); child.transform.SetParent(room.transform);
                views[i] = child.AddComponent<MeshRenderer>();
            }
            Hide(room); Hide(room);
            Assert.That(views.All(r => r.forceRenderingOff), Is.True);
            Restore();
            Assert.That(views.Any(r => r.forceRenderingOff), Is.False);
        }
        [Test] public void CachedPropCarriedOutOfTheRoomRemainsVisible()
        {
            var prop = new GameObject("Carried prop"); prop.transform.SetParent(room.transform);
            var renderer = prop.AddComponent<MeshRenderer>();
            type.GetMethod("Prepare").Invoke(presentation, new object[] { new[] { room } });
            prop.transform.SetParent(passage.transform);
            Hide(room);
            Assert.That(renderer.forceRenderingOff, Is.False);
        }
        [Test] public void LargeRoomRetirementIsBoundedPerFrameAndCancellationRestoresIt()
        {
            var views = new MeshRenderer[1600];
            for (int i = 0; i < views.Length; i++)
            {
                var child = new GameObject("Prop"); child.transform.SetParent(room.transform);
                views[i] = child.AddComponent<MeshRenderer>();
            }
            Hide(room); Hide(room);
            Assert.That(views.Count(r => r.forceRenderingOff), Is.EqualTo(1024));
            Restore();
            Assert.That(views.Any(r => r.forceRenderingOff), Is.False);
        }
    }
}
