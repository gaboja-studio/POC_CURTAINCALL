using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        Component CreateOwnerContactFinalSaws()
        {
            var existing = StaticGet("CurtainCall.HumanCannon.HumanCannonFinalSaws", "Current") as Component;
            if (existing != null) return existing;
            var root = new GameObject("Final saw contact fixture"); root.SetActive(false);
            root.transform.position = player.transform.position + Vector3.right * 5 + Vector3.up * 5;
            var origin = new GameObject("Origin").transform; origin.SetParent(root.transform, false);
            var view = new GameObject("View").transform; view.SetParent(root.transform, false);
            var component = root.AddComponent(GameType("CurtainCall.HumanCannon.HumanCannonFinalSaws"));
            HazardSet(component, "horizontalOrigins", new[] {origin}); HazardSet(component, "horizontalViews", new[] {view});
            root.SetActive(true);
            return component;
        }
        [UnityTest]
        public IEnumerator OwnerContactGatePreservesVisibleGapsAndRejectsHostOnlyRemoteDamage()
        {
            DisableEnhancementThreats();
            var fixture = new GameObject("Gate contact fixture"); fixture.SetActive(false);
            fixture.transform.position = player.transform.position + Vector3.up * 4;
            var visual = new GameObject("Saw visual"); visual.transform.SetParent(fixture.transform, false);
            foreach (float x in new[] {-3f, 3f}) {
                var blade = GameObject.CreatePrimitive(PrimitiveType.Cube); blade.name = "GateSawDisc";
                blade.transform.SetParent(visual.transform, false); blade.transform.localPosition = new Vector3(x, .75f, 0);
                Object.Destroy(blade.GetComponent<Collider>());
            }
            var gate = fixture.AddComponent(GameType("CurtainCall.HumanCannon.HumanCannonSawGate"));
            HazardSet(gate, "sawVisual", visual.transform); fixture.SetActive(true);
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
            Call(mover, "SetStartPose", fixture.transform.position, Quaternion.identity, true);
            var remoteObject = Object.Instantiate((GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab"));
            var remoteNet = remoteObject.GetComponent(GameType("Unity.Netcode.NetworkObject"));
            try {
                yield return null;
                Assert.IsFalse((bool)Call(gate, "Touches", player), "The visible space between separate blades must remain safe.");
                Call(remoteNet, "SpawnWithOwnership", 777UL, true);
                var remote = remoteObject.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var feet = fixture.transform.position + Vector3.left * 3;
                Call(remoteObject.GetComponent(GameType("CurtainCall.Player.PlayerMover")), "SetStartPose", feet, Quaternion.identity, true);
                Assert.IsTrue((bool)Call(gate, "Touches", remote));
                Call(gate, "HostTick", Enum.Parse(GameType("CurtainCall.HumanCannon.PoleSawPhase"), "Active"), ulong.MaxValue);
                Assert.AreEqual(0, Convert.ToInt32(Get(remote, "LostParts")));
                Call(gate, "LateUpdate");
                var channel = Get(gate, "ownerContacts");
                double at = (double)Get(channel, "clock"); int restart = (int)Get(player, "RestartVersion");
                float height = (float)Get(mover, "BodyHeight");
                visual.transform.position += Vector3.forward * 20;
                Assert.IsFalse((bool)Call(gate, "TouchesDisplayedBlades", feet, height, (float)Get(mover, "BodyRadius")),
                    "An owner must use the current visible blade, not the host's previous sample.");
                Assert.IsTrue((bool)Call(gate, "GateContactAt", 0, at, feet, height, (float)Get(mover, "BodyRadius")),
                    "The host retains the earlier shape solely to validate delayed reports.");
                yield return new WaitForSeconds(.3f);
                Assert.IsFalse((bool)Call(channel, "Validate", Actor, 0, 1u, restart, 0d, at, player.transform.position, height, true));
                Assert.IsTrue((bool)Call(channel, "Validate", Actor, 0, 2u, restart, 0d, at, feet, height, true));
                AssertBody(4);
                Assert.IsTrue((bool)Call(channel, "Validate", Actor, 0, 3u, restart, 0d, at, feet, height, true));
                AssertBody(4);
                Call(gate, "ResetGate");
                Assert.IsFalse((bool)Call(channel, "Validate", Actor, 0, 4u, restart, 0d, at, feet, height, true));
                Call(gate, "CaptureContactFrame", 1000d);
                Call(gate, "CaptureContactFrame", 1d);
                Assert.IsTrue((bool)Call(gate, "GateContactAt", 0, 1d, feet + Vector3.forward * 20,
                    height, (float)Get(mover, "BodyRadius")), "A restarted host clock immediately records new geometry.");
                Assert.IsFalse((bool)Call(gate, "GateContactAt", 0, 1000d, feet + Vector3.forward * 20,
                    height, (float)Get(mover, "BodyRadius")), "Previous-session geometry cannot validate a report.");
            } finally {
                if ((bool)Get(remoteNet, "IsSpawned")) Call(remoteNet, "Despawn", true); else Object.Destroy(remoteObject);
                Object.Destroy(fixture);
            }
        }

        [UnityTest]
        public IEnumerator OwnerContactFinalSawsValidateHistoryAvoidanceAndDuplicateDamage()
        {
            DisableEnhancementThreats();
            var saws = CreateOwnerContactFinalSaws();
            yield return new WaitForSeconds(.25f);
            double now = (double)Get(saws, "Clock");
            HazardSet(saws, "warningSeconds", .05f); HazardSet(saws, "startedAt", now - .1);
            int index = Enumerable.Range(0, (int)Get(saws, "HorizontalCount")).First(i => (bool)Call(saws, "IsSawEnabled", i));
            double at = (double)Get(saws, "Clock"), epoch = (double)Get(saws, "StartedAt");
            var feet = (Vector3)Call(saws, "HorizontalPosition", index, at) - Vector3.up * ((float)Get(mover, "BodyHeight") * .5f);
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
            Call(mover, "SetStartPose", feet + Vector3.up * 5, Quaternion.identity, true);
            Call(saws, "OwnerContacts", at);
            var channel = Get(saws, "ownerContacts");
            int restart = (int)Get(player, "RestartVersion"); float height = (float)Get(mover, "BodyHeight");
            yield return new WaitForSeconds(.3f);
            Assert.IsFalse((bool)Call(channel, "Validate", Actor, index, 1u, restart, epoch, at, feet + Vector3.up * 5, height, true));
            Assert.IsFalse((bool)Call(channel, "Validate", Actor, index, 1u, restart - 1, epoch, at, feet, height, true));
            Assert.IsFalse((bool)Call(channel, "Validate", Actor, index, 1u, restart, epoch - 1, at, feet, height, true));
            Assert.IsFalse((bool)Call(channel, "Validate", Actor, index, 1u, restart, epoch, at - 2, feet, height, true));
            AssertBody(0);
            Assert.IsTrue((bool)Call(channel, "Validate", Actor, index, 2u, restart, epoch, at, feet, height, true));
            AssertBody(4);
            Assert.IsFalse((bool)Call(channel, "Validate", Actor, index, 2u, restart, epoch, at, feet, height, true));
            Assert.IsTrue((bool)Call(channel, "Validate", Actor, index, 3u, restart, epoch, at, feet, height, true));
            AssertBody(4);
            Call(channel, "Reset");
            Assert.IsFalse((bool)Call(channel, "Validate", Actor, index, 4u, restart, epoch, at, feet, height, true));
        }

        [UnityTest]
        public IEnumerator OwnerContactRouteFireValidatesContactBeforeFinishedAndRejectsReset()
        {
            DisableEnhancementThreats();
            var fires = (Array)Get(route, "fires");
            var fire = fires.Cast<Component>().First(f => f != null && (bool)Get(f, "IsConfigured"));
            int index = Array.IndexOf(fires.Cast<object>().ToArray(), fire);
            HazardSet(fire, "available", true); HazardSet(fire, "killOnContact", true);
            HazardSet(fire, "warningSeconds", .1f);
            Call(fire, "ResetWall");
            double at = (double)Get(route, "Clock");
            Assert.IsTrue((bool)Call(fire, "Arm", at - 2));
            double epoch = (double)Get(fire, "ArmedAt");
            var contact = (Vector3)Call(fire, "Front", at);
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
            Call(mover, "SetStartPose", contact + Vector3.up * 5, Quaternion.identity, true);
            Call(route, "TickOwnerFireContacts");
            var channel = Get(route, "ownerFireContacts");
            int restart = (int)Get(player, "RestartVersion"); float height = (float)Get(mover, "BodyHeight");
            // Model a report delayed until after the wall disappears; geometry at the reported time still applies.
            Call(fire, "Receive", epoch, true);
            yield return new WaitForSeconds(.3f);
            Assert.IsFalse((bool)Call(fire, "Threatens", contact, .3f, at));
            Assert.IsTrue((bool)Call(fire, "HistoricalThreatens", contact, .3f, at));
            Assert.IsFalse((bool)Call(channel, "Validate", Actor, index, 1u, restart, epoch, at, contact + Vector3.right * 1000, height, true));
            Assert.IsTrue((bool)Call(channel, "Validate", Actor, index, 2u, restart, epoch, at, contact, height, true));
            Assert.AreNotEqual("Normal", Get(player, "State").ToString());
            Call(fire, "ResetWall");
            Assert.IsFalse((bool)Call(channel, "Validate", Actor, index, 3u, restart, epoch, at, contact, height, true));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OwnerContactFinalSawsHostCannotCutRemotePosition()
        {
            DisableEnhancementThreats();
            var saws = CreateOwnerContactFinalSaws();
            yield return new WaitForSeconds(.25f);
            double now = (double)Get(saws, "Clock");
            HazardSet(saws, "warningSeconds", .05f); HazardSet(saws, "startedAt", now - .1);
            int index = Enumerable.Range(0, (int)Get(saws, "HorizontalCount")).First(i => (bool)Call(saws, "IsSawEnabled", i));
            var center = (Vector3)Call(saws, "HorizontalPosition", index, now);
            Call(mover, "SetStartPose", center + Vector3.up * 5, Quaternion.identity, true);
            var instance = Object.Instantiate((GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab"));
            var netObject = instance.GetComponent(GameType("Unity.Netcode.NetworkObject"));
            try {
                Call(netObject, "SpawnWithOwnership", 777UL, true);
                var remote = instance.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var remoteMover = instance.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
                Call(remoteMover, "SetStartPose", center - Vector3.up * ((float)Get(remoteMover, "BodyHeight") * .5f), Quaternion.identity, true);
                Call(saws, "HostContacts", now);
                Assert.AreEqual(0, Convert.ToInt32(Get(remote, "LostParts")));
            } finally { Call(netObject, "Despawn", true); }
            yield return null;
        }
    }
}
