using System.Collections.Generic;
using System.Collections;
using System;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.HumanCannon;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;

namespace CurtainCall.EscapeRoute
{
    /// <summary>A activates behind the loading curtain after four-person departure. Direct A development remains supported.</summary>
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
        [SerializeField] Vector3 campaignPosition;
        [SerializeField] Vector3 campaignEuler;
        // Legacy A cues were authored in the original facility's world coordinates.
        // Track their frame independently: the hierarchy may already have been placed and saved.
        [SerializeField, HideInInspector] Vector3 hazardPathPosition;
        [SerializeField, HideInInspector] Vector3 hazardPathEuler;
        [SerializeField, HideInInspector] Vector3 hazardPathScale = Vector3.one;
        public static EscapeCampaignContent Current { get; private set; }
        public bool IsActive => content != null && content.activeSelf;
        public bool IsCampaign { get; private set; }
        readonly HashSet<ulong> arrived = new();
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

        void Awake()
        {
            Current = this;
            IsCampaign = EscapeSceneTransfer.Current != null && EscapeSceneTransfer.Current.HasDeparture;
            if (!IsCampaign) return;
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
            bool active = state.Phase == CampaignPhase.Escape;
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
            if (!IsCampaign) return;
            Bind();
            if (run == null || !run.HasAuthority || run.State.Phase != CampaignPhase.Escape || !IsActive || reported) return;
            var transfer = EscapeSceneTransfer.Current;
            if (transfer == null || !transfer.Ready || transfer.Participants.Count == 0) return;
            int pending = 0;
            foreach (ulong id in transfer.Participants)
            {
                if (arrived.Contains(id)) continue;
                NetworkPlayer player = null;
                foreach (var candidate in NetworkPlayer.All)
                    if (candidate != null && candidate.IsSpawned && candidate.OwnerClientId == id) { player = candidate; break; }
                // A disconnected participant cannot keep the remaining team waiting forever.
                if (player == null || player.State == PlayerState.Retired || player.State == PlayerState.Fallen) continue;
                if (player.State == PlayerState.Normal && exitSaw != null && exitSaw.DoorOpen && EscapeEntrance.Contains(arrivalArea, player.transform.position))
                {
                    arrived.Add(id);
                    player.ServerSetState(PlayerState.Arrived);
                }
                else pending++;
            }
            if (pending != 0) return;
            reported = true;
            run.EndEscape(arrived.Count > 0);
        }

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
