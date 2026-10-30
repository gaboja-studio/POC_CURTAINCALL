using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [Serializable] sealed class FlowPoint { public float x, y, z; }
        [Serializable] sealed class FlowRow { public string id; public bool closed, opened; public float[] openedApproach; public FlowPoint[] closedWalk; }
        [Serializable] sealed class FlowReport { public FlowRow[] targets; }
        string flowHeld = null;
        void FlowKeys(params string[] names)
        {
            string signature = string.Join("+", names);
            if (signature == flowHeld) return;
            flowHeld = signature;
            var input = T("UnityEngine.InputSystem.InputSystem");
            cursorTestKeyboard ??= input.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) }).Invoke(null, new object[] { "Keyboard", null, null });
            var keys = Array.CreateInstance(T("UnityEngine.InputSystem.Key"), names.Length);
            for (int i = 0; i < names.Length; i++) keys.SetValue(Enum.Parse(T("UnityEngine.InputSystem.Key"), names[i]), i);
            var stateType = T("UnityEngine.InputSystem.LowLevel.KeyboardState");
            var state = Activator.CreateInstance(stateType, new object[] { keys });
            input.GetMethods().Single(m => m.Name == "QueueStateEvent" && m.IsGenericMethodDefinition).MakeGenericMethod(stateType).Invoke(null, new[] { cursorTestKeyboard, state, -1d });
        }
        IEnumerator FlowTap()
        { FlowKeys("F"); yield return new WaitForSecondsRealtime(.08f); FlowKeys(); yield return new WaitForSecondsRealtime(.16f); }
        static Vector3 FlowVector(FlowPoint value) => new(value.x, value.y, value.z);
        IEnumerator FlowWalk(Component player, Component mover, Vector3 destination)
        {
            float end = Time.realtimeSinceStartup + 60, lastProgress = Time.realtimeSinceStartup, best = float.MaxValue;
            while (Time.realtimeSinceStartup < end)
            {
                var delta = destination - player.transform.position; delta.y = 0;
                if (delta.magnitude < .2f && Mathf.Abs(player.transform.position.y - destination.y) < .6f) break;
                if (delta.magnitude < best - .08f) { best = delta.magnitude; lastProgress = Time.realtimeSinceStartup; }
                Assert.Less(Time.realtimeSinceStartup - lastProgress, 4f, "Input walk blocked at " + player.transform.position + " towards " + destination);
                var director = Find("CurtainCall.Cameras.CameraViewDirector");
                var basis = director != null && (bool)Get(director, "UsesMouseLook") ? (Vector3)Get(director, "ViewForward") : (Vector3)Get(mover, "CourseForward");
                basis.y = 0; basis.Normalize(); var right = Vector3.Cross(Vector3.up, basis); delta.Normalize();
                float forward = Vector3.Dot(delta, basis), side = Vector3.Dot(delta, right);
                var keys = new System.Collections.Generic.List<string>();
                if (forward > .3f) keys.Add("W"); else if (forward < -.3f) keys.Add("S");
                if (side > .3f) keys.Add("D"); else if (side < -.3f) keys.Add("A");
                if (best > 1f) keys.Add("LeftShift");
                FlowKeys(keys.ToArray()); yield return null;
            }
            FlowKeys(); yield return null;
            Assert.Less(Vector3.Distance(player.transform.position, destination), .8f, "Continuous input movement must reach the corner without repositioning");
        }
        [UnityTest]
        public IEnumerator M2_Flow_InputWalkWorkshopToRestroomBreach()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var audit = T("CurtainCall.WaitingRoom.Editor.StructureARouteAudit"); audit.GetMethod("BeginRuntime").Invoke(null, null);
            float auditUntil = Time.realtimeSinceStartup + 90;
            while ((string)audit.GetProperty("Status").GetValue(null) != "completed" && Time.realtimeSinceStartup < auditUntil) yield return null;
            Assert.AreEqual("completed", audit.GetProperty("Status").GetValue(null), "Temporary route audit must finish for this run");
            var report = JsonUtility.FromJson<FlowReport>(File.ReadAllText(".harness/local/stage8-connectivity.json"));
            var rows = report.targets;
            var tool = rows.Single(r => r.id == "spot:lobby-crowbar");
            var breach = rows.Single(r => r.id == "restroom-breach");
            Assert.IsTrue(tool.closed && breach.closed, "Daytime closed-puzzle navigation prerequisites");
            var toolRoute = tool.closedWalk.Select(FlowVector).ToArray();
            var wallRoute = breach.closedWalk.Select(FlowVector).ToArray();

            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"); var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            // One initial spawn alignment; every subsequent segment uses InputSystem -> movement -> CharacterController.
            Call(mover, "SetStartPose", toolRoute[0] + Vector3.up * .03f, Quaternion.identity);
            Assert.AreEqual(true, Get(mover, "Simulated")); FlowKeys(); yield return new WaitForSecondsRealtime(.3f);
            foreach (var corner in toolRoute.Skip(1)) yield return FlowWalk(player, mover, corner);
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance"); ulong me = (ulong)Get(carry, "LocalClientId");
            yield return FlowTap(); Assert.AreEqual("stake-puller", Get(Call(carry, "Selected", me), "ItemId"));
            foreach (var corner in toolRoute.Reverse().Skip(1)) yield return FlowWalk(player, mover, corner);
            foreach (var corner in wallRoute.Skip(1)) yield return FlowWalk(player, mover, corner);
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            for (int i = 0; i < 6; i++) yield return FlowTap();
            Assert.AreEqual(true, Call(state, "IsOpen", "restroom-breach")); Assert.AreEqual(6, Call(state, "Hits", "restroom-breach"));
            yield return FlowWalk(player, mover, new Vector3(16.05f, 0, 60.4f));
            CapturePlacement("structure-a-stage8-walk.png"); FlowKeys();
        }
        [UnityTest]
        public IEnumerator M2_Flow_FInputAndAuthorityRejectPartitionWithoutToolLoss()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            yield return PickPlaced("stake-puller", "S10_Workshop");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"); var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var at = new Vector3(1000, 1000, 1000); Call(mover, "SetStartPose", at + Vector3.back, Quaternion.identity);
            var target = ExplorationTestGate("stage8-partition-target", "Breakable", at);
            ExplorationSet(target, "toolId", "stake-puller"); target.gameObject.SetActive(true);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = at + new Vector3(0, 1, -.5f); wall.transform.localScale = new Vector3(4, 4, .1f);
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance"); var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance"); ulong me = (ulong)Get(carry, "LocalClientId");
            int uid = (int)Get(Call(carry, "Selected", me), "Uid");
            int basis = (int)T("CurtainCall.WaitingRoom.ItemUses").GetField("FoundUidBase").GetRawConstantValue() + (int)Field(State, "Generation") * 100;
            foreach (var offset in new[] { 91, 92 })
            {
                var item = Activator.CreateInstance(T("CurtainCall.WaitingRoom.PropItem"), basis + offset, "magician-hairpin", "fixture-hairpin", 5);
                Call(carry, "PlaceFound", item, at + (offset == 92 ? Vector3.down * 5 : Vector3.zero));
            }
            try
            {
                Physics.SyncTransforms(); yield return FlowTap();
                Call(carry, "RequestPickUp", basis + 91); Call(carry, "RequestPickUp", basis + 92);
                Assert.AreEqual(true, Call(carry, "Exists", basis + 91)); Assert.AreEqual(true, Call(carry, "Exists", basis + 92));
                Call(state, "RequestUse", "stage8-partition-target");
                Assert.AreEqual(0, Call(state, "Hits", "stage8-partition-target")); Assert.AreEqual(uid, Get(Call(carry, "Selected", me), "Uid"));
                wall.SetActive(false); Physics.SyncTransforms(); yield return new WaitForSecondsRealtime(.2f);
                Call(carry, "RequestPickUp", basis + 91); Assert.AreEqual("magician-hairpin", Get(Call(carry, "Selected", me), "ItemId"));
                Call(carry, "RequestSelectSlot", 0); yield return FlowTap();
                Assert.AreEqual(true, Get(target, "Opened")); Assert.AreEqual(false, Call(carry, "Exists", uid));
            }
            finally { FlowKeys(); Object.Destroy(target.gameObject); Object.Destroy(wall); }
        }
        [UnityTest]
        public IEnumerator M2_Flow_SceneReloadKeepsOpeningAndDoesNotReseedConsumedSpot()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            yield return PickPlaced("stake-puller", "S10_Workshop");
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance"); ulong me = (ulong)Get(carry, "LocalClientId");
            int uid = (int)Get(Call(carry, "Selected", me), "Uid");
            yield return UsePlaced("dormitory-box-1"); Assert.AreEqual(true, Get(Placed("dormitory-box-1"), "Opened"));
            int generation = (int)Field(State, "Generation");
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity", LoadSceneMode.Single);
            yield return new WaitForSecondsRealtime(.6f);
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            Assert.AreEqual(generation, Field(State, "Generation")); Assert.AreEqual(true, Call(state, "IsOpen", "structure-a:dormitory-box-1"));
            Assert.IsFalse(Placed("dormitory-box-1").GetComponent<BoxCollider>().enabled);
            Assert.AreEqual(false, Call(carry, "Exists", uid), "Same-run scene re-entry must not reissue the consumed found tool");
            Assert.AreEqual(1, Call(state, "Hits", "structure-a:dormitory-box-1"));
        }
        [UnityTest, Timeout(600000)]
        public IEnumerator M2_Flow_AllRoomsInputWalkWithOpenedPuzzleFixture()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var audit = T("CurtainCall.WaitingRoom.Editor.StructureARouteAudit"); audit.GetMethod("BeginRuntime").Invoke(null, null);
            float until = Time.realtimeSinceStartup + 90;
            while ((string)audit.GetProperty("Status").GetValue(null) != "completed" && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual("completed", audit.GetProperty("Status").GetValue(null));
            var report = JsonUtility.FromJson<FlowReport>(File.ReadAllText(".harness/local/stage8-connectivity.json"));
            Assert.IsTrue(report.targets.All(r => r.opened));
            var layout = GameObject.Find("WaitingRoom_LayoutA").transform;
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var book = state.GetType().GetField("book", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(state);
            var openIds = layout.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null && T("CurtainCall.WaitingRoom.IExplorationTarget").IsInstanceOfType(c))
                .Select(c => (string)Get(c, "TargetId")).Where(id => id.StartsWith("structure-a:") || id == "restroom-breach").ToArray();
            // This fixture validates physical traversal only. It is not an actual key/code/cooperative opening.
            Call(book, "Load", Array.Empty<string>(), Array.Empty<int>(), openIds);
            yield return new WaitForSecondsRealtime(1);
            var sources = new System.Collections.Generic.List<UnityEngine.AI.NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(layout, ~0, UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders, 0, new System.Collections.Generic.List<UnityEngine.AI.NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component is Collider c && (c.isTrigger || !c.enabled || !c.gameObject.activeInHierarchy));
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"); var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var capsule = player.GetComponent<CharacterController>(); var settings = UnityEngine.AI.NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = capsule.radius; settings.agentHeight = capsule.height; settings.agentClimb = capsule.stepOffset; settings.agentSlope = capsule.slopeLimit;
            settings.overrideVoxelSize = true; settings.voxelSize = .1f; settings.overrideTileSize = true; settings.tileSize = 256;
            var data = new UnityEngine.AI.NavMeshData(settings.agentTypeID) { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.AI.NavMeshDataInstance instance = default;
            int visited = 0;
            try
            {
                yield return UnityEngine.AI.NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, new Bounds(new Vector3(-10, -1, 80), new Vector3(145, 45, 140)));
                instance = UnityEngine.AI.NavMesh.AddNavMeshData(data);
                var pending = report.targets.ToList();
                while (pending.Count > 0)
                {
                    var row = pending.OrderBy(r => Vector3.Distance(player.transform.position, new Vector3(r.openedApproach[0], r.openedApproach[1], r.openedApproach[2]))).First();
                    var goal = new Vector3(row.openedApproach[0], row.openedApproach[1], row.openedApproach[2]);
                    Debug.Log("[ExplorationWalk] " + visited + "/" + report.targets.Length + " -> " + row.id);
                    Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(player.transform.position, out var origin, 1f, UnityEngine.AI.NavMesh.AllAreas));
                    var path = new UnityEngine.AI.NavMeshPath();
                    Assert.IsTrue(UnityEngine.AI.NavMesh.CalculatePath(origin.position, goal, UnityEngine.AI.NavMesh.AllAreas, path), row.id);
                    Assert.AreEqual(UnityEngine.AI.NavMeshPathStatus.PathComplete, path.status, row.id);
                    foreach (var corner in path.corners.Skip(1)) yield return FlowWalk(player, mover, corner);
                    Assert.Less(Vector3.Distance(player.transform.position, goal), .8f, row.id);
                    pending.Remove(row); visited++;
                }
                Assert.AreEqual(report.targets.Length, visited);
            }
            finally { FlowKeys(); if (instance.valid) instance.Remove(); Object.Destroy(data); }
        }
        [UnityTest]
        public IEnumerator M2_Flow_DoorBlockersSealDoorwayWidthAndReleaseOnSnapshot()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var ids = new[] { "stairs-door", "management-door", "machine-hidden-door", "escape-door" };
            foreach (var id in ids)
            {
                var door = Placed(id); var blocker = (BoxCollider)door.GetType().GetField("blocker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(door);
                Assert.IsTrue(blocker.enabled, id);
                bool alongZ = id == "stairs-door" || id == "management-door";
                var normal = alongZ ? Vector3.forward : Vector3.right; var across = alongZ ? Vector3.right : Vector3.forward;
                foreach (float offset in new[] { -.5f, 0, .5f })
                {
                    var origin = door.transform.position + Vector3.up + across * offset - normal * 1.2f;
                    Assert.IsTrue(blocker.Raycast(new Ray(origin, normal), out _, 2.4f), id + ": doorway edge " + offset);
                }
            }
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var book = state.GetType().GetField("book", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(state);
            Call(book, "Load", Array.Empty<string>(), Array.Empty<int>(), ids.Select(id => "structure-a:" + id).ToArray());
            yield return null; yield return null;
            foreach (var id in ids) Assert.IsFalse(Placed(id).GetComponent<BoxCollider>().enabled, id);
            // Snapshot presentation test, not an actual cooperative two-player opening.
        }
    }
}
