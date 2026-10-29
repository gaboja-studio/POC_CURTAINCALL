using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Collections;
using Unity.Netcode;
using Object = UnityEngine.Object;

namespace CurtainCall.Telemetry.Pipeline.EditorTests
{
    public sealed class NetworkRelaySchedulingTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        string directory;
        GameObject owner;
        object adapter;
        Type adapterType;
        CancellationTokenSource lifetime;
        DurableOutbox queue;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "curtaincall-relay-scheduling", Guid.NewGuid().ToString("N"));
            queue = new DurableOutbox(directory, Guid.NewGuid().ToString("D"));
            queue.Append(new LogEvent { sequence = 1, occurredAt = "2026-10-29T00:00:00Z", environment = "TEST",
                gameVersion = "0.1.0", playerId = "test-client", playerNumber = 1, networkClientId = "1",
                sceneType = "TITLE", eventType = "GAME_STARTED", authority = "LOCAL" });
            queue.Seal("CHECKPOINT");
            // 비활성 객체는 Awake/실제 업로드를 시작하지 않는다. 중계 스케줄러만 독립 검증한다.
            owner = new GameObject("Isolated relay scheduler"); owner.SetActive(false);
            var runtime = owner.AddComponent<TelemetryRuntime>();
            typeof(TelemetryRuntime).GetField("outbox", Private).SetValue(runtime, queue);
            adapterType = typeof(TelemetryRuntime).Assembly.GetType("CurtainCall.Telemetry.Pipeline.NetworkLogAdapter", true);
            adapter = Activator.CreateInstance(adapterType, new object[] { runtime });
            lifetime = new CancellationTokenSource(); Put("relayLifetime", lifetime);
        }
        [TearDown] public void TearDown()
        {
            if (adapter != null) ((IDisposable)adapter).Dispose();
            Object.DestroyImmediate(owner);
            string boundary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "curtaincall-relay-scheduling")) + Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(directory).StartsWith(boundary, StringComparison.OrdinalIgnoreCase), Is.True);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        object Field(string name) => adapterType.GetField(name, Private).GetValue(adapter);
        void Put(string name, object value) => adapterType.GetField(name, Private).SetValue(adapter, value);
        void Tick() => adapterType.GetMethod("TickRelay", Private).Invoke(adapter, null);
        IEnumerator Prepare()
        {
            Tick();
            var work = (Task<DurableOutbox.RelayPreparation>)Field("preparingRelay");
            Assert.That(work, Is.Not.Null, "프레임에서는 파일 탐색 대신 작업 스레드 요청만 만든다.");
            while (!work.IsCompleted) yield return null;
            Tick();
            Assert.That(Field("relay"), Is.Not.Null);
        }

        [UnityTest] public IEnumerator DurableReceiptAddsSpacingBeforeNextBatchInsteadOfDrainingEveryFrame()
        {
            yield return Prepare();
            Put("acknowledgingRelay", Task.FromResult(true));
            Tick();
            Assert.That(Field("relay"), Is.Null);
            Assert.That((float)Field("nextRelay"), Is.GreaterThan(Time.unscaledTime));
            Tick();
            Assert.That(Field("preparingRelay"), Is.Null, "수신 확인 다음 프레임에 다음 파일을 연속 처리하지 않는다.");
        }

        [UnityTest] public IEnumerator RetriesUsePreparedBytesAndDisconnectInvalidatesOldPreparation()
        {
            yield return Prepare();
            var prepared = (DurableOutbox.RelayPreparation)Field("relay");
            File.WriteAllText(prepared.Path, "changed after preparation");
            Put("nextRelay", 0f); Tick();
            Assert.That(Field("relay"), Is.SameAs(prepared), "재시도에서는 원본 바이트를 디스크에서 다시 읽지 않는다.");
            Assert.That((float)Field("nextRelay"), Is.GreaterThan(Time.unscaledTime));
            var token = lifetime.Token;
            ((IDisposable)adapter).Dispose();
            Assert.That(token.IsCancellationRequested, Is.True);
            Assert.That(Field("relay"), Is.Null); Assert.That(Field("preparingRelay"), Is.Null);
            Assert.That(Field("acknowledgingRelay"), Is.Null);
            Assert.That(File.Exists(prepared.Path), Is.True, "취소·연결 종료는 대기 파일을 지우지 않는다.");
        }

        [UnityTest] public IEnumerator OnlyExactServerReceiptStartsAsyncDeletionAndDuplicateIsIgnored()
        {
            yield return Prepare();
            var prepared = (DurableOutbox.RelayPreparation)Field("relay");
            string receipt = prepared.BatchId + ":" + prepared.Hash + ":" + prepared.EventCount;
            void Receive(ulong sender, string value)
            {
                using var writer = new FastBufferWriter(512, Allocator.Temp);
                writer.WriteValueSafe(value);
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                adapterType.GetMethod("ReadReceipt", Private).Invoke(adapter, new object[] { sender, reader });
            }
            Receive(99UL, receipt); Assert.That(Field("acknowledgingRelay"), Is.Null);
            Receive(NetworkManager.ServerClientId, receipt + "wrong"); Assert.That(Field("acknowledgingRelay"), Is.Null);
            Assert.That(File.Exists(prepared.Path), Is.True);
            Receive(NetworkManager.ServerClientId, receipt);
            var work = (Task<bool>)Field("acknowledgingRelay"); Assert.That(work, Is.Not.Null);
            Receive(NetworkManager.ServerClientId, receipt);
            Assert.That(Field("acknowledgingRelay"), Is.SameAs(work));
            while (!work.IsCompleted) yield return null;
            Tick();
            Assert.That(work.Result, Is.True); Assert.That(File.Exists(prepared.Path), Is.False);
            Assert.That((float)Field("nextRelay"), Is.GreaterThan(Time.unscaledTime));
        }
    }
}
