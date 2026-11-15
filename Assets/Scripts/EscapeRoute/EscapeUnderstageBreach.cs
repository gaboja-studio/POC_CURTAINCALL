using CurtainCall.Network.PlayerSync;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>First encounter in A. The existing host-owned hall clock also drives late joins and restarts.</summary>
    [DisallowMultipleComponent]
    public sealed class EscapeUnderstageBreach : MonoBehaviour
    {
        [SerializeField] AnimationClip entrance;
        [SerializeField] BoxCollider closedPanel;
        [SerializeField] GameObject fragments;
        [SerializeField] GameObject pursuer;
        [SerializeField] AudioSource concreteAndFootsteps;
        [SerializeField] BoxCollider encounterArea;
        [SerializeField, Min(0)] float impactTime = 2.3f;
        [SerializeField, Min(0)] float fragmentsClearTime = 3.15f;

        // The first foot lands clear of the opening at 3.97 s in UnderstageEntrance.
        const float FirstHallPlant = 3.97f;
        float lastSample = -1f, turnYaw;
        EscapeUnderstageChase chase;
        struct EntranceClock : INetworkSerializeByMemcpy { public double Round, Started; }
        const string EntranceMessage = "CurtainCall.Escape.CorridorEntrance.v1";
        CustomMessagingManager receiver;
        double round = -1, started = -1, nextPublish;

        public bool EntranceComplete => entrance && lastSample >= entrance.length;
        internal float ImpactTime => impactTime;
        internal float Duration => entrance.length;

        void Awake() => chase = pursuer.GetComponent<EscapeUnderstageChase>();

        void Update()
        {
            var timer = EscapeRouteTimer.Current;
            bool running = timer != null && timer.gameObject.scene == gameObject.scene
                && EscapeCampaignContent.IsTraversalActive && timer.HasStarted;
            var net = NetworkManager.Singleton;
            var messaging = net && net.IsListening && !net.IsServer ? net.CustomMessagingManager : null;
            if (receiver != messaging)
            {
                receiver?.UnregisterNamedMessageHandler(EntranceMessage);
                receiver = messaging;
                receiver?.RegisterNamedMessageHandler(EntranceMessage, ReadEntrance);
            }
            if (!running) { round = started = -1; nextPublish = 0; }
            else
            {
                if (round != timer.StartedAt) { round = timer.StartedAt; started = -1; nextPublish = 0; }
                if (net && net.IsServer)
                {
                    if (started < 0 && (!encounterArea || HasRunnerInCorridor())) { started = timer.Clock; nextPublish = 0; }
                    if (timer.Clock >= nextPublish)
                    {
                        nextPublish = timer.Clock + .5;
                        using var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize<EntranceClock>(), Allocator.Temp);
                        writer.WriteValueSafe(new EntranceClock { Round = round, Started = started });
                        foreach (var id in net.ConnectedClientsIds)
                            if (id != NetworkManager.ServerClientId)
                                net.CustomMessagingManager.SendNamedMessage(EntranceMessage, id, writer, NetworkDelivery.ReliableSequenced);
                    }
                }
            }
            float time = running && started >= 0 ? (float)(timer.Clock - started) : 0f;
            Sample(time);
        }

        bool HasRunnerInCorridor()
        {
            foreach (var player in NetworkPlayer.All)
                if (player && player.IsSpawned && player.State == PlayerState.Normal
                    && (EscapeEntrance.Contains(encounterArea, player.transform.position)
                        || chase && chase.RunnerIsOnRoute(player))) return true;
            return false;
        }

        void ReadEntrance(ulong sender, FastBufferReader reader)
        {
            var timer = EscapeRouteTimer.Current;
            if (sender != NetworkManager.ServerClientId || !timer || !timer.HasStarted
                || reader.Length - reader.Position != FastBufferWriter.GetWriteSize<EntranceClock>()) return;
            reader.ReadValueSafe(out EntranceClock state);
            if (state.Round != timer.StartedAt || !double.IsFinite(state.Started)
                || (state.Started != -1 && (state.Started < state.Round || state.Started > timer.Clock + 1))) return;
            if (round == state.Round && started >= 0 && state.Started < 0) return;
            round = state.Round; started = state.Started;
        }

        void OnDisable()
        {
            receiver?.UnregisterNamedMessageHandler(EntranceMessage);
            receiver = null;
            if (Application.isPlaying) Sample(0f);
            lastSample = -1f;
        }

        internal void Sample(float time, bool sound = true)
        {
            if (!entrance) return;
            time = Mathf.Clamp(time, 0f, entrance.length);
            if (time == lastSample) return;
            if (time <= 0f || !sound) concreteAndFootsteps.Stop();
            else if ((lastSample <= 0f || time < lastSample) && time < concreteAndFootsteps.clip.length)
            {
                concreteAndFootsteps.time = time;
                concreteAndFootsteps.Play();
            }
            bool solid = time < impactTime;
            bool showFragments = time < fragmentsClearTime;
            if (closedPanel.enabled != solid) closedPanel.enabled = solid;
            if (fragments.activeSelf != showFragments) fragments.SetActive(showFragments);
            if (pursuer.activeSelf != (time > 0f)) pursuer.SetActive(time > 0f);
            entrance.SampleAnimation(gameObject, time);
            TurnTowardPlayer(time);
            lastSample = time;
        }

        void TurnTowardPlayer(float time)
        {
            var host = NetworkPlayer.Local;
            if (time <= FirstHallPlant || !host || !host.IsServer)
            {
                turnYaw = 0f;
                return;
            }
            var player = chase ? chase.ChooseTarget() : host;
            if (!player || player.State != PlayerState.Normal) return;
            var actor = pursuer.transform;
            Vector3 up = transform.up;
            Vector3 forward = Vector3.ProjectOnPlane(actor.forward, up);
            Vector3 toward = Vector3.ProjectOnPlane(player.transform.position - actor.position, up);
            float desired = Mathf.Clamp(Vector3.SignedAngle(forward, toward, up), -70f, 70f);
            // SampleAnimation just restored the authored rotation. Limit the added turn during the last steps.
            float delta = time - Mathf.Max(FirstHallPlant, lastSample);
            turnYaw = delta < 0f ? 0f : Mathf.MoveTowards(turnYaw, desired, 65f * delta);
            actor.rotation = Quaternion.AngleAxis(turnYaw, up) * actor.rotation;
        }
    }
}
