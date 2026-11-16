#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed class PursuitUpgradeRuntimeTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type T(string n) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(n)).First(t => t != null);
        static object Get(object o, string n) => o.GetType().GetProperty(n, Flags)?.GetValue(o) ?? o.GetType().GetField(n, Flags)?.GetValue(o);
        static void Set(object o, string n, object v) { var p = o.GetType().GetProperty(n, Flags); if (p != null) p.SetValue(o, v); else o.GetType().GetField(n, Flags).SetValue(o, v); }
        static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
        static readonly Vector3 Origin = new(-26000, 0, 26000);
        GameObject root;
        Component director, room;
        NavMeshData data;
        NavMeshDataInstance nav;
        object pursuitRules;
        string savedRules;

        void Setup(bool tunnel)
        {
            var waiting = T("CurtainCall.Settings.GameSettings").GetProperty("WaitingRoom").GetValue(null);
            pursuitRules = Get(waiting, "Pursuit"); savedRules = JsonUtility.ToJson(pursuitRules);
            root = new GameObject("Pursuit upgrade fixture"); root.SetActive(false);
            var sources = new List<NavMeshBuildSource>();
            void Box(Vector3 center, Vector3 size) {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.SetParent(root.transform);
                go.transform.position = Origin + center; go.transform.localScale = size;
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(Origin + center, Quaternion.identity, Vector3.one), size = size });
            }
            Box(new Vector3(0, -.25f, 0), new Vector3(12, .5f, 14));
            if (tunnel) Box(new Vector3(0, 1.35f, 1), new Vector3(4, .3f, 4));
            else Box(new Vector3(0, 1.5f, 3), new Vector3(3, 3, 2));
            var settings = NavMesh.GetSettingsByIndex(0); settings.agentRadius = .24f; settings.agentHeight = 1.08f;
            settings.agentClimb = .55f; settings.overrideVoxelSize = true; settings.voxelSize = .08f;
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Origin + Vector3.up * 2, new Vector3(14, 10, 16)), Vector3.zero, Quaternion.identity);
            nav = NavMesh.AddNavMeshData(data);
            room = root.AddComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitRoom")); Set(room, "Count", 0);
            director = root.AddComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitDirector")); ((Behaviour)director).enabled = false;
            var rooms = Array.CreateInstance(room.GetType(), 1); rooms.SetValue(room, 0); Set(director, "Rooms", rooms);
            var graph = AssetDatabase.LoadAllAssetsAtPath("Assets/_ThirdParty/curtaincall/Pursuit/Behaviour/PursuitBehaviour.asset").First(a => a.GetType().Name == "BehaviorGraph");
            Set(director, "BehaviourGraph", graph);
            root.SetActive(true); Set(director, "active", true); Set(director, "Elapsed", 120f); Set(director, "forcedStage", 0);
            // An inactive authority stub avoids starting any campaign or touching the user's run.
            var runGo = new GameObject("Inactive run fixture"); runGo.SetActive(false); runGo.transform.SetParent(root.transform);
            var run = runGo.AddComponent(T("CurtainCall.Campaign.CampaignRun"));
            Set(run, "host", Activator.CreateInstance(T("CurtainCall.Campaign.RunState")));
            var state = Activator.CreateInstance(T("CurtainCall.Campaign.CampaignSnapshot"));
            Set(state, "Phase", Enum.Parse(T("CurtainCall.Campaign.CampaignPhase"), "RestTime")); Set(run, "state", state); Set(director, "run", run);
            Physics.SyncTransforms();
        }
        Component Actor(Vector3 offset, bool medium)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/Pursuit_Male_01.prefab");
            var go = Object.Instantiate(prefab, root.transform); go.transform.position = Origin + offset;
            var p = new GameObject("placement"); p.transform.SetParent(root.transform);
            var placement = p.AddComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitPlacement"));
            Set(placement, "Size", Enum.ToObject(placement.GetType().GetField("Size").FieldType, medium ? 1 : 0));
            if (medium) go.transform.localScale *= 1.16f;
            var actor = go.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitActor")); Call(actor, "Initialize", director, room, placement);
            return actor;
        }
        void Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root); nav.Remove(); if (data != null) Object.DestroyImmediate(data);
            if (pursuitRules != null && savedRules != null) JsonUtility.FromJsonOverwrite(savedRules, pursuitRules);
        }
        [UnityTest] public IEnumerator SawHitsInterruptClimbAndRespawnBothSizesAfterTenSeconds()
        {
            try {
                Setup(false);
                Set(pursuitRules, "SawCanKill", true); Set(pursuitRules, "SawKnockback", true);
                var small = Actor(new Vector3(0, 0, 1.3f), false); var medium = Actor(new Vector3(-3, 0, -2), true);
                var initial = small.transform.position; var initialMedium = medium.transform.position;
                Assert.AreEqual(3, Get(small, "Health")); Assert.AreEqual(5, Get(medium, "Health"));
                foreach (string pose in new[] { "Run", "CrouchWalk" }) {
                    Call(small, "SetMotion", Enum.Parse(T("CurtainCall.WaitingRoom.Pursuit.PursuitMotion"), pose), true);
                    Assert.IsTrue((bool)Call(small, "ReceiveSawHit", Vector3.back));
                    float until = Time.time + .7f;
                    while (Time.time < until) { Call(small, "TickCombat"); yield return null; }
                }
                Assert.AreEqual(1, Get(small, "Health")); Assert.Less(small.transform.position.z, initial.z - .2f);
                small.transform.position = initial;
                Call(small, "StartDirect", Origin + new Vector3(0, 3, 2.4f), true);
                float climbing = Time.time + (float)Get(small, "TraversalDuration") * .55f;
                while (Time.time < climbing) { Call(small, "TickDirect"); yield return null; }
                var hitPosition = small.transform.position;
                Assert.IsTrue((bool)Get(small, "IsClimbing")); Assert.Greater(hitPosition.y, 1);
                Assert.IsTrue((bool)Call(small, "ReceiveSawHit", Vector3.back));
                Assert.IsFalse((bool)Get(small, "IsClimbing")); Assert.AreEqual(hitPosition, small.transform.position, "A hit must not teleport to the ledge");
                Assert.AreEqual("Death", Get(small, "State").ToString());
                for (int i = 0; i < 5; i++) Assert.IsTrue((bool)Call(medium, "ReceiveSawHit", Vector3.back));
                Assert.IsTrue((bool)Get(medium, "IsDead")); Assert.IsFalse((bool)Call(medium, "ReceiveSawHit", Vector3.back));
                float died = Time.time;
                while (Time.time < died + 9.8f) { Call(small, "TickCombat"); Call(medium, "TickCombat"); yield return null; }
                Assert.IsTrue((bool)Get(small, "IsDead")); Assert.IsTrue((bool)Get(medium, "IsDead"));
                Assert.Less(small.transform.position.y, .15f, "Interrupted climbing must fall to the floor");
                while (Time.time < died + 10.2f) { Call(small, "TickCombat"); Call(medium, "TickCombat"); yield return null; }
                Assert.AreEqual(3, Get(small, "Health")); Assert.AreEqual(5, Get(medium, "Health"));
                Assert.Less(Vector3.Distance(initial, small.transform.position), .01f); Assert.Less(Vector3.Distance(initialMedium, medium.transform.position), .01f);
                Assert.AreEqual(1, Get(small, "SpawnRevision")); Assert.AreEqual("Display", Get(small, "State").ToString());
            } finally { Cleanup(); }
        }
        [UnityTest] public IEnumerator DisabledSawDeathAndKnockbackKeepClimbingAlive()
        {
            try {
                Setup(false); Set(pursuitRules, "SawCanKill", false); Set(pursuitRules, "SawKnockback", false);
                var actor = Actor(new Vector3(0, 0, 1.3f), false);
                Call(actor, "StartDirect", Origin + new Vector3(0, 3, 2.4f), true);
                var before = actor.transform.position;
                for (int i = 0; i < 8; i++) Assert.IsTrue((bool)Call(actor, "ReceiveSawHit", Vector3.back));
                Assert.AreEqual(1, Get(actor, "Health")); Assert.IsFalse((bool)Get(actor, "IsDead"));
                Assert.IsTrue((bool)Get(actor, "IsClimbing")); Assert.AreEqual(before, actor.transform.position);
                Assert.IsFalse((bool)Call(actor, "TickCombat"));
                yield return null; Call(actor, "TickDirect");
                Assert.Greater(actor.transform.position.y, before.y, "A non-knockback hit must not abort climbing.");
                Set(pursuitRules, "SawCanKill", true);
                before = actor.transform.position; Call(actor, "ReceiveSawHit", Vector3.right);
                Assert.IsTrue((bool)Get(actor, "IsDead")); Assert.AreEqual(before, actor.transform.position);
                Assert.AreEqual(Vector3.zero, Get(actor, "recoilVelocity"), "Death enabled must not implicitly enable knockback.");
            } finally { Cleanup(); }
        }

        [UnityTest] public IEnumerator EnvironmentalNoisePriorityAndUvRedirectRespectOcclusion()
        {
            try {
                Setup(false);
                Set(pursuitRules, "AwakeningSeconds", .1f);
                var actor = Actor(new Vector3(0, 0, -2), false); Set(actor, "activatedAt", Time.time);
                var agent = actor.GetComponent<NavMeshAgent>(); agent.enabled = true; agent.Warp(actor.transform.position);
                ((IList)Get(director, "actors")).Add(actor);
                yield return new WaitForSeconds(.2f);
                var kind = T("CurtainCall.WaitingRoom.Pursuit.PursuitNoiseKind");
                Call(director, "EmitNoise", Enum.Parse(kind, "SawGrab"), Origin + new Vector3(2, 1, -2), null);
                Assert.AreEqual(1, Get(actor, "noisePriority"));
                Call(director, "EmitNoise", Enum.Parse(kind, "SawFire"), Origin + new Vector3(-2, 1, -2), null);
                Assert.AreEqual(2, Get(actor, "noisePriority"));
                Assert.AreEqual(Origin + new Vector3(-2, 1, -2), Get(actor, "sensingPoint"), "Louder noise must supersede a quieter event in the same hearing cooldown.");
                var source = new GameObject("UV player"); source.transform.SetParent(root.transform); source.transform.position = Origin + new Vector3(4, 0, -2);
                var old = new GameObject("Previous chased player"); old.transform.SetParent(root.transform); old.transform.position = Origin + new Vector3(0, 0, -4);
                Set(actor, "target", old.transform);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform); wall.transform.position = Origin + new Vector3(2, 1, -2); wall.transform.localScale = new Vector3(.2f, 3, 3);
                Physics.SyncTransforms();
                Set(pursuitRules, "UvDistraction", true);
                Call(director, "IlluminatePursuers", source, source.transform.position + Vector3.up * 1.2f, Vector3.left, 12f, 50f);
                Assert.AreEqual(old, Get(actor, "ChasedPlayer"), "UV cannot redirect through a closed wall.");
                wall.SetActive(false); Physics.SyncTransforms();
                Call(director, "IlluminatePursuers", source, source.transform.position + Vector3.up * 1.2f, Vector3.right, 12f, 50f);
                Assert.AreEqual(old, Get(actor, "ChasedPlayer"), "Light facing away cannot distract.");
                Call(director, "IlluminatePursuers", source, source.transform.position + Vector3.up * 1.2f, Vector3.left, 12f, 50f);
                Assert.AreEqual(source, Get(actor, "ChasedPlayer"));
                var stage = Get(director, "CurrentStageRules"); Set(stage, "AlwaysNearest", true);
                ((IList)Get(director, "candidates")).Add(old); ((IList)Get(director, "candidates")).Add(source);
                Call(actor, "TickBrain", stage);
                Assert.AreEqual(source, Get(actor, "ChasedPlayer"), "Nearest-player mode must respect the UV focus window.");
                Assert.IsFalse((bool)Call(actor, "DistractWithUv", old), "A second source cannot bypass the cooldown.");
                yield return null;
            } finally { Cleanup(); }
        }

        [UnityTest] public IEnumerator ClosingDoorAndMovedPropPushPursuerOutWithoutSawKnockback()
        {
            try {
                Setup(false); Set(pursuitRules, "SawKnockback", false); Set(pursuitRules, "ObstructionSeparation", true);
                var actor = Actor(new Vector3(0, .04f, -3), false);
                var agent = actor.GetComponent<NavMeshAgent>(); agent.enabled = true; agent.Warp(actor.transform.position);
                var door = GameObject.CreatePrimitive(PrimitiveType.Cube); door.transform.SetParent(root.transform);
                door.name = "Closing shutter"; door.transform.position = Origin + new Vector3(0, 1, -3); door.transform.localScale = new Vector3(4, 2, .2f);
                Physics.SyncTransforms(); yield return null; yield return null;
                Assert.Greater((int)Get(actor, "SeparationCount"), 0); Assert.That(actor.transform.position.y, Is.EqualTo(.04f).Within(.15f));
                var probe = (CapsuleCollider)Get(actor, "separationBody");
                Assert.IsTrue((bool)T("CurtainCall.WaitingRoom.Pursuit.PursuitSeparation").GetMethod("Clear").Invoke(null, new object[] { probe, actor.transform.position, actor.transform }));
                Assert.IsFalse(probe.enabled, "The query probe must not become a gameplay collider.");
                door.SetActive(false);
                var prop = GameObject.CreatePrimitive(PrimitiveType.Cube); prop.transform.SetParent(root.transform);
                prop.name = "Moving trampoline frame"; prop.transform.position = actor.transform.position + Vector3.up; prop.transform.localScale = new Vector3(3, .3f, 3);
                int count = (int)Get(actor, "SeparationCount");
                yield return new WaitForSeconds(.1f);
                Assert.Greater((int)Get(actor, "SeparationCount"), count);
                Assert.IsTrue((bool)T("CurtainCall.WaitingRoom.Pursuit.PursuitSeparation").GetMethod("Clear").Invoke(null, new object[] { probe, actor.transform.position, actor.transform }));
                Assert.That(actor.transform.position.y, Is.EqualTo(.04f).Within(.15f));
            } finally { Cleanup(); }
        }

        [UnityTest] public IEnumerator MediumPursuerCrouchesThroughLowTunnelAndStandsOutside()
        {
            try {
                Setup(true); var actor = Actor(new Vector3(0, 0, -3), true);
                var agent = actor.GetComponent<NavMeshAgent>(); agent.enabled = true;
                Assert.IsTrue(agent.Warp(Origin + new Vector3(0, .04f, -3)));
                Assert.IsTrue(agent.SetDestination(Origin + new Vector3(0, .04f, 5)));
                bool crouched = false, walked = false; float until = Time.time + 12;
                while (Time.time < until && actor.transform.position.z < Origin.z + 4.5f) {
                    agent.speed = 3; Call(actor, "UpdateCrouch");
                    crouched |= (bool)Get(actor, "IsCrouched"); walked |= Get(actor, "State").ToString() == "CrouchWalk";
                    yield return null;
                }
                Assert.IsTrue(crouched); Assert.IsTrue(walked); Assert.GreaterOrEqual(actor.transform.position.z, Origin.z + 4.5f);
                Call(actor, "UpdateCrouch"); Assert.IsFalse((bool)Get(actor, "IsCrouched")); Assert.Greater(agent.height, 1.8f);
            } finally { Cleanup(); }
        }
    }
}
#endif
