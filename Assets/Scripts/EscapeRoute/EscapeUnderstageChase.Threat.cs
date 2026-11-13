using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        enum ThreatPhase : byte { None, ChargeWarning, DebrisWarning, DebrisFlight, Impact }
        struct ThreatCue : INetworkSerializeByMemcpy
        {
            public ThreatPhase Phase;
            public ulong Actor;
            public double Started;
            public float Duration;
            public Vector3 From, To;
        }

        [SerializeField] Transform thrownDebris;
        [SerializeField] AudioSource threatAudio;
        [SerializeField] AudioClip chargeVoice, throwVoice;
        ThreatCue threat;
        double threatRound = -1, nextDebris, voicedAt = -1;
        float previousFlight;

        void ResetThreat()
        {
            threat = default;
            nextDebris = 0;
            previousFlight = 0;
            voicedAt = -1;
            if (thrownDebris) thrownDebris.gameObject.SetActive(false);
            if (threatAudio) threatAudio.Stop();
        }

        bool AdvanceThreat()
        {
            var timer = EscapeRouteTimer.Current;
            if (threatRound != timer.StartedAt)
            {
                ResetThreat();
                threatRound = timer.StartedAt;
                nextDebris = timer.Clock + Tuning.debrisCooldown;
            }
            if (threat.Phase == ThreatPhase.None) return false;
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            float age = Mathf.Max(0, (float)(timer.Clock - threat.Started));
            Vector3 facing = Vector3.ProjectOnPlane(threat.To - transform.position, Vector3.up);
            if (facing.sqrMagnitude > .01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), 220f * Time.deltaTime);
            AdvanceGait(0, 0, 0, true, Time.deltaTime);
            if (threat.Phase == ThreatPhase.ChargeWarning)
            {
                if (age >= threat.Duration) { charging = true; threat = default; }
            }
            else if (threat.Phase == ThreatPhase.DebrisWarning)
            {
                if (age >= Tuning.debrisWarning)
                {
                    threat.Phase = ThreatPhase.DebrisFlight;
                    threat.Started = timer.Clock;
                    previousFlight = 0;
                }
            }
            else if (threat.Phase == ThreatPhase.DebrisFlight)
            {
                float t = Mathf.Clamp01(age / threat.Duration);
                var from = DebrisPosition(threat.From, threat.To, previousFlight);
                var to = DebrisPosition(threat.From, threat.To, t);
                previousFlight = t;
                if (SweepProjectile(from, to, Tuning.debrisRadius, ulong.MaxValue, out var hit) || t >= 1)
                {
                    threat.Phase = ThreatPhase.Impact;
                    threat.From = hit;
                    threat.Started = timer.Clock;
                }
            }
            else if (age >= .35f) threat = default;
            return true;
        }

        bool BeginChargeWarning(NetworkPlayer runner)
        {
            if (!runner || InShowcase || charging) return false;
            threat = new ThreatCue { Phase = ThreatPhase.ChargeWarning, Actor = runner.OwnerClientId,
                Started = EscapeRouteTimer.Current.Clock, Duration = Tuning.chargeWarning,
                From = transform.position, To = PlayerFeet(runner) + Vector3.up };
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            nextSend = 0;
            return true;
        }

        bool TryDebrisAttack(NetworkPlayer runner)
        {
            double now = EscapeRouteTimer.Current.Clock;
            if (InShowcase || !runner || attack.Phase != AttackPhase.None || threat.Phase != ThreatPhase.None || now < nextDebris)
                return false;
            Vector3 aim = PlayerFeet(runner) + Vector3.up;
            float distance = Vector3.Distance(Hand, aim);
            if (distance < Tuning.debrisMinimumRange || distance > Tuning.armRange || !ClearReach(Hand, aim, runner)) return false;
            if (fullRoute && fullRoute.Following && !fullRoute.CanAim(runner, transform.position)) return false;
            threat = new ThreatCue { Phase = ThreatPhase.DebrisWarning, Actor = runner.OwnerClientId,
                Started = now, Duration = Mathf.Clamp(distance / Tuning.debrisSpeed, .25f, 2f), From = Hand, To = aim };
            nextDebris = now + Tuning.debrisCooldown + Random.Range(0f, 2f);
            if (fullRoute) fullRoute.MarkPressured(runner);
            nextSend = 0;
            return true;
        }

        internal static Vector3 DebrisPosition(Vector3 from, Vector3 to, float t)
        {
            t = Mathf.Clamp01(t);
            float arc = Mathf.Clamp(Vector3.Distance(from, to) * .09f, .6f, 3f);
            return Vector3.Lerp(from, to, t) + Vector3.up * (4f * arc * t * (1f - t));
        }

        // Scenery clips the sweep before player capsules are tested, including players whose
        // physical colliders are temporarily disabled by poles, seats or a piggyback stack.
        bool SweepProjectile(Vector3 from, Vector3 to, float radius, ulong excluded, out Vector3 impact)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            impact = to;
            if (length < .00001f) return false;
            bool stopped = false;
            float nearestWall = length;
            foreach (var hit in Physics.SphereCastAll(from, radius, delta / length, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.GetComponentInParent<NetworkPlayer>() ||
                    hit.transform.GetComponentInParent<EscapePursuerDebris>()) continue;
                if (hit.distance < nearestWall) { nearestWall = hit.distance; stopped = true; }
            }
            Vector3 end = from + delta / length * nearestWall;
            NetworkPlayer contact = null;
            float nearest = float.PositiveInfinity;
            foreach (var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned || player.State != PlayerState.Normal || player.OwnerClientId == excluded) continue;
                var mover = player.GetComponent<PlayerMover>();
                var feet = PlayerFeet(player);
                if (!SweptSphereTouches(from, end, feet, mover.BodyHeight, mover.BodyRadius, radius)) continue;
                float distance = (feet + Vector3.up * mover.BodyHeight * .5f - from).sqrMagnitude;
                if (distance < nearest) { nearest = distance; contact = player; }
            }
            if (contact)
            {
                impact = PlayerFeet(contact) + Vector3.up;
                if (contact.ServerCut(1, "EscapePursuerProjectile", NetworkPlayer.DeathOrigin.Pursuit) != BodyPart.None)
                    ReleaseDevices(contact.OwnerClientId);
                return true;
            }
            impact = end;
            return stopped;
        }

        static void ReleaseDevices(ulong actor)
        {
            HumanCannonSystem.Current?.ServerReleaseForInspection(actor);
            HumanCannonPoleSystem.Current?.ServerReleaseForInspection(actor);
            TightropeHangSystem.Current?.ServerReleaseForInspection(actor);
            PiggybackSystem.Current?.ServerReleaseForInspection(actor);
            HumanCannonCarry.Held(actor)?.ServerReleaseForInspection(actor);
        }

        bool FindTorsoTarget(Vector3 release, out Vector3 aim, out ulong actor)
        {
            aim = default; actor = ulong.MaxValue;
            float nearest = Tuning.torsoThrowRange;
            foreach (var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned || player == victim || player.State != PlayerState.Normal) continue;
                var candidate = PlayerFeet(player);
                float distance = Vector3.Distance(release, candidate);
                if (distance >= nearest || !ClearReach(release + Vector3.up * .7f, candidate + Vector3.up * .7f, player)) continue;
                nearest = distance; aim = candidate; actor = player.OwnerClientId;
            }
            return actor != ulong.MaxValue;
        }

        void PresentThreat(ThreatCue cue, double now)
        {
            bool warning = cue.Phase == ThreatPhase.ChargeWarning || cue.Phase == ThreatPhase.DebrisWarning;
            if (thrownDebris) thrownDebris.gameObject.SetActive(cue.Phase == ThreatPhase.DebrisWarning || cue.Phase == ThreatPhase.DebrisFlight);
            if (cue.Phase == ThreatPhase.None) return;
            float age = Mathf.Max(0, (float)(now - cue.Started));
            if (warning)
            {
                if (voicedAt != cue.Started && age < .3f)
                {
                    voicedAt = cue.Started;
                    if (threatAudio) threatAudio.PlayOneShot(cue.Phase == ThreatPhase.ChargeWarning ? chargeVoice : throwVoice);
                }
                if (captureCable) { captureCable.enabled = true; captureCable.SetPosition(0, Hand); captureCable.SetPosition(1, cue.To); }
                if (aimRing) { aimRing.enabled = true; aimRing.transform.SetPositionAndRotation(cue.To - Vector3.up * .9f, Quaternion.Euler(90, 0, 0)); }
                grabArm.localRotation *= Quaternion.Euler(-35f * Mathf.Sin(Mathf.Clamp01(age / Mathf.Max(.1f, cue.Duration)) * Mathf.PI * .5f), 0, 0);
            }
            if (thrownDebris)
            {
                thrownDebris.position = cue.Phase == ThreatPhase.DebrisFlight ? DebrisPosition(cue.From, cue.To, age / cue.Duration) : Hand;
                thrownDebris.rotation = Quaternion.Euler(age * 270f, age * 190f, age * 130f);
            }
        }
    }
}
