using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Sound;
using CurtainCall.Tightrope;
using CurtainCall.WorldIndicators;
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
            public Vector3 From, To, Spread;
            public byte Stopped;
            public int Count => Spread == Vector3.zero ? 1 : 3;
            public Vector3 Endpoint(int index) => index == 0 ? To : index == 1 ? To - Spread : To + Spread;
        }

        [SerializeField] Transform thrownDebris;
        [SerializeField] AudioSource threatAudio;
        [SerializeField] AudioClip chargeVoice, throwVoice;
        ThreatCue threat;
        double threatRound = -1, nextDebris, voicedAt = -1, pressureRoarRound = -1;
        readonly Transform[] sideDebris = new Transform[2];
        readonly LineRenderer[] sideRings = new LineRenderer[2];
        readonly AreaIndicatorView[] sideLandings = new AreaIndicatorView[2];
        float previousFlight;
        SoundHandle threatSound;

        void ResetThreat()
        {
            threatSound.Stop();
            // Also reached by the chase's reset/inactive/OnDisable paths.
            HideChaseWorldUi();
            threat = default;
            nextDebris = 0;
            previousFlight = 0;
            voicedAt = -1;
            HideDebrisProps();
            if (captureCable) captureCable.enabled = false;
            if (aimRing) aimRing.enabled = false;
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
            if (threat.Phase == ThreatPhase.DebrisWarning && (!CanUseAttackHand || WaitingForConveyor(threat.Actor)))
            {
                ResetThreat(); nextDebris = timer.Clock + Tuning.debrisCooldown; nextSend = 0;
                return false;
            }
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
                    // Launch from the release pose, after turning toward the locked target.
                    threat.From = ThrowHandPosition(1f);
                    threat.Phase = ThreatPhase.DebrisFlight;
                    threat.Started = timer.Clock;
                    previousFlight = 0;
                    nextSend = 0;
                }
            }
            else if (threat.Phase == ThreatPhase.DebrisFlight)
            {
                float t = Mathf.Clamp01(age / threat.Duration);
                AdvanceDebrisFlight(t);
                if (threat.Stopped == (1 << threat.Count) - 1)
                {
                    threat.Phase = ThreatPhase.Impact;
                    threat.Started = timer.Clock;
                }
            }
            else if (age >= .35f) threat = default;
            return true;
        }

        void AdvanceDebrisFlight(float t)
        {
            for (int i = 0; i < threat.Count; i++)
            {
                int bit = 1 << i;
                if ((threat.Stopped & bit) != 0) continue;
                var aim = threat.Endpoint(i);
                var from = DebrisPosition(threat.From, aim, previousFlight);
                var to = DebrisPosition(threat.From, aim, t);
                if (SweepProjectile(from, to, Tuning.debrisRadius, ulong.MaxValue, out _) || t >= 1f)
                    threat.Stopped |= (byte)bit;
            }
            previousFlight = t;
        }

        bool BeginChargeWarning(NetworkPlayer runner)
        {
            if (InShowcase || charging) return false;
            ShowChargeWarning(runner, Tuning.entranceRoarDuration);
            return true;
        }

        bool TryPressureWarning(NetworkPlayer runner)
        {
            var timer = EscapeRouteTimer.Current;
            if (pressureRoarRound == timer.StartedAt || Pressure(runner) < .5f) return false;
            pressureRoarRound = timer.StartedAt;
            ShowChargeWarning(runner, Tuning.chargeWarning);
            return true;
        }

        void ShowChargeWarning(NetworkPlayer runner, float duration)
        {
            Vector3 point = runner ? PlayerFeet(runner) : transform.position + transform.forward * 3f;
            threat = new ThreatCue { Phase = ThreatPhase.ChargeWarning, Actor = ulong.MaxValue,
                Started = EscapeRouteTimer.Current.Clock, Duration = duration,
                From = transform.position, To = point };
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            nextSend = 0;
        }

        bool TryDebrisAttack(NetworkPlayer runner, bool bossVolley = false)
        {
            double now = EscapeRouteTimer.Current.Clock;
            if (InShowcase || !CanUseAttackHand || !runner || WaitingForConveyor(runner.OwnerClientId)
                || attack.Phase != AttackPhase.None || threat.Phase != ThreatPhase.None || now < nextDebris)
                return false;
            Vector3 aim = PlayerFeet(runner) + Vector3.up;
            Vector3 hand = Hand;
            float distance = Vector3.Distance(hand, aim);
            if (distance < Tuning.debrisMinimumRange || distance > Tuning.armRange || !ClearReach(hand, aim, runner)) return false;
            if (fullRoute && fullRoute.Following && !fullRoute.CanAim(runner, transform.position)) return false;
            if (TryPressureWarning(runner)) return true;
            threat = new ThreatCue { Phase = ThreatPhase.DebrisWarning, Actor = runner.OwnerClientId,
                Started = now, Duration = Mathf.Clamp(distance / Tuning.debrisSpeed, .25f, 2f), From = hand, To = aim };
            if (bossVolley)
            {
                var right = Vector3.Cross(Vector3.up, aim - hand).normalized;
                threat.Spread = (right == Vector3.zero ? transform.right : right) * Tuning.bossDebrisSpacing;
            }
            float cooldown = Tuning.debrisCooldown * Mathf.Lerp(1f, Tuning.pressureDebrisCooldownScale, Pressure(runner));
            nextDebris = now + cooldown + Random.Range(0f, 2f);
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
                if (!WaitingForConveyor(contact.OwnerClientId)
                    && contact.ServerCut(1, "EscapePursuerProjectile", NetworkPlayer.DeathOrigin.Pursuit) != BodyPart.None)
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
            float farthest = 0;
            foreach (var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned || player == victim || player.State != PlayerState.Normal
                    || WaitingForConveyor(player.OwnerClientId)) continue;
                var candidate = PlayerFeet(player);
                float distance = Vector3.Distance(release, candidate);
                if (distance <= farthest || !ClearThrowArc(release, candidate)) continue;
                farthest = distance; aim = candidate; actor = player.OwnerClientId;
            }
            return actor != ulong.MaxValue;
        }

        void PresentThreat(ThreatCue cue, double now)
        {
            float age = Mathf.Max(0, (float)(now - cue.Started));
            bool warning = cue.Phase == ThreatPhase.ChargeWarning || cue.Phase == ThreatPhase.DebrisWarning;
            bool debris = cue.Phase == ThreatPhase.DebrisWarning || cue.Phase == ThreatPhase.DebrisFlight;
            float progress = (age + (cue.Phase == ThreatPhase.DebrisFlight ? Tuning.debrisWarning : 0))
                / Mathf.Max(.001f, Tuning.debrisWarning + cue.Duration);
            // The opening roar belongs to the creature, without a selected player or marker.
            // A thrown object retains its separate, time-limited impact warning.
            // Do not overwrite the separate capture attack's indicators when no threat is active.
            if (cue.Phase != ThreatPhase.None)
            {
                if (captureCable) captureCable.enabled = false;
                if (attackVolume) attackVolume.Hide();
                if (grabTarget) grabTarget.Hide();
                if (aimRing) aimRing.enabled = false;
            }
            for (int i = 0; i < 3; i++)
            {
                bool active = debris && i < cue.Count && (cue.Stopped & (1 << i)) == 0;
                PresentDebrisPiece(cue, i, active, age, progress);
            }
            if (cue.Phase == ThreatPhase.None) return;
            if (cue.Phase == ThreatPhase.DebrisWarning || cue.Phase == ThreatPhase.DebrisFlight)
            {
                // Reuse the torso throw's overhand wind-up, release and recovery on the grey rig.
                SampleGait(0, 0);
                float pitch = cue.Phase == ThreatPhase.DebrisWarning
                    ? ThrowPitch(Mathf.Clamp01(age / Tuning.debrisWarning))
                    : Mathf.Lerp(ThrowPitch(1f + .2f * Mathf.Clamp01(age / Tuning.throwSwingSeconds)), 0,
                        Mathf.SmoothStep(0, 1, age / Mathf.Max(.05f, Tuning.throwSwingSeconds)));
                if (grabArm) grabArm.localRotation *= Quaternion.Euler(pitch, 0, 0);
            }
            if (warning)
            {
                if (voicedAt != cue.Started && age < .3f)
                {
                    voicedAt = cue.Started;
                    threatSound = SoundManager.PlayAt(cue.Phase == ThreatPhase.ChargeWarning
                        ? SoundCueIds.EscapeRoar : SoundCueIds.EscapeArmWarning, transform);
                }
            }
        }

        void PresentDebrisPiece(ThreatCue cue, int i, bool active, float age, float progress)
        {
            ref var landing = ref (i == 0 ? ref debrisLanding : ref sideLandings[i - 1]);
            var aim = cue.Endpoint(i);
            bool warning = cue.Phase == ThreatPhase.DebrisWarning;
            bool ringNeeded = PresentEndpointWorldUi(ref landing, active && (warning || age < cue.Duration),
                aim - Vector3.up, Tuning.debrisRadius, progress, out var support) && warning;
            if (i > 0 && ringNeeded && !sideRings[i - 1] && aimRing)
                sideRings[i - 1] = Instantiate(aimRing, aimRing.transform.parent);
            var ring = i == 0 ? aimRing : sideRings[i - 1];
            // The central ring doubles as the capture indicator between threats.
            if (ring && (i > 0 || cue.Phase != ThreatPhase.None))
            {
                ring.enabled = ringNeeded;
                if (ringNeeded) ring.transform.SetPositionAndRotation(support.point + support.normal * .07f,
                    Quaternion.FromToRotation(Vector3.up, support.normal) * Quaternion.Euler(90, 0, 0));
            }

            bool visible = active && (i == 0 || !warning);
            if (i > 0 && visible && !sideDebris[i - 1] && thrownDebris)
                sideDebris[i - 1] = Instantiate(thrownDebris, thrownDebris.parent);
            var prop = i == 0 ? thrownDebris : sideDebris[i - 1];
            if (!prop) return;
            prop.gameObject.SetActive(visible);
            if (visible)
                prop.SetPositionAndRotation(warning ? Hand : DebrisPosition(cue.From, aim, age / cue.Duration),
                    Quaternion.Euler(age * 270f, i * 37f + age * 190f, age * 130f));
        }

        void HideDebrisProps()
        {
            if (thrownDebris) thrownDebris.gameObject.SetActive(false);
            foreach (var prop in sideDebris) if (prop) prop.gameObject.SetActive(false);
        }
    }
}
