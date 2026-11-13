#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Cameras;
using CurtainCall.Dev;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.SpaceLink;
using CurtainCall.Tightrope;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CurtainCall.WaitingRoom
{
    /// <summary>로컬 opt-in만 사용하는 실제 MPPM4인 검사 도구. 정상 게임에는 생성되지 않는다.</summary>
    [DefaultExecutionOrder(-90)]
    public sealed class RehearsalMultiplayerReview : MonoBehaviour
    {
        public const string Key = "CurtainCall.Rehearsal.MultiplayerReview";
        [Serializable] public class Config { public string folder, project; public string[] clients; public bool lowMemoryPreview = true; }
        [Serializable] public class Command
        {
            public int id, lane, station, day = 1, slot, partnerSlot;
            public string action, phase = "Rehearsal", trick = "Tightrope", role = "Lower";
            public Vector3 target;
            public float distance, move, side, aux, yaw, seconds = 1;
            public bool tap, jump, release, balance = true, value;
        }
        [Serializable] public class Actor
        {
            public ulong id; public bool local, airborne, seated, flying; public Vector3 feet;
            public string state; public int floor, stack, lost, slot, learned; public long below = -1, above = -1;
        }
        [Serializable] public class Lane { public int lane, relayRenderers; public bool connected, usable; }
        [Serializable] public class Snapshot
        {
            public int process, slot, command, stage, round, participants;
            public string scene, phase, connection, game, error, shape, quest, guide, bot;
            public bool completed, blocked, control, simulated, balanceVisible, walls, pathVisible, arrowVisible, pathValid;
            public bool active, director, transitioning, curtain, room;
            public bool cannonRehearsal, cannonMastered, cannonEnding, localBalanceActive, localBalanceFallen, freeMovement;
            public bool cannonUnlocked, cannonCanPractice, cannonPrompt, scheduleConfirmed;
            public string trick, scheduleSecond, scheduleResult, focusedInteraction;
            public Vector3[] pickupPoints, practicePoints;
            public bool[] pickupVisible, practiceVisible;
            public float localBalance, courseDistance, courseFinish;
            public Vector3 courseOrigin;
            public double serverTime;
            public int cannonCheckpoint;
            public bool cannonArrived, questVisible;
            public bool cannonTooltipVisible, cannonGuideVisible;
            public string cannonHint;
            public string cannonState;
            public Vector3[] relayFeet, boarding, muzzles;
            public bool[] relayCanGrab;
            public Vector3 trampolineSurface, trampolineHandle;
            public Vector3[] transportFrom, transportTo;
            public Vector2[] transportSize;
            public bool relayActive, relaySpots, swingActive, swingView, trapezeActive;
            public Vector3 position; public Actor[] actors; public Lane[] lanes; public ulong[] relayHolders, upper, lower;
            public int[] opened; public bool[] unlimited, far; public Vector3[] poles, path;
            public bool[] canGrab, canAttach, launchUpper, launchLower; public float[] angles, speeds; public float time; public int cannonStations; public ulong[] cannonSeated;
            public bool[] transportOpen, transportBarrier; public string[] transportPhase;
            public ulong trampolineCarrier;
        }
        static readonly BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly PropertyInfo Input = typeof(PlayerInputReader).GetProperty("Current");
        int previousMipmap, previousFrameRate, previousVsync;
        TightropePracticeBotBrain brain;
        RehearsalCannonReviewBrain cannonBrain;
        Config config; int slot, lastId, inputRound = -1; Command active; double until, nextRead, nextState, nextHistory, nextSetupTap, nextResolutionCheck; string error;
        string scheduleResult;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Config config;
            int slot;
#if UNITY_EDITOR
            string json = EditorPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return;
            config = JsonUtility.FromJson<Config>(json);
            if (config == null || string.IsNullOrEmpty(config.folder)) return;
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            slot = string.Equals(project, config.project.Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                ? 0 : Array.FindIndex(config.clients ?? Array.Empty<string>(), p => string.Equals(project, p.Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) + 1;
            if (slot == 0 && !string.Equals(project, config.project.Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) return;
#else
            var args = Environment.GetCommandLineArgs();
            int folderAt = Array.IndexOf(args, "-rehearsalReview"), slotAt = Array.IndexOf(args, "-rehearsalSlot");
            if (folderAt < 0 || folderAt + 1 >= args.Length || slotAt < 0 || slotAt + 1 >= args.Length || !int.TryParse(args[slotAt + 1], out slot)) return;
            config = new Config { folder = Path.GetFullPath(args[folderAt + 1]), lowMemoryPreview = true };
#endif
            var go = new GameObject("Explicit rehearsal multiplayer review"); DontDestroyOnLoad(go);
            // The normal Lobby/SpaceLinkHub creates this on every peer. Direct LAN review bypasses that UI.
            SpaceLinkDirector.EnsureExists();
            var review = go.AddComponent<RehearsalMultiplayerReview>(); review.config = config; review.slot = slot;
            Directory.CreateDirectory(config.folder);
            review.previousMipmap = QualitySettings.globalTextureMipmapLimit;
            review.previousFrameRate = Application.targetFrameRate;
            review.previousVsync = QualitySettings.vSyncCount;
            if (config.lowMemoryPreview) QualitySettings.globalTextureMipmapLimit = Mathf.Max(2, review.previousMipmap);
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 30;
            Application.runInBackground = true;
        }
        void OnDestroy()
        {
            if (config == null) return;
            QualitySettings.globalTextureMipmapLimit = previousMipmap;
            QualitySettings.vSyncCount = previousVsync; Application.targetFrameRate = previousFrameRate;
        }
        static object Read(object target, string name)
        {
            if (target == null || target is UnityEngine.Object native && native == null) return null;
            object value = target.GetType().GetField(name, All)?.GetValue(target) ?? target.GetType().GetProperty(name, All)?.GetValue(target);
            return value is UnityEngine.Object result && result == null ? null : value;
        }
        static object Rehearsal => typeof(WaitingRoomRehearsal).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, All).Invoke(target, args);
        void Update()
        {
            // 사용자 그래픽 설정이 시작 이후 다시 적용돼도 검사 프로세스 네 개의 렌더 부하를 제한한다.
            if (config.lowMemoryPreview && Time.unscaledTimeAsDouble >= nextResolutionCheck)
            {
                nextResolutionCheck = Time.unscaledTimeAsDouble + 2;
                if (Screen.width > 960 || Screen.height > 540) Screen.SetResolution(960, 540, FullScreenMode.Windowed);
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextRead)
            {
                nextRead = now + .08;
                string file = Path.Combine(config.folder, $"command-{slot}.json");
                if (File.Exists(file)) try
                {
                    var command = JsonUtility.FromJson<Command>(File.ReadAllText(file));
                    if (command != null && command.id > lastId)
                    {
                        error = ""; lastId = command.id;
                        if (command.action is "input" or "walk" or "auto" or "cannon-auto" or "pose" or "rope-pose")
                        { active = command; until = now + Mathf.Clamp(command.seconds, 0, 120); inputRound = Rehearsal != null ? (int)Read(Rehearsal, "round") : -1; }
                        Apply(command);
                        File.AppendAllText(Path.Combine(config.folder, $"actions-{slot}.jsonl"), JsonUtility.ToJson(command) + Environment.NewLine);
                    }
                }
                catch (IOException) { }
                catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
            }
            var player = NetworkPlayer.Local;
            if (player != null && player.TryGetComponent(out PlayerInputReader reader))
            {
                var input = new PlayerInputFrame();
                if (active != null && (active.action is "input" or "walk") && inputRound >= 0 && Rehearsal != null && (int)Read(Rehearsal, "round") != inputRound) until = 0;
                if (active != null && now < until)
                {
                    input.Move = active.move; input.Posture = active.side; input.AuxDirection = (int)active.aux;
                    input.InteractTapped = active.tap; input.ActionPressed = active.jump;
                    input.InteractLongPressed = active.release; input.InteractHoldProgress = active.release ? 1 : 0;
                    active.tap = active.jump = active.release = false;
                    var mover = player.GetComponent<PlayerMover>();
                    if (active.action == "walk")
                    {
                        Vector3 delta = Vector3.ProjectOnPlane(active.target - player.transform.position, Vector3.up);
                        if (delta.magnitude < .2f) until = 0;
                        else
                        {
                            Vector3 forward = mover.FreeMovement && CameraViewDirector.Current != null && CameraViewDirector.Current.UsesMouseLook
                                ? CameraViewDirector.Current.ViewForward : mover.CourseForward;
                            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
                            input.Move = Vector3.Dot(delta.normalized, forward);
                            input.Posture = mover.FreeMovement ? Vector3.Dot(delta.normalized, Vector3.Cross(Vector3.up, forward)) : 0;
                        }
                    }
                    if (active.action == "auto" && WaitingRoomRehearsal.CurrentStage is 1 or 2)
                        input = LowPoleLessonInput(player);
                    else if (active.action == "auto" && brain != null)
                    {
                        input = brain.BuildInput();
                        var rope = TightropeCourse.Current;
                        if (rope != null)
                        {
                            float at = rope.GetDistance(player.transform.position);
                            bool straight = rope.HasRelayGap && (at < rope.SwingGapFrom - 7.4f || at > rope.SwingGapTo + 8f)
                                || rope.HasTrapezeGap && (at < rope.TrapezeGapFrom - 6.9f || at > rope.TrapezeGapTo + 4f);
                            if (straight && at < rope.FinishDistance + 1f) input.Move = 1f;
                            AssistTutorialStart(player, rope, ref input);
                        }
                    }
                    if (active.action == "cannon-auto" && cannonBrain != null) input = cannonBrain.BuildInput();
                    // Native bots already supply balance/air steering. Overwriting their airborne posture misses the narrow rope.
                    if (active.balance && active.action is not ("auto" or "cannon-auto") && !mover.FreeMovement
                        && (TightropeTrapeze.Current == null || !TightropeTrapeze.Current.IsCrossing(player))
                        && player.TryGetComponent(out PlayerBalance balance) && balance.IsActive)
                        input.Posture = -Mathf.Clamp(balance.Value / 15f, -1f, 1f);
                }
                Input.SetValue(reader, input);
            }
            if (now >= nextState) { nextState = now + .4; WriteState(); }
        }
        int lowPoleFrom = -1, lowPoleRound = -1;
        PlayerInputFrame LowPoleLessonInput(NetworkPlayer player)
        {
            // Development-only driver: uses the normal input path, never injects ownership, progress or a flight.
            var input = new PlayerInputFrame();
            var course = TightropeCourse.Current; var poles = TightropeTrapeze.Current;
            if (course == null || poles == null || player.State != PlayerState.Normal) return input;
            int round = (int)Read(Rehearsal, "round");
            if (lowPoleRound != round) { lowPoleRound = round; lowPoleFrom = -1; }
            if (Read(Rehearsal, "arrived") is System.Collections.Generic.HashSet<ulong> arrivals && arrivals.Contains(player.OwnerClientId)) return input;
            int held = poles.HeldPole(player);
            if (held >= 0)
            {
                lowPoleFrom = held;
                float angle = poles.SwingAngle(held), speed = poles.SwingSpeed(held);
                input.Move = angle > 65f ? -1f : angle < -65f ? 1f : speed < -.2f ? -1f : 1f;
                input.ActionPressed = poles.CanLaunchTowardNext(held, poles.IsLower(player))
                    && (poles.IsLower(player) || poles.PassengerAt(held) == ulong.MaxValue);
            }
            else if (poles.IsInFlight(player))
            {
                int next = lowPoleFrom + 1;
                Vector3 target = next < poles.PoleCount ? poles.PoleFeet(next, poles.HolderAt(next) != ulong.MaxValue)
                    : course.GetLanePosition(0, course.TrapezeGapTo + 1.2f);
                input.Move = Mathf.Clamp(course.GetDistance(target) - course.GetDistance(player.transform.position), -1f, 1f);
                input.Posture = Mathf.Clamp((target.x - player.transform.position.x) * 3f, -1f, 1f);
                input.InteractTapped = next < poles.PoleCount && (poles.CanGrabPole(next, player) || poles.CanAttachToPole(next, player));
            }
            else
            {
                input.Move = 1f;
                input.Posture = -Mathf.Clamp(player.GetComponent<PlayerBalance>().Value / 15f, -1f, 1f);
                if (poles.CanGrabPole(0, player) || poles.CanAttachToPole(0, player)) { input.Move = 0f; input.InteractTapped = true; }
            }
            return input;
        }

        void AssistTutorialStart(NetworkPlayer player, TightropeCourse rope, ref PlayerInputFrame input)
        {
            int stage = WaitingRoomRehearsal.CurrentStage;
            if (stage == 3)
            {
                var relay = TightropeRelayGate.Current;
                float relayDistance = rope.GetDistance(player.transform.position);
                // Clear the mouth of a shared return lane so the other three real capsules can get across.
                if (relay != null && !relay.IsHeld(player) && relayDistance >= relay.GapTo + .35f && relayDistance < relay.ExitDistance + 2f
                    && NetworkPlayer.All.Where(p => p != null && p.State == PlayerState.Normal).All(p => rope.GetDistance(p.transform.position) >= relay.GapTo + .6f)) input.Move = 1f;
                return;
            }
            if (stage != 4) return;
            var partner = NetworkPlayer.All.FirstOrDefault(p => p != null && p.Slot == active.partnerSlot);
            if (partner == null || partner.State != PlayerState.Normal) return;
            var stack = PiggybackSystem.Current; var trap = TightropeTrapeze.Current;
            float at = rope.GetDistance(player.transform.position);
            bool upper = active.role == "Upper";
            if (stage == 4 && at > rope.TrapezeGapTo + .6f && rope.GetDistance(partner.transform.position) > rope.TrapezeGapTo + .6f
                && trap != null && !trap.IsCrossing(player) && !trap.IsCrossing(partner))
            { input.Move = 1f; return; }
            if (stack != null && stack.IsInStack(player))
            {
                if (!upper && (stage == 3 || at < trap.EntryDistance - .25f)) input.Move = 1f;
                return;
            }
            if (at >= 0 || trap != null && trap.IsCrossing(player)) return;
            // The stock bot follows a human on platforms. Two automated partners need a fixed rendezvous instead of following each other.
            int pair = Mathf.Min(player.Slot, partner.Slot) / 2;
            int lane = stage == 4 ? trap.LaneIndex : (upper ? partner.Slot : player.Slot) % rope.LaneCount;
            float distance = rope.Shape.SpawnDistance - pair * 2.5f - (upper ? 1f : 0f);
            Vector3 target = rope.GetLanePosition(lane, distance);
            Vector3 forward = CameraViewDirector.Current != null ? CameraViewDirector.Current.ViewForward : rope.Forward;
            Vector2 axis = HumanCannonBotSteering.PlanarInput(player.transform.position, target, forward, .08f);
            input = new PlayerInputFrame { Move = axis.y, Posture = axis.x };
            if (upper && Vector3.ProjectOnPlane(player.transform.position - partner.transform.position, Vector3.up).magnitude < 1.3f
                && Time.timeAsDouble >= nextSetupTap)
            { input.InteractTapped = true; nextSetupTap = Time.timeAsDouble + .8d; }
        }
        void Apply(Command c)
        {
            var session = NetworkSessionManager.EnsureExists(); var player = NetworkPlayer.Local;
            switch (c.action)
            {
                case "host": _ = session.HostLanAsync(); break;
                case "join": _ = session.JoinLanAsync("127.0.0.1"); break;
                case "launch":
#if UNITY_EDITOR
                    DevMode.SetEditorEnabled(true);
#endif
                    if (!DevStartLauncher.EnsureExists().TryLaunch(new RunStartPoint(c.day, (RunStartPhase)Enum.Parse(typeof(RunStartPhase), c.phase), c.station, c.trick)))
                        throw new InvalidOperationException(DevStartLauncher.Instance.Status);
                    break;
                case "clear": if (!(bool)typeof(WaitingRoomRehearsal).GetMethod("TryClearCurrentStage").Invoke(Rehearsal, null)) throw new InvalidOperationException("Cannot clear current stage"); break;
                case "pose": player.GetComponent<PlayerMover>().Relocate(c.target, Quaternion.Euler(0, c.yaw, 0)); break;
                case "rope-pose":
                    var course = TightropeCourse.Current;
                    player.GetComponent<PlayerMover>().Relocate(course.GetLanePosition(c.lane, c.distance), Quaternion.LookRotation(course.Forward)); break;
                case "look": CameraViewDirector.Current?.SetLookRotation(Quaternion.Euler(12f, c.yaw, 0)); break;
                case "fall": player.RequestState(PlayerState.Fallen); break;
                case "vote": Invoke(Rehearsal, "RequestVote", c.value); break;
                case "skip": Invoke(Rehearsal, "RequestSkip", c.value); break;
                case "auto":
                    var partner = NetworkPlayer.All.FirstOrDefault(p => p != null && p.Slot == c.partnerSlot);
                    if (partner == null || partner == player) throw new InvalidOperationException("Review partner is not ready");
                    brain = new TightropePracticeBotBrain(player, partner);
                    brain.SetRole((TightropeBotRole)Enum.Parse(typeof(TightropeBotRole), c.role));
                    break;
                case "input": case "walk": break;
                case "cannon-auto": cannonBrain = new RehearsalCannonReviewBrain(player, c.partnerSlot); CameraViewDirector.Current?.SetLookRotation(Quaternion.Euler(12, 0, 0)); break;
                case "unlock-cannon": CampaignRun.Instance.DiscoverTrick("HumanCannon"); break;
                case "cannon-start": if (!WaitingRoomCannonRehearsal.StartPractice()) throw new InvalidOperationException("Cannon rehearsal cannot start"); break;
                case "schedule-set": scheduleResult = ScheduleBoard.Instance.Set(c.day, c.station, c.trick, 1).ToString(); break;
                case "schedule-confirm": scheduleResult = ScheduleBoard.Instance.Confirm().ToString(); break;
                case "cannon-answer": WaitingRoomCannonRehearsal.AnswerSchedulePrompt(c.value); break;
                case "cannon-abandon": WaitingRoomCannonRehearsal.VoteToAbandon(c.value); break;
                case "cannon-skip": WaitingRoomCannonRehearsal.VoteToSkip(c.value); break;
                case "capture": Capture(); break;
                case "capture-ui": ScreenCapture.CaptureScreenshot(Path.Combine(config.folder, $"client-{slot}-{lastId}-ui.png")); break;
                default: throw new ArgumentException("Unknown review action: " + c.action);
            }
        }
        void Capture()
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("No gameplay camera");
            var texture = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture; pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(config.folder, $"client-{slot}-{lastId}.png"), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(texture); if (pixels != null) Destroy(pixels); }
        }
        void WriteState()
        {
            try
            {
                var session = NetworkSessionManager.Instance; var local = NetworkPlayer.Local; var course = TightropeCourse.Current;
                var gate = TightropeRelayGate.Current; var trapeze = TightropeTrapeze.Current; var stack = PiggybackSystem.Current;
                var cannon = HumanCannonSystem.Current; var route = HumanCannonRouteSystem.Current;
                var s = new Snapshot { process = System.Diagnostics.Process.GetCurrentProcess().Id, slot = slot, command = lastId,
                    courseOrigin = course != null ? course.transform.position : Vector3.zero,
                    courseDistance = course != null && local != null ? course.GetDistance(local.transform.position) : 0f,
                    courseFinish = course != null ? course.FinishDistance : 0f,
                    time = Time.realtimeSinceStartup, scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                    serverTime = session?.NetworkManager is { IsListening: true } clock ? clock.ServerTime.Time : Time.timeAsDouble,
                    connection = session?.ConnectionState.ToString(), game = session?.GameState.ToString(), phase = CampaignRun.Instance?.State.Phase.ToString(),
                    bot = active?.action == "auto" ? brain?.Status : active?.action == "cannon-auto" ? cannonBrain?.Status : null, stage = WaitingRoomRehearsal.CurrentStage, completed = WaitingRoomRehearsal.IsCompleted, error = error,
                    blocked = InputGate.IsBlocked, position = local != null ? local.transform.position : Vector3.zero,
                    active = WaitingRoomRehearsal.IsActive, director = SpaceLinkDirector.Instance != null,
                    transitioning = SpaceLinkDirector.Instance != null && SpaceLinkDirector.Instance.IsTransitioning,
                    curtain = CurtainCall.UI.SceneIntegration.SceneCurtainTransition.IsActive, room = GameObject.Find("S09_PracticeRoom") != null,
                    cannonRehearsal = WaitingRoomCannonRehearsal.IsActive, cannonMastered = WaitingRoomCannonRehearsal.HasMastered,
                    cannonEnding = WaitingRoomCannonRehearsal.IsEnding,
                    cannonCheckpoint = local != null ? WaitingRoomCannonRehearsal.Checkpoint(local.OwnerClientId) : -1,
                    cannonArrived = local != null && WaitingRoomCannonRehearsal.HasArrived(local.OwnerClientId),
                    round = Rehearsal != null ? (int)Read(Rehearsal, "round") : -1,
                    participants = session != null ? session.PlayerCount : 0, shape = course != null ? course.ExportShape() : null,
                    guide = WaitingRoomGuidePanel.CurrentTitle, quest = (string)Read(Read(Rehearsal, "quest"), "Body"),
                    walls = Read(Rehearsal, "finishWalls") is GameObject wall && wall.activeSelf,
                    control = local != null && local.GetComponent<PlayerMover>().ControlEnabled,
                    simulated = local != null && local.GetComponent<PlayerMover>().Simulated,
                    relayActive = gate != null && gate.IsActive, relaySpots = gate != null && (bool)Read(gate, "UsesSpots"),
                    swingActive = course != null && course.HasSwingGap, swingView = TightropeSwingPole.Current != null && TightropeSwingPole.Current.PoleVisual != null,
                    trapezeActive = trapeze != null && trapeze.IsActive,
                    trampolineCarrier = HumanCannonTrampoline.Current != null ? HumanCannonTrampoline.Current.Carrier : ulong.MaxValue,
                    actors = NetworkPlayer.All.Where(p => p != null).Select(p => new Actor { id = p.OwnerClientId, local = p.IsLocal,
                        learned = Read(Rehearsal, "lessonProgress") is Dictionary<ulong, byte> progress && progress.TryGetValue(p.OwnerClientId, out var learned) ? learned : 0,
                        feet = p.transform.position, state = p.State.ToString(), airborne = p.IsAirborne, lost = (int)p.LostParts,
                        slot = p.Slot, floor = stack != null ? stack.GetFloor(p) : 0, stack = stack != null ? stack.GetStackSize(p) : 1,
                        below = stack?.GetBelow(p) is NetworkPlayer below ? (long)below.OwnerClientId : -1,
                        above = stack?.GetAbove(p) is NetworkPlayer above ? (long)above.OwnerClientId : -1,
                        seated = cannon != null && cannon.IsSeated(p.OwnerClientId), flying = cannon != null && cannon.IsFlying(p.OwnerClientId) }).ToArray() };
                if (gate != null)
                {
                    s.relayHolders = Enumerable.Range(0, gate.PoleCount).Select(gate.HolderAt).ToArray();
                    s.opened = Enumerable.Range(0, gate.PoleCount).Select(gate.OpenedLane).ToArray();
                    s.unlimited = Enumerable.Range(0, gate.PoleCount).Select(gate.IsUnlimitedHold).ToArray();
                    s.far = Enumerable.Range(0, gate.PoleCount).Select(gate.IsFarPole).ToArray();
                    s.relayFeet = Enumerable.Range(0, gate.PoleCount).Select(gate.PoleFeet).ToArray();
                    s.relayCanGrab = Enumerable.Range(0, gate.PoleCount).Select(i => gate.CanUsePole(i, local)).ToArray();
                }
                if (course != null) s.lanes = Enumerable.Range(0, course.LaneCount).Select(l => new Lane { lane = l,
                    relayRenderers = course.GetComponentsInChildren<Renderer>().Count(r => r.enabled && !r.forceRenderingOff
                        && r.GetComponentsInParent<Transform>().Any(t => t.name.StartsWith("RelayRope", StringComparison.Ordinal))
                        && r.GetComponentsInParent<Transform>().Any(t => t.name == "Rope" + l)),
                    connected = gate != null && gate.IsLaneConnected(l), usable = course.IsLaneUsable(l, (course.SwingGapFrom + course.SwingGapTo) * .5f) }).ToArray();
                if (trapeze != null) { s.upper = Enumerable.Range(0, trapeze.PoleCount).Select(trapeze.HolderAt).ToArray(); s.lower = Enumerable.Range(0, trapeze.PoleCount).Select(trapeze.PassengerAt).ToArray(); s.poles = Enumerable.Range(0, trapeze.PoleCount).Select(i => trapeze.PoleFeet(i)).ToArray();
                    s.canGrab = Enumerable.Range(0, trapeze.PoleCount).Select(i => trapeze.CanGrabPole(i, local)).ToArray();
                    s.canAttach = Enumerable.Range(0, trapeze.PoleCount).Select(i => trapeze.CanAttachToPole(i, local)).ToArray();
                    s.launchUpper = Enumerable.Range(0, trapeze.PoleCount).Select(i => trapeze.CanLaunchTowardNext(i, false)).ToArray();
                    s.launchLower = Enumerable.Range(0, trapeze.PoleCount).Select(i => trapeze.CanLaunchTowardNext(i, true)).ToArray();
                    s.angles = Enumerable.Range(0, trapeze.PoleCount).Select(trapeze.SwingAngle).ToArray();
                    s.speeds = Enumerable.Range(0, trapeze.PoleCount).Select(trapeze.SwingSpeed).ToArray(); }
                var guide = Read(Rehearsal, "guide");
                if (guide is RehearsalPathGuide pulseGuide)
                {
                    s.pathVisible = pulseGuide.IsVisible;
                    s.arrowVisible = pulseGuide.IsVisible;
                    s.path = pulseGuide.Path;
                    s.pathValid = s.path.Length >= 2;
                }
                var cannonLesson = FindFirstObjectByType<WaitingRoomCannonRehearsal>();
                s.cannonUnlocked = CampaignRun.Instance != null && CampaignRun.Instance.State.CannonUnlocked;
                s.cannonCanPractice = WaitingRoomCannonRehearsal.CanPractice;
                s.trick = CampaignRun.Instance?.State.TrickId;
                s.scheduleSecond = ScheduleBoard.Instance?.Book.Get(1, 1);
                s.scheduleConfirmed = ScheduleBoard.Instance != null && ScheduleBoard.Instance.Book.FirstDayConfirmed;
                s.scheduleResult = scheduleResult;
                s.focusedInteraction = (ShopReach.Focused as IShopPresentation)?.ActionLabel;
                var discovery = FindObjectsByType<PerformancePropDiscovery>(FindObjectsSortMode.None).FirstOrDefault(d => d.TrickId == CampaignSnapshot.HumanCannonId);
                if (discovery != null)
                {
                    var map = Read(discovery, "mapProps") as GameObject[] ?? Array.Empty<GameObject>();
                    s.pickupPoints = map.Where(p => p != null).Select(p => p.GetComponent<PerformancePropPickup>().PresentationPoint).ToArray();
                    s.pickupVisible = map.Where(p => p != null).Select(p => p.activeInHierarchy).ToArray();
                    s.practiceVisible = discovery.PracticeProps.Where(p => p != null).Select(p => p.activeInHierarchy).ToArray();
                    s.practicePoints = discovery.PracticeProps.Where(p => p != null && p.GetComponent<RehearsalCannonPracticeStation>() != null)
                        .Select(p => p.GetComponent<RehearsalCannonPracticeStation>().PresentationPoint - Vector3.up).ToArray();
                }
                if (cannonLesson != null)
                {
                    s.cannonPrompt = (bool)Read(cannonLesson, "prompt");
                    s.cannonState = JsonUtility.ToJson(Read(cannonLesson, "shared"));
                    if (s.cannonRehearsal)
                    {
                        var quest = Read(cannonLesson, "quest"); s.quest = Read(quest, "Body") as string;
                        s.questVisible = quest != null && (bool)Read(quest, "Visible");
                        s.walls = Read(cannonLesson, "walls") is GameObject cannonWall && cannonWall.activeSelf;
                        var nativeCourse = cannonLesson.Course;
                        if (nativeCourse != null)
                        {
                            s.cannonTooltipVisible = nativeCourse.Tooltip != null && nativeCourse.Tooltip.HasRuntimePresentation;
                            s.cannonGuideVisible = nativeCourse.Guide != null && nativeCourse.Guide.HasRuntimePresentation;
                            s.cannonHint = Read(nativeCourse.DeviceUi, "shownTarget") as string;
                        }
                    }
                }
                if (cannon != null) { s.cannonStations = cannon.StationCount; s.cannonSeated = Enumerable.Range(0, cannon.StationCount).Select(cannon.Occupant).ToArray();
                    s.boarding = Enumerable.Range(0, cannon.StationCount).Select(i => cannon.Station(i).BoardingPoint.position).ToArray();
                    s.muzzles = Enumerable.Range(0, cannon.StationCount).Select(i => cannon.Station(i).Muzzle.position).ToArray(); }
                if (route != null) { s.transportOpen = Enumerable.Range(0, route.TransportCount).Select(route.TrapOpen).ToArray(); s.transportBarrier = Enumerable.Range(0, route.TransportCount).Select(i => route.Transport(i).BarrierActive).ToArray(); s.transportPhase = Enumerable.Range(0, route.TransportCount).Select(i => route.TransportState(i).Phase.ToString()).ToArray();
                    s.transportFrom = Enumerable.Range(0, route.TransportCount).Select(i => route.Transport(i).StartPosition).ToArray();
                    s.transportTo = Enumerable.Range(0, route.TransportCount).Select(i => route.Transport(i).Position(1f)).ToArray();
                    s.transportSize = Enumerable.Range(0, route.TransportCount).Select(i => route.Transport(i).HalfSize).ToArray(); }
                if (HumanCannonTrampoline.Current != null) { s.trampolineSurface = HumanCannonTrampoline.Current.Surface; s.trampolineHandle = HumanCannonTrampoline.Current.Handle.position; }
                if (local != null) { var balance = local.GetComponent<PlayerBalance>(); s.localBalance = balance.Value; s.localBalanceActive = balance.IsActive; s.localBalanceFallen = balance.HasFallen; s.freeMovement = local.GetComponent<PlayerMover>().FreeMovement; }
                var binding = FindFirstObjectByType<CurtainCall.UI.SceneIntegration.PerformanceSceneBinding>();
                s.balanceVisible = binding != null && binding.LocalBalance != null && Read(binding.LocalBalance, "visibility") is CanvasGroup visible && visible.alpha > .99f;
                string path = Path.Combine(config.folder, $"state-{slot}.json");
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(s, true));
                if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
                else File.Move(path + ".tmp", path);
                if (Time.realtimeSinceStartupAsDouble >= nextHistory)
                { nextHistory = Time.realtimeSinceStartupAsDouble + 1d; File.AppendAllText(Path.Combine(config.folder, $"history-{slot}.jsonl"), JsonUtility.ToJson(s) + Environment.NewLine); }
            }
            catch (IOException) { /* A viewer may briefly hold the destination without FileShare.Delete. Retry the snapshot next frame. */ }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                File.WriteAllText(Path.Combine(config.folder, $"snapshot-error-{slot}.txt"), e.ToString());
            }
        }
    }
}
#endif
