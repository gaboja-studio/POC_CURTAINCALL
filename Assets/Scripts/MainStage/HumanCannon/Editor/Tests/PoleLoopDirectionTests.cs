using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class PoleLoopDirectionTests
    {
        GameObject root;
        HumanCannonPoleLoop loop;
        Transform carriage;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Conveyor direction fixture");
            root.SetActive(false);
            root.transform.SetPositionAndRotation(new Vector3(51, -17, -32), Quaternion.Euler(0, 23, 0));
            carriage = new GameObject("Carriage").transform;
            carriage.SetParent(root.transform, false);
            loop = root.AddComponent<HumanCannonPoleLoop>();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [TestCase(0f), TestCase(1f / 6), TestCase(.5f), TestCase(5f / 6)]
        [Category("CommonProgressionRegression"), Category("EscapeRemainingAudit")]
        public void RecycleBoundaryUsesPhaseAndFirstCrossingOnly(float phase)
        {
            loop.Configure(carriage, Vector3.zero, 100, 8, 5, phase);
            Assert.That(loop.TryGetRecycleBoundary(0, 1000, out _), Is.False, "Closed rails have no teleport seam.");
            var settings = new UnityEditor.SerializedObject(loop);
            settings.FindProperty("wallFed").boolValue = true;
            settings.FindProperty("exitStraightLength").floatValue = 52;
            settings.FindProperty("unloadingDistance").floatValue = 14;
            settings.ApplyModifiedPropertiesWithoutUndo();
            double period = loop.Length / 5d, boundary = period * (1d - phase);
            Assert.That(loop.TryGetRecycleBoundary(0, boundary - .01, out _), Is.False);
            Assert.That(loop.TryGetRecycleBoundary(boundary - .01, boundary + period * 3, out var terminal), Is.True);
            Assert.That(terminal, Is.InRange(boundary - .001, boundary));
            foreach (var pair in new[] { (boundary, boundary), (boundary, 0d), (-1d, boundary), (double.NaN, boundary), (0d, double.PositiveInfinity) })
                Assert.That(loop.TryGetRecycleBoundary(pair.Item1, pair.Item2, out _), Is.False);
            settings.FindProperty("metersPerSecond").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(loop.TryGetRecycleBoundary(0, 1000, out _), Is.False);
        }

        [TestCase(.02d, false), TestCase(.5d, false), TestCase(40d, false)]
        [TestCase(.02d, true), TestCase(.5d, true), TestCase(40d, true)]
        [Category("CommonProgressionRegression"), Category("EscapeRemainingAudit")]
        public void OccupiedWallFedRailCannotTeleportClientRidersAcrossItsRecycleSeam(double advance, bool authority)
        {
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var previous = HumanCannonHazardCourse.Current;
            var clockRoot = new GameObject("Offline conveyor clock"); clockRoot.SetActive(false); clockRoot.transform.SetParent(root.transform);
            var clock = clockRoot.AddComponent<HumanCannonHazardCourse>();
            var systemRoot = new GameObject("Offline passenger receiver"); systemRoot.SetActive(false); systemRoot.transform.SetParent(root.transform);
            var system = systemRoot.AddComponent<HumanCannonPoleSystem>();
            var all = (System.Collections.Generic.List<CurtainCall.Network.PlayerSync.NetworkPlayer>)typeof(CurtainCall.Network.PlayerSync.NetworkPlayer)
                .GetField("all", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            var saved = all.ToArray(); all.Clear();
            var actors = new System.Collections.Generic.List<CurtainCall.Network.PlayerSync.NetworkPlayer>();
            try
            {
                foreach (ulong id in new ulong[] { 7, 8 })
                {
                    var actorRoot = new GameObject("Conveyor passenger " + id); actorRoot.SetActive(false); actorRoot.transform.SetParent(root.transform);
                    var actor = actorRoot.AddComponent<CurtainCall.Network.PlayerSync.NetworkPlayer>();
                    typeof(Unity.Netcode.NetworkBehaviour).GetProperty("OwnerClientId").SetValue(actor, id);
                    typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsSpawned").SetValue(actor, true);
                    actors.Add(actor); all.Add(actor);
                }
                if (authority)
                {
                    var session = systemRoot.AddComponent<CurtainCall.Network.Session.NetworkSessionManager>();
                    typeof(CurtainCall.Network.Session.NetworkSessionManager).GetProperty("ConnectionState").SetValue(session,
                        CurtainCall.Network.Session.SessionConnectionState.Host);
                    typeof(HumanCannonPoleSystem).GetField("session", fields).SetValue(system, session);
                }
                typeof(HumanCannonHazardCourse).GetProperty("Current").SetValue(null, clock);
                typeof(HumanCannonHazardCourse).GetField("synchronized", fields).SetValue(clock, true);
                loop.Configure(carriage, Vector3.zero, 100, 8, 5);
                var config = new UnityEditor.SerializedObject(loop);
                config.FindProperty("wallFed").boolValue = true;
                config.FindProperty("exitStraightLength").floatValue = 52;
                config.FindProperty("unloadingDistance").floatValue = 14;
                config.ApplyModifiedPropertiesWithoutUndo(); root.SetActive(true);
                var pole = carriage.gameObject.AddComponent<HumanCannonPole>();
                typeof(HumanCannonPoleSystem).GetField("poles", fields).SetValue(system, new[] { pole });
                var type = typeof(HumanCannonPoleSystem).GetNestedType("Runtime", System.Reflection.BindingFlags.NonPublic);
                var runtime = System.Activator.CreateInstance(type, new object[] { 2 });
                var states = System.Array.CreateInstance(type, 1); states.SetValue(runtime, 0);
                typeof(HumanCannonPoleSystem).GetField("states", fields).SetValue(system, states);
                Assert.That(system.State(0).TryGrab(7, 0, system.Clock, 1000), Is.True);
                Assert.That(system.State(0).TryGrab(8, 1, system.Clock, 1000), Is.True);
                void Sample(double elapsed)
                {
                    typeof(HumanCannonHazardCourse).GetField("epoch", fields).SetValue(clock, clock.Clock - elapsed);
                    typeof(HumanCannonPoleSystem).GetMethod("Update", fields).Invoke(system, null);
                    loop.RefreshPose();
                }
                double beforeTime = loop.Length / 5d - .01d;
                Sample(beforeTime);
                var top = system.HangingPosition(0, 7); var bottom = system.HangingPosition(0, 8);
                Sample(beforeTime + advance);
                if (authority)
                {
                    Assert.That(system.OccupantCount(0), Is.Zero, "Both riders must leave before the carriage is recycled.");
                    Assert.That(Vector3.Distance(top, (Vector3)type.GetField("ReleasePosition").GetValue(runtime)), Is.LessThan(.1f));
                    Assert.That((Vector3)type.GetField("ReleaseVelocity").GetValue(runtime), Is.EqualTo(Vector3.zero));
                }
                else
                {
                    Assert.That(Vector3.Distance(top, system.HangingPosition(0, 7)), Is.LessThan(.1f),
                        "While waiting for the host release, the local rider must remain at the terminal, not teleport into the supply wall.");
                    Assert.That(Vector3.Distance(bottom, system.HangingPosition(0, 8)), Is.LessThan(.1f));
                    typeof(HumanCannonPoleSystem).GetMethod("LateUpdate", fields).Invoke(system, null);
                    Assert.That(Vector3.Distance(top, actors[0].transform.position), Is.LessThan(.1f), "Remote presentation must also hold the terminal pose.");
                    Assert.That(system.State(0).TryRelease(7, system.State(0).Revision, system.Clock, .1f), Is.True);
                    Assert.That(system.State(0).TryGrab(7, system.State(0).Revision, system.Clock, 1000, seat: 0), Is.True);
                    var rider = ((System.Array)type.GetField("Riders").GetValue(runtime)).GetValue(0);
                    rider.GetType().GetField("Launch").SetValue(rider, 123u);
                    Assert.That(Vector3.Distance(system.HangingPosition(0, 7), pole.HangingPosition(0, 0)), Is.LessThan(.001f),
                        "The same actor's new flight must not inherit a prior lap's frozen pose.");
                }
            }
            finally
            {
                foreach (var actor in actors) typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsSpawned").SetValue(actor, false);
                all.Clear(); all.AddRange(saved);
                typeof(HumanCannonHazardCourse).GetProperty("Current").SetValue(null, previous);
            }
        }

        [TestCase(.1f)] [TestCase(.3f)] [TestCase(.6f)] [TestCase(.9f)]
        public void Reverse_PreservesInitialRailPositionAndReversesFacingAndMomentum(float phase)
        {
            loop.Configure(carriage, Vector3.zero, 34, 16, 5, phase);
            loop.EvaluatePose(0, out var position, out var rotation, out var velocity, out var angular);
            loop.Configure(carriage, Vector3.zero, 34, 16, 5, phase, 0, true);
            loop.EvaluatePose(0, out var reversed, out var facing, out var reverseVelocity, out var reverseAngular);
            Assert.That(Vector3.Distance(position, reversed), Is.LessThan(.001f));
            Assert.That(Vector3.Dot(rotation * Vector3.forward, facing * Vector3.forward), Is.EqualTo(-1).Within(.001f));
            Assert.That((velocity + reverseVelocity).magnitude, Is.LessThan(.001f));
            Assert.That((angular + reverseAngular).magnitude, Is.LessThan(.001f));
        }

        [Test]
        public void WallFedRailFacesCannonAcrossBendAndUnloadsBeforeRecycling()
        {
            loop.Configure(carriage, Vector3.zero, 10, 2, 1);
            var settings = new UnityEditor.SerializedObject(loop);
            settings.FindProperty("wallFed").boolValue = true;
            settings.FindProperty("exitStraightLength").floatValue = 8;
            settings.FindProperty("unloadingDistance").floatValue = 3;
            settings.ApplyModifiedPropertiesWithoutUndo();

            double lastStraight = 10d + Mathf.PI;
            loop.EvaluatePose(lastStraight, out var corner, out var facing, out var velocity, out var angular);
            Assert.That(Vector3.Distance(corner, root.transform.TransformPoint(new Vector3(-12, 0, -2))), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(facing, root.transform.rotation), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(velocity, -root.transform.forward), Is.LessThan(.001f));
            Assert.That(angular, Is.EqualTo(Vector3.zero));
            Assert.That(loop.AtUnloadingBay(lastStraight + 4), Is.False);
            Assert.That(loop.AtUnloadingBay(lastStraight + 6), Is.True);
            loop.EvaluatePose(loop.Length, out var supply, out _, out _, out _);
            Assert.That(Vector3.Distance(supply, root.transform.position), Is.LessThan(.001f));
            Assert.That(loop.AtUnloadingBay(loop.Length), Is.False);
        }

        [TestCase(3d)] [TestCase(9d)] [TestCase(19d)] [TestCase(31d)]
        public void Reverse_ReleaseVelocityMatchesActualRailMotionAndClosesSeam(double time)
        {
            loop.Configure(carriage, Vector3.zero, 34, 16, 5, 0, 0, true);
            const double dt = .01d;
            loop.EvaluatePose(time, out var p, out var r, out var v, out var w);
            loop.EvaluatePose(time + dt, out var next, out var nextR, out _, out _);
            Vector3 seat = new Vector3(1.2f, -1.5f, .4f);
            Vector3 pointVelocity = v + Vector3.Cross(w, r * seat);
            Assert.That(((next + nextR * seat - p - r * seat) / (float)dt - pointVelocity).magnitude, Is.LessThan(.025f));
            loop.EvaluatePose(time + loop.Length / 5d, out var wrapped, out var wrappedR, out var wrappedV, out _);
            Assert.That(Vector3.Distance(p, wrapped), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(r, wrappedR), Is.LessThan(.05f));
            Assert.That(Vector3.Distance(v, wrappedV), Is.LessThan(.001f));
        }
    }
}
