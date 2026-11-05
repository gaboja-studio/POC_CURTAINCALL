using System;
using System.Collections;
using System.Reflection;
using CurtainCall.HumanCannon;
using CurtainCall.Network.Session;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public class TightropeSwingSnapshotTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        GameObject root;
        TightropeTrapeze trapeze;
        Action<ulong, FastBufferReader> receive;
        FieldInfo version;
        Array poles;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Swing snapshot receiver"); root.SetActive(false);
            trapeze = root.AddComponent<TightropeTrapeze>();
            var run = root.AddComponent<TightropeRun>();
            Set(trapeze, "run", run); Set(trapeze, "round", run.Round);
            Set(trapeze, "session", root.AddComponent<NetworkSessionManager>());
            poles = (Array)Get(trapeze, "poles");
            var type = typeof(TightropeTrapeze).GetNestedType("PoleState", BindingFlags.NonPublic);
            for (int i = 0; i < poles.Length; i++)
            {
                var state = Activator.CreateInstance(type);
                var child = new GameObject("Pole physics"); child.SetActive(false); child.transform.SetParent(root.transform);
                Set(state, "Physics", child.AddComponent<HumanCannonPole>());
                poles.SetValue(state, i);
            }
            receive = (Action<ulong, FastBufferReader>)typeof(TightropeTrapeze).GetMethod("ReadState", Flags)
                .CreateDelegate(typeof(Action<ulong, FastBufferReader>), trapeze);
            version = typeof(TightropeTrapeze).GetField("version", Flags);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        FastBufferWriter Packet(uint sequence, bool duplicateFlight = false)
        {
            double now = Time.timeAsDouble;
            var writer = new FastBufferWriter(17 + 3 * 48 + 68, Allocator.Temp);
            writer.WriteValueSafe(0); writer.WriteValueSafe(sequence); writer.WriteValueSafe(now);
            for (int i = 0; i < 3; i++)
            {
                writer.WriteValueSafe(1u); writer.WriteValueSafe(i == 0 ? 7ul : ulong.MaxValue);
                writer.WriteValueSafe(ulong.MaxValue);
                writer.WriteValueSafe(15f); writer.WriteValueSafe(20f); writer.WriteValueSafe(.5f);
                writer.WriteValueSafe(now + 30); writer.WriteValueSafe(now);
            }
            writer.WriteValueSafe((byte)1); writer.WriteValueSafe(duplicateFlight ? 7ul : 9ul);
            writer.WriteValueSafe(1u); writer.WriteValueSafe(0);
            writer.WriteValueSafe(Vector3.zero); writer.WriteValueSafe(Vector3.up * 5); writer.WriteValueSafe(Vector3.forward * 3);
            writer.WriteValueSafe(now); writer.WriteValueSafe(now + 8);
            return writer;
        }

        [Test]
        public void ReusedStagingKeepsMalformedAndOldPacketsAtomic()
        {
            void Deliver(uint sequence, bool invalid = false)
            {
                using var writer = Packet(sequence, invalid);
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                receive(NetworkManager.ServerClientId, reader);
            }
            Deliver(1);
            var flights = (IDictionary)Get(trapeze, "flights");
            Assert.That(flights.Contains(9ul), Is.True);
            Assert.That(Get(poles.GetValue(0), "Upper"), Is.EqualTo(7ul));
            Deliver(2, true);
            Assert.That(Get(trapeze, "version"), Is.EqualTo(1u));
            Assert.That(flights.Contains(9ul), Is.True);
            Deliver(3);
            Assert.That(Get(trapeze, "version"), Is.EqualTo(3u));
            Deliver(2);
            Assert.That(Get(trapeze, "version"), Is.EqualTo(3u));
        }

        [Test]
        public void RopeSwingSmoothsRegularSnapshotsButSnapsOnRelease()
        {
            var swing = root.AddComponent<TightropeSwingPole>();
            Set(swing, "course", root.GetComponent<TightropeCourse>());
            Set(swing, "run", Get(trapeze, "run")); Set(swing, "round", 0);
            Set(swing, "session", Get(trapeze, "session"));
            Set(swing, "physicalPole", Get(poles.GetValue(0), "Physics"));
            var read = (Action<ulong, FastBufferReader>)typeof(TightropeSwingPole).GetMethod("ReadState", Flags)
                .CreateDelegate(typeof(Action<ulong, FastBufferReader>), swing);
            float Display() => (float)typeof(TightropeSwingPole).GetProperty("OwnAngle", Flags).GetValue(swing);
            void Deliver(int sequence, float angle, byte mode)
            {
                using var writer = new FastBufferWriter(54, Allocator.Temp);
                writer.WriteValueSafe(0); writer.WriteValueSafe(sequence); writer.WriteValueSafe(mode);
                writer.WriteValueSafe(7ul); writer.WriteValueSafe(0);
                writer.WriteValueSafe(Time.timeAsDouble); writer.WriteValueSafe(Time.timeAsDouble);
                writer.WriteValueSafe(angle); writer.WriteValueSafe(0f); writer.WriteValueSafe(0f);
                writer.WriteValueSafe(angle); writer.WriteValueSafe(mode == 2);
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                read(NetworkManager.ServerClientId, reader);
            }
            Deliver(1, 10, 1);
            Assert.That(Display(), Is.EqualTo(10).Within(.001f));
            Deliver(2, 20, 1);
            Assert.That(Display(), Is.EqualTo(10).Within(.001f));
            Assert.That(Get(swing, "angle"), Is.EqualTo(20f));
            Deliver(3, 30, 2);
            Assert.That(Display(), Is.EqualTo(30).Within(.001f));
        }

        [Test]
        public void RepeatedOccupiedSnapshotsAllocateNoManagedMemoryAfterWarmup()
        {
            using var writer = Packet(1);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            for (int i = 0; i < 8; i++) { version.SetValue(trapeze, 0u); reader.Seek(0); receive(0, reader); }
            long allocated = 0;
            for (int i = 0; i < 256; i++)
            {
                version.SetValue(trapeze, 0u); reader.Seek(0);
                long before = GC.GetAllocatedBytesForCurrentThread();
                receive(NetworkManager.ServerClientId, reader);
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }
            Assert.That(Get(trapeze, "version"), Is.EqualTo(1u), "Measure accepted packets, not an early return.");
            Assert.That(allocated, Is.Zero, "Includes occupied pole and in-flight actor snapshot processing.");
        }

        static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    }
}
