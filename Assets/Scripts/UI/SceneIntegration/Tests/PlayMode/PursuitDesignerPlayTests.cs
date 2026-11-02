using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator PursuitDesigner_LiveMapUsesNoPreviewCameraAndTracksPlayerAcrossFloors()
        {
            yield return PursuitHost();
            var type = T("CurtainCall.WaitingRoom.Pursuit.Editor.PursuitDesignerWindow");
            bool existed = Resources.FindObjectsOfTypeAll(type).Length > 0;
            var window = (EditorWindow)type.GetMethod("Open").Invoke(null, null);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            object Read(string name) => type.GetField(name, flags).GetValue(window);
            void Write(string name, object value) => type.GetField(name, flags).SetValue(window, value);
            object Invoke(string name, params object[] args) => type.GetMethod(name, flags).Invoke(window, args);
            bool previousFollow = (bool)Read("liveFollowPlayer");
            int previousPlayer = (int)Read("followedPlayerId");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var oldPosition = player.transform.position;
            bool simulated = (bool)Get(mover, "Simulated");
            GameObject extra = null;
            try {
                mover.GetType().GetProperty("Simulated").SetValue(mover, false);
                Write("liveFollowPlayer", true); Write("followedPlayerId", 0);
                Invoke("TickLive"); window.Repaint(); yield return null; yield return null;
                Assert.Greater(((ICollection)Read("liveWalls")).Count, 0, "Show cached fixed-wall outlines without a scene render.");
                Assert.AreSame(player.transform, Invoke("LiveFollowTarget"));
                var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
                var rooms = ((IEnumerable)Field(director, "Rooms")).Cast<Component>().ToArray();
                foreach (string name in new[] { "Lobby", "MachineRoom" }) {
                    var room = rooms.First(r => r.name.Contains(name));
                    var box = (BoxCollider)Field(room, "Area");
                    float floor = (float)Invoke("FloorHeight", room);
                    var point = new Vector3(box.bounds.center.x, floor + .02f, box.bounds.center.z);
                    Call(mover, "SetStartPose", point, Quaternion.identity);
                    yield return null; Invoke("TickLive");
                    Assert.That(Vector3.Distance((Vector3)Read("viewCenter"), player.transform.position), Is.LessThan(.001f));
                    Assert.AreEqual(floor, (float)Read("viewFloor"));
                }
                Invoke("PreparePreview", new Rect(0, 0, 600, 600));
                Assert.IsTrue((Object)Read("previewCamera") == null);
                Assert.IsTrue((Object)Read("texture") == null);
                Assert.AreEqual(0, ((IDictionary)Read("previews")).Count);
                Assert.IsFalse(Resources.FindObjectsOfTypeAll<Camera>().Any(c => c.name == "__PursuitDesignerCamera"));
                window.Repaint(); yield return null; yield return null;
                var viewport = (Rect)Read("liveViewport"); Assert.Greater(viewport.width, 100);
                var beforePan = (Vector3)Read("viewCenter");
                window.SendEvent(new Event { type = EventType.MouseDown, button = 2, mousePosition = viewport.center });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 2, mousePosition = viewport.center + new Vector2(50, 20), delta = new Vector2(50, 20) });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 2, mousePosition = viewport.center + new Vector2(50, 20) });
                Assert.IsFalse((bool)Read("liveFollowPlayer"), "Manual drag switches to free mode.");
                var panned = (Vector3)Read("viewCenter"); Assert.Greater(Vector3.Distance(beforePan, panned), .1f);
                Invoke("TickLive"); Assert.AreEqual(panned, (Vector3)Read("viewCenter"));
                // Selecting a different observed transform changes only the editor's camera centre.
                extra = new GameObject("ObservedPlayerFixture"); extra.transform.position = oldPosition;
                var players = (IList)Read("livePlayers"); players.Add(extra.transform);
                Write("followedPlayerId", extra.transform.GetInstanceID()); Write("liveFollowPlayer", true);
                Invoke("UpdateLiveTracking"); Assert.AreEqual(extra.transform.position, (Vector3)Read("viewCenter"));
                players.Remove(extra.transform); Object.Destroy(extra); extra = null;
                Invoke("RefreshLivePlayers"); Invoke("UpdateLiveTracking");
                Assert.AreSame(player.transform, Invoke("LiveFollowTarget"), "Disconnected target returns to automatic local selection.");
                // Warm-cache sampling measures editor polling, not game FPS or GUI rendering.
                Write("nextLiveDiscovery", EditorApplication.timeSinceStartup + 600);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 100; i++) Invoke("TickLive");
                long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                Debug.Log($"Pursuit designer warm polling: {timer.Elapsed.TotalMilliseconds / 100:F3} ms/tick, {allocated / 100} bytes/tick (reflection included), no preview camera/RT.");
                Write("panelVisible", false); Write("nextRefresh", 0d);
                Invoke("Tick"); Assert.AreEqual(0d, Read("nextRefresh"), "Hidden tabs must not poll/repaint.");
            } finally {
                if (extra != null) Object.Destroy(extra);
                Call(mover, "SetStartPose", oldPosition, Quaternion.identity);
                mover.GetType().GetProperty("Simulated").SetValue(mover, simulated);
                Write("liveFollowPlayer", previousFollow); Write("followedPlayerId", previousPlayer); Write("panelVisible", true);
                Invoke("Cleanup");
                if (!existed) window.Close(); else window.Repaint();
            }
        }
    }
}
