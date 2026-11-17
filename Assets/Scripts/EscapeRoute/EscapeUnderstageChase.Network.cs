using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        // v10 adds Preparing (appended to preserve phase values); v9 added committed jump endpoints.
        const string StateMessage = "CurtainCall.Escape.Understage.Pursuer.v10";
        const double SendInterval = .1;
        enum Stage : byte { Entrance, Idle, Pursuit, CartJump, ServiceRoute }

        struct Snapshot : INetworkSerializeByMemcpy
        {
            public double Round, Time;
            public Vector3 Position;
            public Vector3 Takeoff, Touchdown;
            public Quaternion Rotation;
            public float Stride, Duck, Run, JumpTime;
            public uint Step, Landing;
            public uint JumpRevision;
            public Stage Stage;
            public AttackCue Attack;
            public ThreatCue Threat;
            public ulong LookActor;
        }

        CustomMessagingManager receiver;
        double nextSend;
        Snapshot previous, latest;
        bool hasSnapshot;
        uint heardStep, heardLanding;
        AttackCue lastSentAttack;
        ThreatCue lastSentThreat;
        ulong lastSentLookActor = ulong.MaxValue;
        bool lastSentJumping;
        uint lastSentJumpRevision;

        void Listen(CustomMessagingManager next)
        {
            if (receiver == next) return;
            receiver?.UnregisterNamedMessageHandler(StateMessage);
            receiver = next;
            receiver?.RegisterNamedMessageHandler(StateMessage, ReadSnapshot);
            hasSnapshot = false;
            HideChaseWorldUi();
        }

        void Publish(NetworkManager net, EscapeRouteTimer timer)
        {
            double now = timer.Clock;
            ulong lookActor = HostLookActor;
            bool transition = attack.Phase != lastSentAttack.Phase || attack.Started != lastSentAttack.Started || attack.Stopped != lastSentAttack.Stopped || attack.Release != lastSentAttack.Release
                || threat.Phase != lastSentThreat.Phase || threat.Started != lastSentThreat.Started || lookActor != lastSentLookActor
                || jumping != lastSentJumping || jumping && jumpRevision != lastSentJumpRevision;
            if (net.ConnectedClientsIds.Count < 2 || now < nextSend && !transition) return;
            lastSentAttack = attack;
            lastSentThreat = threat;
            lastSentLookActor = lookActor;
            lastSentJumping = jumping;
            lastSentJumpRevision = jumpRevision;
            nextSend = now + SendInterval;
            var state = CaptureSnapshot(timer.StartedAt, now, lookActor);
            using var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize<Snapshot>(), Allocator.Temp);
            writer.WriteValueSafe(state);
            foreach (var id in net.ConnectedClientsIds)
                if (id != NetworkManager.ServerClientId)
                    net.CustomMessagingManager.SendNamedMessage(StateMessage, id, writer,
                        transition ? NetworkDelivery.ReliableSequenced : NetworkDelivery.UnreliableSequenced);
        }

        Snapshot CaptureSnapshot(double round, double now, ulong lookActor)
        {
            var state = new Snapshot
            {
                Round = round, Time = now, Attack = attack, Threat = threat, LookActor = lookActor,
                Position = transform.position, Rotation = transform.rotation,
                Stride = stride, Duck = duckWeight, Run = runWeight, Step = stepNumber,
                JumpTime = jumping ? jumpTime : 0f, Landing = landingNumber,
                Takeoff = jumping ? takeoff : default, Touchdown = jumping ? touchdown : default, JumpRevision = jumpRevision,
                Stage = jumping ? Stage.CartJump : !breach.EntranceComplete ? Stage.Entrance : body.enabled ? Stage.Pursuit : Stage.Idle
            };
            if (state.Stage == Stage.Pursuit && usingBypass)
                state.Stage = Stage.ServiceRoute;
            return state;
        }

        void ReadSnapshot(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != FastBufferWriter.GetWriteSize<Snapshot>()) return;
            reader.ReadValueSafe(out Snapshot state);
            var timer = EscapeRouteTimer.Current;
            if (timer && timer.HasStarted) Accept(state, timer.StartedAt);
        }

        bool Accept(Snapshot state, double round)
        {
            if (state.Round != round || !double.IsFinite(state.Time) || state.Time < round ||
                !float.IsFinite(state.Position.sqrMagnitude) || !(Quaternion.Dot(state.Rotation, state.Rotation) is > .99f and < 1.01f) ||
                !(state.Stride is >= 0f and <= 1f) || !(state.Duck is >= 0f and <= 1f) ||
                !(state.Run is >= 0f and <= 1f) || (byte)state.Stage > (byte)Stage.ServiceRoute ||
                (byte)state.Attack.Phase > (byte)AttackPhase.Preparing || (byte)state.Attack.Reach > (byte)Reach.Wire ||
                !double.IsFinite(state.Attack.Started) || !float.IsFinite(state.Attack.Aim.sqrMagnitude) || !float.IsFinite(state.Attack.Origin.sqrMagnitude) ||
                !float.IsFinite(state.Attack.MarkedFeet.sqrMagnitude) || !float.IsFinite(state.Attack.Release.sqrMagnitude) || !float.IsFinite(state.Attack.Drop.sqrMagnitude) ||
                !float.IsFinite(state.Attack.StopPosition.sqrMagnitude) ||
                (byte)state.Threat.Phase > (byte)ThreatPhase.Impact || !double.IsFinite(state.Threat.Started) ||
                !float.IsFinite(state.Threat.From.sqrMagnitude) || !float.IsFinite(state.Threat.To.sqrMagnitude) ||
                !float.IsFinite(state.Threat.Duration) || state.Threat.Duration < 0 || state.Threat.Duration > 10 ||
                (state.Threat.Phase != ThreatPhase.None && state.Threat.Duration <= 0) ||
                !float.IsFinite(state.JumpTime) || state.JumpTime < 0f ||
                !float.IsFinite(state.Takeoff.sqrMagnitude) || !float.IsFinite(state.Touchdown.sqrMagnitude) ||
                (state.Stage == Stage.CartJump && (!jumpClip || !landingClip || state.JumpTime > JumpDuration))) return false;
            bool sameRound = hasSnapshot && latest.Round == round;
            if (sameRound && state.Time <= latest.Time) return false;
            bool sameJump = sameRound && latest.Stage == Stage.CartJump && state.Stage == Stage.CartJump
                && latest.JumpRevision == state.JumpRevision;
            if (sameJump && (!state.Takeoff.Equals(latest.Takeoff) || !state.Touchdown.Equals(latest.Touchdown)
                || state.JumpTime < latest.JumpTime)) return false;
            previous = sameRound && latest.Stage == state.Stage && (state.Stage != Stage.CartJump || sameJump) ? latest : state;
            if (!sameRound) { heardStep = state.Step; heardLanding = state.Landing; }
            latest = state;
            hasSnapshot = true;
            return true;
        }

        void PresentRemote(EscapeRouteTimer timer)
        {
            if (!CurrentSnapshot(timer)) { HideChaseWorldUi(); return; }
            double span = latest.Time - previous.Time;
            float t = span > 0 ? Mathf.Clamp01((float)((timer.Clock - SendInterval - previous.Time) / span)) : 1f;
            var rotation = Quaternion.Slerp(previous.Rotation, latest.Rotation, t);
            // The shared hall clock already animates the wall and the entering body on each peer.
            if (!breach.EntranceComplete)
            {
                HideRouteLandingWorldUi();
                if (latest.Stage == Stage.Entrance) transform.rotation = rotation;
                return;
            }
            if (latest.Stage == Stage.Entrance) { HideRouteLandingWorldUi(); return; }
            if (latest.Stage != Stage.CartJump) HideRouteLandingWorldUi();
            transform.SetPositionAndRotation(Vector3.Lerp(previous.Position, latest.Position, t), rotation);
            if (latest.Stage == Stage.Idle) StopChase();
            else
            {
                body.enabled = true;
                if (latest.Stage == Stage.CartJump)
                {
                    gait.Stop();
                    SetBodyHeight(0f);
                    float time = Mathf.Lerp(previous.JumpTime, latest.JumpTime, t);
                    SampleJumpPose(time);
                    PresentRouteLandingWorldUi(time, latest.Takeoff, latest.Touchdown);
                }
                else
                {
                    float duck = Mathf.Lerp(previous.Duck, latest.Duck, t);
                    SetBodyHeight(duck);
                    float phase = Mathf.LerpAngle(previous.Stride * 360f, latest.Stride * 360f, t) / 360f;
                    float displayedRun = AuthoredDoll ? (t < .5f ? previous.Run : latest.Run) : Mathf.Lerp(previous.Run, latest.Run, t);
                    SampleGait(Mathf.Repeat(phase, 1f), duck, displayedRun);
                }
            }
            if (bypass) bypass.SetShutter(latest.Stage == Stage.ServiceRoute, transform.position, body);
            uint landing = latest.Landing;
            if (latest.Stage == Stage.CartJump && Mathf.Lerp(previous.JumpTime, latest.JumpTime, t) < ContactTime)
                landing = previous.Landing;
            uint step = t < .5f ? previous.Step : latest.Step;
            if (landing > heardLanding) { heardLanding = landing; PlayStep(.8f); }
            else if (step > heardStep && (latest.Stage == Stage.Pursuit || latest.Stage == Stage.ServiceRoute)) PlayStep();
            if (step > heardStep) heardStep = step;
        }
    }
}
