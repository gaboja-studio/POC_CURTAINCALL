using System.Collections.Generic;
using System.Collections;
using System;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.HumanCannon;
using CurtainCall.UI.SceneIntegration;
using CurtainCall.WaitingRoom;
using CurtainCall.Tightrope;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;

namespace CurtainCall.EscapeRoute
{
    /// <summary>A activates behind the loading curtain after all current participants depart. Direct A development remains supported.</summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class EscapeCampaignContent : MonoBehaviour
    {
        [SerializeField] GameObject content;
        [SerializeField] GameObject[] sceneRoots = Array.Empty<GameObject>();
        [SerializeField] GameObject[] activeRoots = Array.Empty<GameObject>();
        [SerializeField] GameObject[] campaignDisabledObjects = Array.Empty<GameObject>();
        [SerializeField] Behaviour[] campaignDisabledBehaviours = Array.Empty<Behaviour>();
        [SerializeField] BoxCollider arrivalArea;
        [SerializeField] EscapeExitSaw exitSaw;
        [Header("팀 목표 · 탈출로 플레이 중")]
        [SerializeField] string escapeTitle = "탈출구 열기", arrivalTitle = "갈대밭 도착";
        [Tooltip("첫 단원이 출구 장치 구역에 들어온 뒤 문이 열릴 때까지 팀에 표시합니다."), SerializeField, TextArea]
        string escapeBody = "장애물을 통과해 출구로 가세요. 절단기로 케이블을 끊으면 문이 열립니다.";
        [Tooltip("출구가 열린 뒤, 내가 도착할 때까지 표시합니다."), SerializeField, TextArea]
        string arrivalBody = "열린 출구를 지나 갈대밭에 도착하세요.";
        HudRoleCard objective;
        bool? lastDoorOpen;
        [SerializeField] Vector3 campaignPosition;
        [SerializeField] Vector3 campaignEuler;
        // Legacy A cues were authored in the original facility's world coordinates.
        // Track their frame independently: the hierarchy may already have been placed and saved.
        [SerializeField, HideInInspector] Vector3 hazardPathPosition;
        [SerializeField, HideInInspector] Vector3 hazardPathEuler;
        [SerializeField, HideInInspector] Vector3 hazardPathScale = Vector3.one;
        public static EscapeCampaignContent Current { get; private set; }
        public bool IsActive => content != null && content.activeSelf;
        public bool HasArrived(ulong actor) => arrived.Contains(actor);
        public bool ExitOpen => exitSaw != null && exitSaw.DoorOpen;
        public bool DevOpenExit(out string reason)
        {
            reason = "";
            if (!CurtainCall.Dev.DevMode.Enabled) reason = "Dev 모드를 켜 주세요.";
            else if (Current != this || !IsTraversalActive) reason = "탈출로 플레이 중에만 사용할 수 있습니다.";
            else if (exitSaw == null || !exitSaw.DevOpenExit()) reason = "탈출로 준비가 끝난 뒤 호스트가 실행해 주세요.";
            return reason.Length == 0;
        }
        public bool IsCampaign { get; private set; }
        /// <summary>추가 로드된 탈출로가 실제 진행 공간일 때만 콘텐츠 규칙을 적용한다.</summary>
        public static bool IsTraversalActive => Current != null && Current.isActiveAndEnabled
            && Current.IsActive && Current.gameObject.scene == SceneManager.GetActiveScene();
        readonly HashSet<ulong> arrived = new();
        readonly List<ulong> directParticipants = new();
        CampaignRun run;
        int generation;
        bool reported;
        readonly List<Light> waitingLights = new();
        UnityEngine.Camera sharedCamera;
        bool cameraOcclusion;
        Func<NetworkPlayer, PlayerState, bool> previousStateRule;
        bool stateRuleInstalled, destroyed;
        static readonly ProfilerMarker ActivationMarker = new("Escape.ActivateContent");
        readonly EscapeSceneryActivation scenery = new();
        Coroutine revealing;
        bool activationRequested;
        Scene departureScene;
        readonly List<GameObject> suspendedDepartureDevices = new();
        UvFlashlight routeLight, departureLight;
        public bool IsSceneryReady => scenery.Ready;
        static readonly int ShaftFadeId = Shader.PropertyToID("_ShaftFade");
        Renderer[] shaftRenderers;
        MaterialPropertyBlock shaftBlock;
        Vector4 lastShaftFade;

        void Awake()
        {
            Current = this;
            IsCampaign = EscapeSceneTransfer.Current != null && EscapeSceneTransfer.Current.HasDeparture;
            if (!IsCampaign) { SetEquipmentLight(true); RefreshShaftFade(true); return; }
            SetActive(false);
            PlaceCampaign();
            departureScene = EscapeEntrance.Current != null ? EscapeEntrance.Current.gameObject.scene
                : SceneManager.GetSceneByName(GameSettings.Base.SpaceTransition.WaitingRoomScene);
            var waiting = departureScene;
            foreach (var root in waiting.isLoaded ? waiting.GetRootGameObjects() : Array.Empty<GameObject>())
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (light.type == LightType.Directional && light.enabled) waitingLights.Add(light);
            sharedCamera = UnityEngine.Camera.main;
            if (sharedCamera != null) cameraOcclusion = sharedCamera.useOcclusionCulling;
            foreach (var item in campaignDisabledObjects) if (item != null) item.SetActive(false);
            foreach (var item in campaignDisabledBehaviours) if (item != null) item.enabled = false;
            foreach (var root in activeRoots)
                if (root != null && root.name == "EscapeBackground") scenery.Prepare(root);
            // Prepare the dormant hierarchy during the additive load, not in the departure frame.
            scenery.Suspend();
            Bind();
        }

        void Bind()
        {
            if (run == CampaignRun.Instance) return;
            if (run != null) run.StepChanged -= Step;
            run = CampaignRun.Instance;
            if (run != null) { run.StepChanged += Step; Step(run.State); }
        }

        // The saved facility frame owns the authored local coordinates. Placement is idempotent:
        // opening both scenes in the editor must show the same separation as the campaign.
        public void PlaceCampaign()
        {
            foreach (var root in sceneRoots)
                if (root != null && root.transform.parent != transform) root.transform.SetParent(transform, true);
            var rotation = Quaternion.Euler(campaignEuler);
            if ((transform.position - campaignPosition).sqrMagnitude >= .000001f
                || Quaternion.Angle(transform.rotation, rotation) >= .001f)
            {
                var before = transform.worldToLocalMatrix;
                transform.SetPositionAndRotation(campaignPosition, rotation);
                var delta = transform.localToWorldMatrix * before;
                foreach (var line in GetComponentsInChildren<LineRenderer>(true))
                {
                    if (!line.useWorldSpace) continue;
                    var points = new Vector3[line.positionCount]; line.GetPositions(points);
                    for (int i = 0; i < points.Length; i++) points[i] = delta.MultiplyPoint3x4(points[i]);
                    line.SetPositions(points);
                }
            }
            PlaceHazardPaths();
            RefreshShaftFade(true);
        }

        /// <summary>심부 어둠의 높이를 공통 설정과 시설 루트에서 계산한다. 공유 재질 자체는 바꾸지 않는다.</summary>
        public void RefreshShaftFade(bool refreshRenderers = false)
        {
            var tuning = GameSettings.EscapeRoute.ShaftFade;
            float bottom = transform.TransformPoint(Vector3.up * tuning.Bottom).y;
            float top = transform.TransformPoint(Vector3.up * tuning.Top).y;
            var fade = new Vector4(bottom, Mathf.Max(bottom + .01f, top), tuning.Strength, 0f);
            if (!refreshRenderers && shaftRenderers != null && fade == lastShaftFade) return;
            if (refreshRenderers || shaftRenderers == null) shaftRenderers = GetComponentsInChildren<Renderer>(true);
            shaftBlock ??= new MaterialPropertyBlock();
            foreach (var view in shaftRenderers)
            {
                if (view == null) continue;
                bool usesFade = false;
                foreach (var material in view.sharedMaterials)
                    if (material != null && material.HasProperty(ShaftFadeId)) { usesFade = true; break; }
                if (!usesFade) continue;
                view.GetPropertyBlock(shaftBlock);
                shaftBlock.SetVector(ShaftFadeId, fade);
                view.SetPropertyBlock(shaftBlock);
            }
            lastShaftFade = fade;
        }

        void PlaceHazardPaths()
        {
            var previousRotation = Quaternion.Euler(hazardPathEuler);
            if ((hazardPathPosition - transform.position).sqrMagnitude < .000001f
                && Quaternion.Angle(previousRotation, transform.rotation) < .001f
                && (hazardPathScale - transform.lossyScale).sqrMagnitude < .000001f) return;
            var previous = Matrix4x4.TRS(hazardPathPosition, previousRotation, hazardPathScale);
            var delta = transform.localToWorldMatrix * previous.inverse;
            foreach (var cue in GetComponentsInChildren<HumanCannonHazardCue>(true))
            {
                cue.origin = delta.MultiplyPoint3x4(cue.origin);
                cue.destination = delta.MultiplyPoint3x4(cue.destination);
            }
            hazardPathPosition = transform.position;
            hazardPathEuler = transform.eulerAngles;
            hazardPathScale = transform.lossyScale;
        }

        void Step(CampaignSnapshot state)
        {
            if (!IsCampaign) return;
            // Keep the outgoing scenery visible while failure fades to black.
            bool active = state.Phase == CampaignPhase.Escape || EscapeEndingPresentation.IsSuccessfulEnding(state)
                || FailureEndingPresentation.IsFailureEnding(state) && IsActive;
            if (active && generation != state.Generation) { generation = state.Generation; arrived.Clear(); reported = false; }
            // Release departure rules and activate the traversal run before chaining its state guard.
            SetActive(active);
            if (active && run.HasAuthority && !stateRuleInstalled)
            {
                previousStateRule = NetworkPlayer.ServerCanChangeState;
                NetworkPlayer.ServerCanChangeState = AllowState;
                stateRuleInstalled = true;
            }
            foreach (var light in waitingLights) if (light != null) light.enabled = !active;
            if (sharedCamera != null) sharedCamera.useOcclusionCulling = active ? false : cameraOcclusion;
            if (active && gameObject.scene.isLoaded) SceneManager.SetActiveScene(gameObject.scene);
        }

        void SetActive(bool active)
        {
            using var sample = ActivationMarker.Auto();
            if (active && !activationRequested)
            {
                SuspendDepartureDevices();
                SetEquipmentLight(true);
                scenery.Suspend();
            }
            if (!active && revealing != null) { StopCoroutine(revealing); revealing = null; }
            if (content != null && content.activeSelf != active) content.SetActive(active);
            foreach (var root in activeRoots)
            {
                if (root == null) continue;
                bool enabledRoot = active && Array.IndexOf(campaignDisabledObjects, root) < 0;
                if (root.activeSelf != enabledRoot) root.SetActive(enabledRoot);
            }
            if (!active)
            {
                if (activationRequested)
                {
                    scenery.Restore();
                    SetEquipmentLight(false);
                    ResumeDepartureDevices();
                }
                scenery.Suspend();
            }
            if (active && !activationRequested && !scenery.Ready) revealing = StartCoroutine(RevealScenery());
            activationRequested = active;
        }

        void SetEquipmentLight(bool active)
        {
            if (!active)
            {
                if (routeLight != null) routeLight.enabled = false;
                if (departureLight != null) departureLight.enabled = true;
                departureLight = null;
                return;
            }

            // One component draws the local and the replicated UV spot. The source
            // scene stays loaded briefly, so release its light before enabling ours.
            if (departureScene.isLoaded)
                foreach (var root in departureScene.GetRootGameObjects())
                {
                    var light = root.GetComponentInChildren<UvFlashlight>();
                    if (light == null || !light.isActiveAndEnabled) continue;
                    departureLight = light;
                    departureLight.enabled = false;
                    break;
                }
            if (routeLight == null) routeLight = GetComponent<UvFlashlight>();
            if (routeLight == null) routeLight = gameObject.AddComponent<UvFlashlight>();
            else routeLight.enabled = true;
        }

        void SuspendDepartureDevices()
        {
            if (!departureScene.isLoaded) return;
            WaitingRoomStageDevices.SetDepartureSuspended(departureScene, true);
            // Keep the departure floor and camera alive while releasing shared device channels.
            Suspend(PracticeRoomFreePlay.Current);
            Suspend(TightropeCourse.Current);
            Suspend(HumanCannonPoleSystem.Current);
            Suspend(HumanCannonTrampoline.Current);
            Suspend(HumanCannonSystem.Current);

            void Suspend(Component device)
            {
                if (device == null || device.gameObject.scene != departureScene || !device.gameObject.activeInHierarchy) return;
                var root = device.gameObject;
                suspendedDepartureDevices.Add(root);
                root.SetActive(false);
            }
        }

        void ResumeDepartureDevices()
        {
            if (departureScene.isLoaded)
                foreach (var device in suspendedDepartureDevices)
                    if (device != null) device.SetActive(true);
            suspendedDepartureDevices.Clear();
            WaitingRoomStageDevices.SetDepartureSuspended(departureScene, false);
        }

        IEnumerator RevealScenery()
        {
            // Global gameplay roots and the entrance floor are active; background modules initialize as intact units.
            do { yield return null; } while (!scenery.Advance());
            revealing = null;
        }

        bool AllowState(NetworkPlayer player, PlayerState next)
        {
            if (!destroyed && isActiveAndEnabled && IsActive)
            {
                // Arrival is an authoritative exit-volume decision, never a client request.
                if (next == PlayerState.Arrived) return false;
                if ((next == PlayerState.Fallen || next == PlayerState.Retired) && (run == null || run.State.Phase != CampaignPhase.Escape
                    || !IsActive || EscapeSceneTransfer.Current == null || !EscapeSceneTransfer.Current.Ready)) return false;
                if (next == PlayerState.Normal && run != null && run.State.Phase == CampaignPhase.Escape
                    && player != null && player.State != PlayerState.Normal) return false;
            }
            return previousStateRule == null || previousStateRule(player, next);
        }

        internal static bool CanUseRouteDevice(Scene scene, ulong owner)
        {
            var content = Current;
            return content == null || !content.IsCampaign || content.gameObject.scene != scene
                || EscapeSceneTransfer.Current?.CanParticipate(owner) == true;
        }

        internal static bool IsUnresolvedParticipant(bool connected, PlayerState? state) =>
            connected && state is null or PlayerState.Normal or PlayerState.EscapeCaptured;

        void Update()
        {
            RefreshShaftFade();
            // F8 launches skip the waiting-room transfer, but still have an authoritative escape run.
            // A standalone scene without that run keeps its existing development recovery loop.
            if (!IsCampaign && (CampaignRun.Instance == null || CampaignRun.Instance.State.Phase != CampaignPhase.Escape)) return;
            Bind();
            if (!IsCampaign && run != null && generation != run.State.Generation)
            { generation = run.State.Generation; arrived.Clear(); reported = false; }
            if (run == null || !run.HasAuthority || run.State.Phase != CampaignPhase.Escape || !IsActive || reported) return;
            var transfer = EscapeSceneTransfer.Current;
            IReadOnlyCollection<ulong> participants;
            if (IsCampaign)
            {
                if (transfer == null || !transfer.Ready || transfer.Participants.Count == 0) return;
                participants = transfer.Participants;
            }
            else
            {
                if (EscapeDevelopmentSession.AwaitingEntrance) return;
                directParticipants.Clear();
                foreach (var player in NetworkPlayer.All)
                    if (player != null && player.IsSpawned && player.Slot != NetworkPlayer.NoSlot) directParticipants.Add(player.OwnerClientId);
                if (directParticipants.Count == 0) return;
                participants = directParticipants;
            }
            int pending = 0;
            foreach (ulong id in participants)
            {
                // A disconnected owner can still have a spawned avatar for a frame.
                // Leave previously confirmed arrivals in the result, and ignore the stale avatar.
                bool connected = !IsCampaign || transfer.IsParticipantConnected(id);
                if (!connected) continue;
                var player = EscapeSceneTransfer.FindSpawnedParticipant(id);
                if (arrived.Contains(id))
                {
                    if (IsCampaign || player == null || player.State != PlayerState.Normal) continue;
                    arrived.Remove(id); // A development restart returned this participant to the start.
                }
                // Avatar availability is not a disconnect verdict. Wait for a connected member's state.
                if (player == null)
                {
                    if (IsCampaign) pending++;
                    continue;
                }
                if (player.State == PlayerState.Retired || player.State == PlayerState.Fallen) continue;
                if (player.State == PlayerState.Arrived) { arrived.Add(id); continue; }
                if (player.State == PlayerState.Normal && ExitOpen && EscapeEntrance.Contains(arrivalArea, player.transform.position))
                {
                    arrived.Add(id);
                    // 도착자는 갈대밭 엔딩에서 빈손으로 달린다. 마지막 도착은 같은 프레임에 성공 엔딩(EndGame)이 되어
                    // 절단기의 '쓸 수 없는 운반자 놓기'가 다시 돌지 않으므로, 도착 판정 순간 호스트가 운반을 풀어 모두에게 보낸다.
                    var carried = HumanCannonCarry.Held(id);
                    bool hadCutter = carried is EscapeExitSaw;
                    carried?.ServerReleaseForInspection(id);
                    CurtainCall.Telemetry.Pipeline.TelemetryRuntime.Instance?.EmitFor(player, "ESCAPE_ARRIVED", new()
                    {
                        ["position"] = CurtainCall.Telemetry.Pipeline.DetailLogIds.Position(player.transform.position),
                        ["reason"] = "REED_FIELD_ENTRY",
                        ["carriedCutterOnArrival"] = hadCutter,
                        ["cutterReleased"] = hadCutter && HumanCannonCarry.Held(id) is not EscapeExitSaw
                    }, "HOST");
                    player.ServerSetState(PlayerState.Arrived);
                    CurtainCall.Telemetry.Pipeline.TelemetryRuntime.Instance?.SubmitEscapeArrival();
                }
                else if (IsUnresolvedParticipant(connected, player.State)) pending++;
            }
            if (pending != 0 || arrived.Count == 0 && DevCheats.ShouldHoldCheckpointWipe()) return;
            reported = true;
            run.EndEscape(arrived.Count > 0);
        }

        void LateUpdate()
        {
            var local = NetworkPlayer.Local;
            if (!IsTraversalActive || SceneCurtainTransition.IsActive || local == null || local.State != PlayerState.Normal
                || (IsCampaign && (run == null || run.State.Phase != CampaignPhase.Escape || EscapeSceneTransfer.Current?.Ready != true)))
            {
                ClearObjective();
                return;
            }
            bool open = exitSaw != null && exitSaw.DoorOpen;
            if (!open && (!exitSaw || !exitSaw.TeamReachedExitZone)) { ClearObjective(); return; }
            if (lastDoorOpen == open) return;
            lastDoorOpen = open;
            string heading = open ? arrivalTitle : escapeTitle, body = open ? arrivalBody : escapeBody;
            if (objective && !objective.Closing) objective.SetText(body, heading);
            else objective = HudUIManager.Current.ShowObjective(body, transform, heading);
        }

        void ClearObjective()
        {
            if (objective) objective.Close(true);
            objective = null;
            lastDoorOpen = null;
        }

        void OnDisable() => ClearObjective();

        void OnDestroy()
        {
            destroyed = true;
            if (activationRequested) SetActive(false);
            if (stateRuleInstalled && NetworkPlayer.ServerCanChangeState == AllowState) NetworkPlayer.ServerCanChangeState = previousStateRule;
            foreach (var light in waitingLights) if (light != null) light.enabled = true;
            if (sharedCamera != null) sharedCamera.useOcclusionCulling = cameraOcclusion;
            if (run != null) run.StepChanged -= Step;
            if (Current == this) Current = null;
        }
    }
}
