using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class EscapePoleFeedbackTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static Type TypeNamed(string name) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

        static void Set(object owner, string name, object value)
        {
            var type = owner.GetType();
            var property = type.GetProperty(name, Instance);
            if (property != null) property.SetValue(owner, value);
            else type.GetField(name, Instance).SetValue(owner, value);
        }

        static object Call(object owner, string name, params object[] args) =>
            owner.GetType().GetMethod(name, Instance).Invoke(owner, args);

        [Test]
        public void GrabRequiresReachAnUnblockedPathAndAFreeSeat()
        {
            var playerType = TypeNamed("CurtainCall.Network.PlayerSync.NetworkPlayer");
            var polesType = TypeNamed("CurtainCall.HumanCannon.HumanCannonPoleSystem");
            var cannonsType = TypeNamed("CurtainCall.HumanCannon.HumanCannonSystem");
            var oldPoles = polesType.GetProperty("Current", Static).GetValue(null);
            var oldCannons = cannonsType.GetProperty("Current", Static).GetValue(null);
            var oldLocal = playerType.GetProperty("Local", Static).GetValue(null);
            var origin = new Vector3(21000, 30, 21000);
            var root = new GameObject("Pole reach fixture");
            var actor = new GameObject("Entrant");
            root.SetActive(false);
            actor.SetActive(false);
            Component player = null;
            GameObject wall = null;
            try
            {
                root.transform.position = origin + new Vector3(0, 1, 2);
                actor.transform.position = origin;
                var pole = root.AddComponent(TypeNamed("CurtainCall.HumanCannon.HumanCannonPole"));
                Set(pole, "requireClearGrabPath", true);
                var system = root.AddComponent(polesType);
                var poles = Array.CreateInstance(pole.GetType(), 1);
                poles.SetValue(pole, 0);
                Set(system, "poles", poles);
                Set(system, "allowPlatformJumpGrab", true);
                Set(system, "independentInteraction", true);
                var runtime = polesType.GetNestedType("Runtime", BindingFlags.NonPublic);
                var states = Array.CreateInstance(runtime, 1);
                states.SetValue(Activator.CreateInstance(runtime, new object[] { 1 }), 0);
                Set(system, "states", states);
                var cannon = root.AddComponent(cannonsType);
                Call(cannon, "ResetSeats");
                player = actor.AddComponent(playerType);
                Call(actor.GetComponent(TypeNamed("CurtainCall.Player.PlayerMover")), "Awake");
                Set(player, "IsSpawned", true);
                Set(player, "OwnerClientId", 42UL);
                root.SetActive(true);
                actor.SetActive(true);
                playerType.GetProperty("Local", Static).SetValue(null, player);
                polesType.GetProperty("Current", Static).SetValue(null, system);
                cannonsType.GetProperty("Current", Static).SetValue(null, cannon);

                int FindGrip()
                {
                    Physics.SyncTransforms();
                    return (int)Call(system, "FindGrabbable", player);
                }

                Assert.That(FindGrip(), Is.Zero);
                actor.transform.position = origin + Vector3.back * 8;
                Assert.That(FindGrip(), Is.EqualTo(-1));
                actor.transform.position = origin;
                wall = new GameObject("Blocking wall", typeof(BoxCollider));
                wall.transform.position = origin + new Vector3(0, 1, 1);
                wall.GetComponent<BoxCollider>().size = new Vector3(4, 3, .2f);
                Assert.That(FindGrip(), Is.EqualTo(-1));
                wall.SetActive(false);
                Assert.That(FindGrip(), Is.Zero);
                var seat = Call(system, "State", 0);
                Assert.That((bool)Call(seat, "TryGrab", 99UL, 0U, 0d, 0f, false, 0), Is.True);
                Assert.That(FindGrip(), Is.EqualTo(-1));
            }
            finally
            {
                if (player != null) Set(player, "IsSpawned", false);
                if (wall != null) Object.DestroyImmediate(wall);
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(root);
                playerType.GetProperty("Local", Static).SetValue(null, oldLocal);
                polesType.GetProperty("Current", Static).SetValue(null, oldPoles);
                cannonsType.GetProperty("Current", Static).SetValue(null, oldCannons);
            }
        }

        [Test]
        public void ConveyorCatchZoneHasOneFixedSpotlightAndNoCarriageLights()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Levels/escape_route.unity");
            try
            {
                var frame = scene.GetRootGameObjects().Single(t => t.name == "EscapeCampaignFrame");
                var rail = frame.transform.Find("EscapeBackground/35_AOpenFacility/04_ExpandedOverheadRail");
                Assert.That(rail, Is.Not.Null);
                var fixture = rail.GetComponentsInChildren<Light>(true).Single(l => l.name == "작업등 광원");
                var zone = fixture.transform.parent.parent;
                Assert.That(zone.GetComponentsInChildren<Light>(true).Count(l => l.isActiveAndEnabled), Is.EqualTo(1));
                Assert.That(fixture.isActiveAndEnabled && fixture.type == LightType.Spot, Is.True);
                var shaft = fixture.transform.parent.Find("먼지에 비치는 빛줄기");
                Assert.That(shaft.gameObject.activeInHierarchy && shaft.GetComponent<Renderer>().enabled, Is.True);
                var loops = frame.GetComponentsInChildren(TypeNamed("CurtainCall.HumanCannon.HumanCannonPoleLoop"), true);
                Assert.That(loops, Is.Not.Empty);
                foreach (var loop in loops)
                    Assert.That(loop.GetComponentsInChildren<Light>(true), Is.Empty, loop.name);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
