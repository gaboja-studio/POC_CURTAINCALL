using System.Collections.Generic;
using System.Collections;
using System;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.HumanCannon;
using CurtainCall.UI.SceneIntegration;
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
        public bool IsSceneryReady => scenery.Ready;
        static readonly int ShaftFadeId = Shader.PropertyToID("_ShaftFade");
        Renderer[] shaftRenderers;
        MaterialPropertyBlock shaftBlock;
        Vector4 lastShaftFade;

        void Awake()
        {
            Current = this;
            IsCampaign = EscapeSceneTransfer.Current != null && EscapeSceneTransfer.Current.HasDeparture;
            if (!IsCampaign) { RefreshShaftFade(true); return; }
            SetActive(false);
            PlaceCampaign();
            var waiting = SceneManager.GetSceneByName(GameSettings.Base.SpaceTransition.WaitingRoomScene);
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
            bool active = state.Phase == CampaignPhase.Escape || EscapeEndingPresentation.IsSuccessfulEnding(state);
            if (active && generation != state.Generation) { generation = state.Generation; arrived.Clear(); reported = false; }
            if (active && run.HasAuthority && !stateRuleInstalled)
            {
                previousStateRule = NetworkPlayer.ServerCanChangeState;
                NetworkPlayer.ServerCanChangeState = AllowState;
                stateRuleInstalled = true;
            }
            SetActive(active);
            foreach (var light in waitingLights) if (light != null) light.enabled = !active;
            if (sharedCamera != null) sharedCamera.useOcclusionCulling = active ? false : cameraOcclusion;
            if (active && gameObject.scene.isLoaded) SceneManager.SetActiveScene(gameObject.scene);
        }

        void SetActive(bool active)
        {
            using var sample = ActivationMarker.Auto();
            if (active && !activationRequested) scenery.Suspend();
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
                if (activationRequested) scenery.Restore();
                scenery.Suspend();
            }
            if (active && !activationRequested && !scenery.Ready) revealing = StartCoroutine(RevealScenery());
            activationRequested = active;
        }

        IEnumerator RevealScenery()
        {
            // Global gameplay roots and the entrance floor are active; background modules initialize as intact units.
            do { yield return null; } while (!scenery.Advance());
            revealing = null;
        }

        bool AllowState(NetworkPlayer player, PlayerState next)
        {
            if (!destroyed)
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
                NetworkPlayer player = null;
                foreach (var candidate in NetworkPlayer.All)
                    if (candidate != null && candidate.IsSpawned && candidate.OwnerClientId == id) { player = candidate; break; }
                if (arrived.Contains(id))
                {
                    if (IsCampaign || player == null || player.State != PlayerState.Normal) continue;
                    arrived.Remove(id); // A development restart returned this participant to the start.
                }
                // A disconnected participant cannot keep the remaining team waiting forever.
                if (player == null || player.State == PlayerState.Retired || player.State == PlayerState.Fallen) continue;
                if (player.State == PlayerState.Normal && ExitOpen && EscapeEntrance.Contains(arrivalArea, player.transform.position))
                {
                    arrived.Add(id);
                    CurtainCall.Telemetry.Pipeline.TelemetryRuntime.Instance?.EmitFor(player,"ESCAPE_ARRIVED",new(){["position"]=CurtainCall.Telemetry.Pipeline.DetailLogIds.Position(player.transform.position),["reason"]="REED_FIELD_ENTRY"},"HOST");
                    player.ServerSetState(PlayerState.Arrived);
                    CurtainCall.Telemetry.Pipeline.TelemetryRuntime.Instance?.SubmitEscapeArrival();
                }
                else pending++;
            }
            if (pending != 0 || arrived.Count == 0 && DevCheats.ShouldHoldCheckpointWipe()) return;
            if (!IsCampaign && arrived.Count == 0) return; // Preserve the direct-start retry when everyone fails.
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
            if (stateRuleInstalled && NetworkPlayer.ServerCanChangeState == AllowState) NetworkPlayer.ServerCanChangeState = previousStateRule;
            foreach (var light in waitingLights) if (light != null) light.enabled = true;
            if (sharedCamera != null) sharedCamera.useOcclusionCulling = cameraOcclusion;
            if (run != null) run.StepChanged -= Step;
            if (Current == this) Current = null;
        }
    }
}
