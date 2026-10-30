#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 2인 확인용 MPPM 복제본 조종기(에디터 전용, 2026-10-30). EditorPrefs 키를 켠 때만 MPPM 복제본에서 동작한다(평소 플레이에는 없다).
    /// 폴더의 command.json을 읽어 실제 입력 경로로 행동하고, state.json에 이 화면에서 보이는 상태를 적는다. 판정은 기존 게임 코드가 한다.
    /// </summary>
    [DefaultExecutionOrder(-90)] // 입력 읽기(-100) 뒤에 덮어쓴다
    public sealed class WaitingRoomMppmReview : MonoBehaviour
    {
        public const string Key = "CurtainCall.WaitingRoom.MppmReview";
        [Serializable] class Command { public int id; public string action = ""; public Vector3 at; public float yaw; public int uid; public float seconds = 1; }
        [Serializable] class State
        {
            public int command; public string error = "", connection = "", scene = "", phase = "", guide = "", held = "";
            public int day, remoteUvLit; public bool cannonUnlocked, mapCannonActive, practiceCannonActive, uvOn, uvMarkLit, localReady, scheduleConfirmed, nightCanEnd;
            public Vector3 position; public float time;
        }

        static readonly PropertyInfo current = typeof(PlayerInputReader).GetProperty("Current");
        string folder; int lastId; Command active; double until, nextRead, nextWrite; string error = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string target = EditorPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(target)) return;
            var type = Type.GetType("Unity.Multiplayer.PlayMode.Editor.VirtualProjectsEditor, UnityEditor.MultiplayerModule");
            bool clone = type != null && (bool)type.GetProperty("IsClone", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            if (!clone) return;
            Application.runInBackground = true;
            var go = new GameObject("WaitingRoomMppmReview"); DontDestroyOnLoad(go);
            go.AddComponent<WaitingRoomMppmReview>().folder = target;
            Directory.CreateDirectory(target);
        }

        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextRead) { nextRead = now + .1; ReadCommand(); }
            var player = NetworkPlayer.Local;
            if (player != null && player.TryGetComponent(out PlayerInputReader reader) && active != null && now < until)
            {
                var input = reader.Current;
                if (active.action == "uv") input.UvLightHeld = true;
                current.SetValue(reader, input);
            }
            if (now >= nextWrite) { nextWrite = now + .25; WriteState(); }
        }

        void ReadCommand()
        {
            string path = Path.Combine(folder, "command.json");
            if (!File.Exists(path)) return;
            try
            {
                var command = JsonUtility.FromJson<Command>(File.ReadAllText(path));
                if (command == null || command.id <= lastId) return;
                lastId = command.id; active = command; until = Time.realtimeSinceStartupAsDouble + Mathf.Clamp(command.seconds, 0, 120);
                Apply(command);
            }
            catch (IOException) { }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
        }

        void Apply(Command c)
        {
            var session = NetworkSessionManager.EnsureExists();
            var player = NetworkPlayer.Local;
            switch (c.action)
            {
                case "join": _ = session.JoinLanAsync("127.0.0.1"); break;
                case "pose":
                    player.GetComponent<PlayerMover>().SetStartPose(c.at, Quaternion.Euler(0, c.yaw, 0));
                    var director = CurtainCall.Cameras.CameraViewDirector.Current;
                    if (director != null && director.UsesMouseLook) director.SetLookRotation(Quaternion.Euler(0, c.yaw, 0));
                    break;
                case "pickup": PropCarry.Instance.RequestPickUp(c.uid); break;
                case "interact": ShopReach.Pump(); ShopReach.Focused?.Interact(); break;
                case "bed": NightBed.AllBeds().OrderBy(b => (b.transform.position - player.transform.position).sqrMagnitude).First().Interact(); break;
                case "uv": break; // Update에서 seconds 동안 우클릭 유지
                default: throw new ArgumentException("unknown action " + c.action);
            }
        }

        void WriteState()
        {
            var s = new State { command = lastId, error = error, time = Time.realtimeSinceStartup };
            try
            {
                var session = NetworkSessionManager.EnsureExists();
                s.connection = session.ConnectionState.ToString();
                s.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                var run = CampaignRun.Instance;
                if (run != null) { s.phase = run.State.Phase.ToString(); s.day = run.State.Day; s.cannonUnlocked = run.State.CannonUnlocked; s.localReady = run.IsLocalReady; }
                var flashlight = FindFirstObjectByType<UvFlashlight>();
                if (flashlight != null) { s.uvOn = flashlight.IsOn; s.remoteUvLit = flashlight.RemoteLitCount; }
                s.uvMarkLit = UvMark.All.Any(m => m != null && m.Lit);
                s.guide = WaitingRoomGuidePanel.CurrentTitle;
                var set = FindFirstObjectByType<PerformancePropDiscovery>(FindObjectsInactive.Include);
                if (set != null)
                {
                    var so = typeof(PerformancePropDiscovery).GetField("mapProps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(set) as GameObject[];
                    var po = typeof(PerformancePropDiscovery).GetField("practiceProps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(set) as GameObject[];
                    s.mapCannonActive = so != null && so.Any(g => g != null && g.activeSelf);
                    s.practiceCannonActive = po != null && po.Any(g => g != null && g.activeSelf);
                }
                var board = ScheduleBoard.Instance;
                if (board != null) { s.scheduleConfirmed = board.Book.IsConfirmedTonight; s.nightCanEnd = board.NightCanEnd(); }
                var player = NetworkPlayer.Local;
                if (player != null)
                {
                    s.position = player.transform.position;
                    if (PropCarry.Instance != null) s.held = string.Join(",", PropCarry.Instance.Held(player.OwnerClientId).Select(i => i.ItemId));
                }
            }
            catch (Exception e) { s.error = e.GetType().Name + ": " + e.Message; }
            try { File.WriteAllText(Path.Combine(folder, "state.json"), JsonUtility.ToJson(s, true)); } catch (IOException) { }
        }
    }
}
#endif
