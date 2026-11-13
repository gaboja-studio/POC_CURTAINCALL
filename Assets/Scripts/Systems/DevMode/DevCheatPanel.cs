using System.Collections.Generic;
using CurtainCall.Cameras;
using CurtainCall.Campaign;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Dev
{
    /// <summary>
    /// Dev 모드 테스트 치트 창(F9, Task-089). 공연·탈출로에서 특정 지점으로 바로 이동하고, 추락 무시·되살리기·재시작을 쓴다.
    /// 씬 배치 불필요(<see cref="DevModeBadge"/>가 만든다). 창이 열려 있는 동안은 게임 입력을 막고 커서를 푼다.
    /// </summary>
    public sealed partial class DevCheatPanel : MonoBehaviour
    {
        public const string ShortcutLabel = "F9";
        const float RefreshInterval = .5f;
        const float NudgeForward = 3f;
        const float NudgeUp = 2f;

        public static DevCheatPanel Instance { get; private set; }
        public bool IsOpen { get; private set; }
        public string Status { get; private set; } = "";

        readonly List<DevCheats.TeleportPoint> points = new();

        UIInputBlocker input;
        float nextRefresh;
        Vector2 scroll;
        GUIStyle label, button, heading, toggle;

        public static DevCheatPanel EnsureExists()
        {
            if (Instance != null) return Instance;
            if (!DevMode.Enabled) return null;
            var go = new GameObject(nameof(DevCheatPanel));
            DontDestroyOnLoad(go);
            return go.AddComponent<DevCheatPanel>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            input = gameObject.AddComponent<UIInputBlocker>();
            input.BlockWhileActive = false;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void LateUpdate()
        {
            if (DevMode.Enabled) DevCheats.TrackCheckpoints();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (!DevMode.Enabled)
            {
                if (IsOpen) { IsOpen = false; input.SetOpen(false); }
                return;
            }
            if (keyboard == null) return;
            if (keyboard.f9Key.wasPressedThisFrame) Toggle();
        }

        public void Toggle()
        {
            if (!DevMode.Enabled) return;
            IsOpen = !IsOpen;
            input.SetOpen(IsOpen);
            if (IsOpen) scroll = Vector2.zero;
            nextRefresh = 0f;
        }

        void Refresh()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshInterval;
            DevCheats.CollectTeleportPoints(NetworkPlayer.Local, points);

        }

        void Report(bool done, string success, string failure) => Status = done ? success : failure;

        public bool TryLaunchPerformance(string trick, int difficulty)
        {
            var launcher = DevStartLauncher.EnsureExists();
            bool done = launcher != null && launcher.TryLaunch(new RunStartPoint(1, RunStartPhase.Performance, 0, trick, difficulty));
            Status = launcher != null ? launcher.Status : "Dev 모드를 켜 주세요.";
            if (done && IsOpen) Toggle(); // The launcher owns the input lock before releasing this panel's lock.
            return done;
        }

        void OnGUI()
        {
            if (!DevMode.Enabled || !IsOpen) return;
            if (Event.current.type == EventType.Layout) Refresh();
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            button ??= new GUIStyle(GUI.skin.button) { fontSize = 15, fixedHeight = 32, wordWrap = true };
            toggle ??= new GUIStyle(GUI.skin.toggle) { fontSize = 15, wordWrap = true };
            heading ??= new GUIStyle(label) { fontSize = 19, fontStyle = FontStyle.Bold };
            float width = Mathf.Min(380, Screen.width - 20), height = Mathf.Min(620, Screen.height - 60);
            var bounds = new Rect(10, 44, width, height);
            var color = GUI.color;
            GUI.color = new Color(.14f, .12f, .20f, 1);
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = color;
            GUILayout.BeginArea(bounds, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.BeginVertical(GUILayout.Width(width - 28));
            GUILayout.Label($"DEV · 테스트 치트 [{ShortcutLabel}]", heading);

            var local = NetworkPlayer.Local;
            var session = NetworkSessionManager.Instance;
            bool host = session != null && session.IsHost;
            if (local == null || !local.IsSpawned) GUILayout.Label("내 캐릭터가 아직 없습니다. 대기실·공연·탈출로에 들어간 뒤 사용하세요.", label);
            else GUILayout.Label($"상태: {local.State} · 자리 {local.Slot + 1} · {(host ? "호스트" : "클라이언트")}", label);

            bool checkpointMode = DevCheats.CurrentCheckpointRoute() != null;
            DrawCheckpoints(local, host, checkpointMode);
            GUILayout.Space(6);
            GUILayout.Label("공연 난이도 바로가기", heading);
            GUILayout.Label("새 테스트 런 · 접속한 팀원도 함께 이동합니다.", label);
            bool canLaunch = (session == null || session.ConnectionState == SessionConnectionState.Offline || host)
                && (DevStartLauncher.Instance == null || !DevStartLauncher.Instance.IsBusy);
            GUI.enabled = canLaunch;
            for (int trick = 0; trick < 2; trick++)
            {
                GUILayout.BeginHorizontal();
                for (int tier = 1; tier <= RunState.MaxDifficulty; tier++)
                    if (GUILayout.Button($"{(trick == 0 ? "외줄" : "대포")} {tier}단계", button))
                        TryLaunchPerformance(trick == 0 ? CampaignSnapshot.TightropeId : CampaignSnapshot.HumanCannonId, tier);
                GUILayout.EndHorizontal();
            }
            GUI.enabled = true;
            DrawPursuitCheats();
            GUILayout.Space(6);
            GUILayout.Label("생존", heading);
            bool suppress = GUILayout.Toggle(DevCheats.SuppressFalls, " 추락 무시 (균형·줄 밖 착지·대포 추락·목마·매달림 낙하)", toggle);
            if (suppress != DevCheats.SuppressFalls)
            {
                DevCheats.SuppressFalls = suppress;
                Status = suppress ? "추락 무시 켜짐 · 톱·화염 등 직접 피해는 그대로" : "추락 무시 꺼짐";
            }
            if (!host) GUILayout.Label("※ 사망 판정은 호스트가 하므로 호스트 화면에서 켜야 모두에게 적용됩니다.", label);
            if (GUILayout.Button("균형 중앙으로", button))
            {
                bool done = local != null && local.TryGetComponent(out PlayerBalance balance) && balance.IsActive && !balance.HasFallen;
                if (done) local.GetComponent<PlayerBalance>().ResetBalance();
                Report(done, "균형을 중앙으로 되돌렸습니다.", "줄 위에서 살아 있을 때만 쓸 수 있습니다.");
            }
            GUI.enabled = host;
            if (!checkpointMode && GUILayout.Button("모두 되살리기 (제자리, 부상 회복)", button))
            {
                NetworkPlayer.ServerRestoreEveryone();
                Status = "모두 온전한 몸으로 되살렸습니다.";
            }
            GUI.enabled = true;
            if (GUILayout.Button("모두 재시작 (출발점)", button))
            {
                bool done = local != null && local.IsSpawned;
                if (done) local.RequestRestartAll();
                Report(done, "재시작을 요청했습니다(외줄 공연 중에는 호스트만).", "내 캐릭터가 없습니다.");
            }

            GUILayout.Space(6);
            GUILayout.Label("위치 이동", heading);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"앞으로 {NudgeForward:0}m", button) && local != null)
            {
                Vector3 forward = CameraViewDirector.Current != null ? CameraViewDirector.Current.ViewForward : Vector3.zero;
                if (forward == Vector3.zero) forward = local.transform.forward;
                forward.y = 0f;
                forward = forward.sqrMagnitude > .0001f ? forward.normalized : Vector3.forward;
                Report(DevCheats.Teleport(local, local.transform.position + forward * NudgeForward, Quaternion.LookRotation(forward), out var why),
                    $"앞으로 {NudgeForward:0}m 이동", why);
            }
            if (GUILayout.Button($"위로 {NudgeUp:0}m", button) && local != null)
                Report(DevCheats.Teleport(local, local.transform.position + Vector3.up * NudgeUp, local.transform.rotation, out var why),
                    $"위로 {NudgeUp:0}m 이동", why);
            GUILayout.EndHorizontal();
            if (!checkpointMode) DrawTeleportPoints(local, false);

            GUILayout.Space(8);
            if (Status.Length > 0) GUILayout.Label(Status, label);
            if (GUILayout.Button($"닫기 [{ShortcutLabel}] · 시작 지점 [{DevStartLauncher.ShortcutLabel} / F8]", button)) Toggle();
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawCheckpoints(NetworkPlayer local, bool host, bool checkpointMode)
        {
            bool escape = EscapeCampaignContent.IsTraversalActive;
            if (!escape && !checkpointMode) return;
            GUILayout.Space(6);
            bool waitingRoom = WaitingRoomScenes.IsWaitingRoom(SceneManager.GetActiveScene());
            GUILayout.Label(escape ? "탈출로 체크포인트" : waitingRoom ? "대기실 체크포인트" : "공연 체크포인트", heading);
            if (escape)
            {
                GUI.enabled = host && !EscapeCampaignContent.Current.ExitOpen;
                if (GUILayout.Button("마지막 문 열기 (호스트)", button)) TryOpenEscapeExit();
                GUI.enabled = true;
            }
            if (!checkpointMode) { DrawTeleportPoints(local, true); return; }

            GUILayout.Label("부활 · 플레이어별 마지막 통과 지점", label);
            if (host)
            {
                bool canRevive = false;
                foreach (var player in NetworkPlayer.All)
                {
                    if (!player.IsSpawned || player.Slot == NetworkPlayer.NoSlot) continue;
                    string passed = DevCheats.LastCheckpointLabel(player);
                    bool dead = player.State is PlayerState.Fallen or PlayerState.Retired;
                    canRevive |= dead && passed != null;
                    GUILayout.Label($"단원 {player.Slot + 1}{(dead ? " (사망)" : "")}: {passed ?? "통과 기록 없음"}", label);
                }
                DevCheats.HoldCheckpointWipe = GUILayout.Toggle(DevCheats.HoldCheckpointWipe, " 전원 사망 시 F9 부활 대기", toggle);
                GUI.enabled = canRevive;
                if (GUILayout.Button("사망자를 각자의 마지막 체크포인트에서 부활", button))
                {
                    DevCheats.TryRecoverTeamAtLastCheckpoint(out var recoveryMessage);
                    Status = recoveryMessage;
                }
                GUI.enabled = true;
            }
            else GUILayout.Label("체크포인트 부활은 호스트만 실행할 수 있습니다.", label);

            GUILayout.Label("순간이동 · 체크포인트 순서", label);
            if (points.Count == 0) GUILayout.Label("사용할 수 있는 체크포인트가 없습니다.", label);
            GUI.enabled = local != null && local.State == PlayerState.Normal;
            foreach (var point in points)
            {
                if (point.Order < 0 || !GUILayout.Button($"{point.Order + 1:00}. {point.Label}{(point.TeamRecovery ? "" : " (이동 전용)")}", button)) continue;
                Report(DevCheats.Teleport(local, point.Position, point.Rotation, out var why), $"{point.Label}(으)로 이동", why);
            }
            GUI.enabled = true;
        }

        void DrawTeleportPoints(NetworkPlayer local, bool escape)
        {
            bool listed = false;
            foreach (var point in points)
            {
                if (EscapeDevTeleports.IsCheckpoint(point) != escape) continue;
                listed = true;
                if (!GUILayout.Button(point.Label, button) || local == null) continue;
                Report(DevCheats.Teleport(local, point.Position, point.Rotation, out var why), $"{point.Label}(으)로 이동", why);
            }
            if (!listed) GUILayout.Label(escape ? "탈출로 체크포인트를 준비 중입니다." : "등록된 일반 이동 지점이 없습니다.", label);
        }

        public bool TryOpenEscapeExit()
        {
            var content = EscapeCampaignContent.Current;
            string reason = "탈출로 플레이 중에만 사용할 수 있습니다.";
            bool done = content != null && content.DevOpenExit(out reason);
            Report(done, "마지막 문을 열었습니다. 갈대밭으로 이동하세요.", reason);
            if (done) nextRefresh = 0f;
            return done;
        }
    }
}
