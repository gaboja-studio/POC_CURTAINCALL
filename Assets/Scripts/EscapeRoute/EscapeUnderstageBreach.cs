using CurtainCall.Network.PlayerSync;
using CurtainCall.Sound;
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
        [SerializeField] AnimationCurve entranceTravel;

        // The first foot lands clear of the opening at 3.97 s in UnderstageEntrance.
        internal const float FirstHallPlant = 3.97f;
        float lastSample = -1f, turnYaw;
        SoundHandle collapseSound;
        Vector3 impactPoint;
        EscapeUnderstageChase chase;
        struct EntranceClock : INetworkSerializeByMemcpy { public double Round, Started; public bool ArenaHandoff; }
        const string EntranceMessage = "CurtainCall.Escape.CorridorEntrance.v2";
        CustomMessagingManager receiver;
        double round = -1, started = -1, nextPublish;
        bool arenaHandoff;

        public bool EntranceComplete => entrance && lastSample >= entrance.length;
        internal float ImpactTime => impactTime;
        internal float SampleTime => Mathf.Max(0, lastSample);
        internal float Duration => entrance.length;
        internal Vector3 ContactPoint => impactPoint;
        internal float TravelAfterImpact(float time) => entranceTravel != null && entranceTravel.length > 1
            ? Mathf.Max(0, entranceTravel.Evaluate(time) - entranceTravel.Evaluate(impactTime + EscapePursuerWalls.FollowThrough))
            : Mathf.Max(0, time - impactTime - EscapePursuerWalls.FollowThrough) * 2.2f;

        void Awake() => chase = pursuer.GetComponent<EscapeUnderstageChase>();

        void Update()
        {
            var timer = EscapeRouteTimer.Current;
            bool running = timer != null && timer.gameObject.scene == gameObject.scene
                && EscapeCampaignContent.IsTraversalActive && timer.HasStarted;
            bool arenaOwnsActor = running && pursuer && pursuer.TryGetComponent<EscapePursuerBoss>(out var boss)
                && boss.TakesOverChase;
            var net = NetworkManager.Singleton;
            var messaging = net && net.IsListening && !net.IsServer ? net.CustomMessagingManager : null;
            if (receiver != messaging)
            {
                receiver?.UnregisterNamedMessageHandler(EntranceMessage);
                receiver = messaging;
                receiver?.RegisterNamedMessageHandler(EntranceMessage, ReadEntrance);
            }
            if (!running) { round = started = -1; arenaHandoff = false; nextPublish = 0; }
            else
            {
                if (round != timer.StartedAt) { round = timer.StartedAt; started = -1; arenaHandoff = false; nextPublish = 0; }
                if (net && net.IsServer)
                {
                    if (arenaOwnsActor && !arenaHandoff)
                    {
                        // Keep publishing the open wall for peers who missed the pickup or joined later.
                        arenaHandoff = true;
                        nextPublish = 0;
                    }
                    else if (!arenaHandoff && started < 0 && (!encounterArea || HasRunnerInCorridor()))
                    { started = timer.Clock; nextPublish = 0; }
                    if (timer.Clock >= nextPublish)
                    {
                        nextPublish = timer.Clock + .5;
                        using var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize<EntranceClock>(), Allocator.Temp);
                        writer.WriteValueSafe(new EntranceClock { Round = round, Started = started, ArenaHandoff = arenaHandoff });
                        foreach (var id in net.ConnectedClientsIds)
                            if (id != NetworkManager.ServerClientId)
                                net.CustomMessagingManager.SendNamedMessage(EntranceMessage, id, writer, NetworkDelivery.ReliableSequenced);
                    }
                }
            }
            float time = running && started >= 0 ? (float)(timer.Clock - started) : 0f;
            if ((arenaOwnsActor || arenaHandoff) && entrance) CompleteArenaHandoff();
            else Sample(time);
        }

        void CompleteArenaHandoff()
        {
            // Sampling the entrance clip here would overwrite the arena actor's pose and position.
            if (closedPanel) closedPanel.enabled = false;
            if (fragments && fragments.activeSelf) fragments.SetActive(false);
            if (!pursuer.activeSelf) pursuer.SetActive(true);
            lastSample = entrance.length;
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
            AcceptEntrance(state, timer.StartedAt, timer.Clock);
        }

        bool AcceptEntrance(EntranceClock state, double epoch, double clock)
        {
            if (state.Round != epoch || !double.IsFinite(state.Started)
                || (state.Started != -1 && (state.Started < state.Round || state.Started > clock + 1))) return false;
            if (round == state.Round && (arenaHandoff && !state.ArenaHandoff || started >= 0 && state.Started < 0)) return false;
            round = state.Round; started = state.Started;
            arenaHandoff = state.ArenaHandoff;
            return true;
        }

        void OnDisable()
        {
            receiver?.UnregisterNamedMessageHandler(EntranceMessage);
            receiver = null;
            if (Application.isPlaying) Sample(0f);
            lastSample = -1f;
            arenaHandoff = false;
        }

        internal void Sample(float time, bool sound = true)
        {
            if (!entrance) return;
            time = Mathf.Clamp(time, 0f, entrance.length);
            if (time == lastSample) return;
            if (SoundCatalog.Current != null && SoundCatalog.Current.Find(SoundCueIds.EscapeWallCollapse) != null)
            {
                if (concreteAndFootsteps) concreteAndFootsteps.Stop();
                if (time <= 0f || !sound) collapseSound.Stop();
                else
                {
                    if (lastSample < impactTime && time >= impactTime && time - impactTime < .3f)
                        collapseSound = SoundManager.PlayAt(SoundCueIds.EscapeWallCollapse, impactPoint);
                    if (lastSample < FirstHallPlant && time >= FirstHallPlant && time - FirstHallPlant < .3f)
                        SoundManager.PlayAt(SoundCueIds.EscapeFootstep, pursuer.transform);
                }
            }
            else if (concreteAndFootsteps && concreteAndFootsteps.clip)
            {
                if (time <= 0f || !sound) concreteAndFootsteps.Stop();
                else if ((lastSample <= 0f || time < lastSample) && time < concreteAndFootsteps.clip.length)
                {
                    concreteAndFootsteps.time = time;
                    concreteAndFootsteps.Play();
                }
            }
            entrance.SampleAnimation(gameObject, time);
            if (time <= impactTime || lastSample < impactTime)
            {
                Vector3 fist = pursuer.transform.position + Vector3.up * 3.6f;
                if (closedPanel)
                {
                    var panel = closedPanel.transform;
                    var local = panel.InverseTransformPoint(fist) - closedPanel.center;
                    Vector3 half = closedPanel.size * .5f;
                    local.x = Mathf.Clamp(local.x, -half.x, half.x);
                    local.y = Mathf.Clamp(local.y, -half.y, half.y);
                    local.z = Mathf.Clamp(local.z, -half.z, half.z);
                    fist = panel.TransformPoint(local + closedPanel.center);
                }
                else fist += pursuer.transform.forward * 2f;
                impactPoint = fist;
            }
            bool solid = time < impactTime;
            bool showFragments = time < fragmentsClearTime;
            if (closedPanel.enabled != solid) closedPanel.enabled = solid;
            if (fragments.activeSelf != showFragments) fragments.SetActive(showFragments);
            if (pursuer.activeSelf != (time > 0f)) pursuer.SetActive(time > 0f);
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
